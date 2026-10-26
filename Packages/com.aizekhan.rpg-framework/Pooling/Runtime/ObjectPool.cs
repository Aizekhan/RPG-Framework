using System;
using System.Collections.Generic;
using System.Diagnostics;
using RPGFramework.Logging;

namespace RPGFramework.Pooling
{
    /// <summary>
    /// Generic object pool implementation with configurable lifecycle callbacks.
    /// Tracks every object it hands out: releasing an object twice, or one that did not come from this pool,
    /// is rejected with a warning instead of corrupting the counters. Active objects carry an activation
    /// timestamp for leak detection (<see cref="FindLeaks"/>) and average-lifetime statistics.
    /// </summary>
    /// <typeparam name="T">Type of objects to pool (must be class)</typeparam>
    public class ObjectPool<T> : IManagedPool, IObjectPool<T> where T : class
    {
        private static readonly Stopwatch DefaultClockSource = Stopwatch.StartNew();

        private readonly Stack<T> _pool;
        private readonly Dictionary<T, double> _activeSince = new Dictionary<T, double>(ReferenceComparer.Instance);
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int _maxSize;
        private readonly int _initialSize;
        private readonly ILogger _logger;
        private readonly string _poolName;

        private int _totalCreated;
        private int _createdOnDemand; // Objects created during Get() calls (not pre-warming)
        private long _totalGetCount;
        private long _totalReturnCount;
        private int _peakActiveCount;
        private double _totalActiveSeconds;
        private int _rejectedReleaseCount;

        /// <summary>
        /// Number of active (in-use) objects.
        /// </summary>
        public int CountActive => _activeSince.Count;

        /// <summary>
        /// Number of inactive (available) objects in the pool.
        /// </summary>
        public int CountInactive => _pool.Count;

        /// <summary>
        /// Total pool size (active + inactive).
        /// </summary>
        public int TotalSize => CountActive + _pool.Count;

        public string Name => _poolName;

        public Type ItemType => typeof(T);

        /// <summary>
        /// Releases rejected because the object was not active in this pool (double release / foreign object).
        /// </summary>
        public int RejectedReleaseCount => _rejectedReleaseCount;

        /// <summary>
        /// Seconds used for activation timestamps; defaults to a monotonic stopwatch. Replace for tests
        /// or to follow game time.
        /// </summary>
        public Func<double> Clock { get; set; } = () => DefaultClockSource.Elapsed.TotalSeconds;

        /// <summary>
        /// Creates a new object pool with specified factory and lifecycle callbacks.
        /// </summary>
        /// <param name="createFunc">Factory function to create new instances</param>
        /// <param name="onGet">Optional callback invoked when object is retrieved from pool</param>
        /// <param name="onRelease">Optional callback invoked when object is returned to pool</param>
        /// <param name="onDestroy">Optional callback invoked when object is destroyed (pool full or cleared)</param>
        /// <param name="initialSize">Number of objects to pre-create (0 = no pre-warming)</param>
        /// <param name="maxSize">Maximum pool size (objects beyond this are destroyed on Release)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <param name="poolName">Optional pool name for logging</param>
        public ObjectPool(
            Func<T> createFunc,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            Action<T> onDestroy = null,
            int initialSize = 10,
            int maxSize = 100,
            ILogger logger = null,
            string poolName = null)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onGet = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;
            _maxSize = maxSize > 0 ? maxSize : throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive");
            _logger = logger;
            _poolName = poolName ?? typeof(T).Name;
            _initialSize = Math.Max(0, initialSize);

            _pool = new Stack<T>(_initialSize);

            // Pre-warm pool
            if (_initialSize > 0)
            {
                PreWarm(_initialSize);
            }
        }

        /// <summary>
        /// Pre-creates objects to populate the pool.
        /// </summary>
        /// <param name="count">Number of objects to create</param>
        private void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = _createFunc();
                _pool.Push(obj);
                _totalCreated++;
            }

            LogTrace($"Pre-warmed pool with {count} objects");
        }

        /// <summary>
        /// Gets an object from the pool.
        /// Creates a new instance if pool is empty.
        /// </summary>
        public T Get()
        {
            T obj;

            if (_pool.Count > 0)
            {
                obj = _pool.Pop();
                LogTrace($"Got object from pool, remaining: {_pool.Count}");
            }
            else
            {
                obj = _createFunc();
                _totalCreated++;
                _createdOnDemand++;
                LogTrace($"Created new object for pool (pool was empty), total created: {_totalCreated}");
            }

            _onGet?.Invoke(obj);
            _activeSince[obj] = Clock();
            _totalGetCount++;

            // Update peak tracking
            if (_activeSince.Count > _peakActiveCount)
            {
                _peakActiveCount = _activeSince.Count;
            }

            return obj;
        }

        /// <summary>
        /// Returns an object to the pool for reuse.
        /// If pool is at max capacity, object is destroyed.
        /// </summary>
        /// <param name="instance">Object to return (null is silently ignored)</param>
        public void Release(T instance)
        {
            if (instance == null)
            {
                LogWarning("Attempted to release null object to pool");
                return;
            }

            if (!_activeSince.TryGetValue(instance, out double since))
            {
                _rejectedReleaseCount++;
                LogWarning("Rejected release of an object that is not active in this pool (released twice or from another pool)");
                return;
            }

            _activeSince.Remove(instance);
            _totalActiveSeconds += Math.Max(0d, Clock() - since);

            _onRelease?.Invoke(instance);
            _totalReturnCount++;

            if (_pool.Count < _maxSize)
            {
                _pool.Push(instance);
                LogTrace($"Released object to pool, now contains: {_pool.Count}");
            }
            else
            {
                _onDestroy?.Invoke(instance);
                LogTrace($"Destroyed object (pool full at {_maxSize})");
            }
        }

        /// <summary>
        /// Clears the pool, destroying all pooled objects.
        /// Objects that are still active are forgotten: they are no longer tracked and releasing them later is rejected.
        /// </summary>
        public void Clear()
        {
            if (_onDestroy != null)
            {
                foreach (var obj in _pool)
                {
                    _onDestroy(obj);
                }
            }

            _pool.Clear();
            _activeSince.Clear();

            LogInfo("Pool cleared");
        }

        /// <summary>
        /// Resizes the pool to the specified size.
        /// Destroys excess objects or pre-warms additional objects as needed.
        /// </summary>
        /// <param name="newSize">Target inactive object count</param>
        public void Resize(int newSize)
        {
            if (newSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newSize), "Pool size cannot be negative");
            }

            TrimExcess(newSize);

            // Add new objects if needed
            while (_pool.Count < newSize)
            {
                var obj = _createFunc();
                _pool.Push(obj);
                _totalCreated++;
            }

            LogInfo($"Resized pool to {newSize} objects");
        }

        /// <summary>
        /// Destroys inactive objects above <paramref name="maxInactive"/>.
        /// </summary>
        /// <returns>Number of objects destroyed.</returns>
        public int TrimExcess(int maxInactive)
        {
            int removed = 0;
            while (_pool.Count > Math.Max(0, maxInactive))
            {
                var obj = _pool.Pop();
                _onDestroy?.Invoke(obj);
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// Active objects held for longer than <paramref name="thresholdSeconds"/>, oldest first.
        /// </summary>
        public IReadOnlyList<PoolLeak> FindLeaks(double thresholdSeconds)
        {
            double now = Clock();
            var leaks = new List<PoolLeak>();
            foreach (var pair in _activeSince)
            {
                double age = now - pair.Value;
                if (age >= thresholdSeconds)
                    leaks.Add(new PoolLeak(_poolName, pair.Key, age));
            }
            leaks.Sort((a, b) => b.ActiveSeconds.CompareTo(a.ActiveSeconds));
            return leaks;
        }

        public bool IsActive(T instance) => instance != null && _activeSince.ContainsKey(instance);

        /// <summary>
        /// Stops tracking an active object without returning it (it was destroyed or handed off elsewhere).
        /// </summary>
        public bool Forget(T instance) => instance != null && _activeSince.Remove(instance);

        /// <summary>
        /// Gets current pool statistics.
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            return new PoolStatistics
            {
                PoolKey = _poolName,
                PoolType = typeof(T).Name,
                InitialSize = _initialSize,
                ActiveCount = CountActive,
                InactiveCount = _pool.Count,
                TotalCreated = _totalCreated,
                CreatedOnDemand = _createdOnDemand,
                TotalGetCount = _totalGetCount,
                TotalReturnCount = _totalReturnCount,
                PeakActiveCount = _peakActiveCount,
                AverageActiveTime = _totalReturnCount > 0 ? (float)(_totalActiveSeconds / _totalReturnCount) : 0f
            };
        }

        #region Logging

        private void LogInfo(string message)
        {
            _logger?.Log(LogSeverity.Information, $"[Pool: {_poolName}] {message}", "Pooling");
        }

        private void LogWarning(string message)
        {
            _logger?.Log(LogSeverity.Warning, $"[Pool: {_poolName}] {message}", "Pooling");
        }

        private void LogTrace(string message)
        {
            _logger?.Log(LogSeverity.Trace, $"[Pool: {_poolName}] {message}", "Pooling");
        }

        #endregion

        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(T x, T y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
