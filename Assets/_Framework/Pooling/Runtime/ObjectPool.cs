using System;
using System.Collections.Generic;
using RPGFramework.Logging;

namespace RPGFramework.Pooling
{
    /// <summary>
    /// Generic object pool implementation with lifecycle callbacks and statistics tracking.
    /// Thread-safe for single-threaded use (typical game engine pattern).
    /// </summary>
    /// <typeparam name="T">Type of objects to pool (must be class)</typeparam>
    public class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int _maxSize;
        private readonly ILogger _logger;
        private readonly string _poolName;

        private int _activeCount;
        private int _totalCreated;
        private int _createdOnDemand; // Objects created during Get() calls (not pre-warming)
        private long _totalGetCount;
        private long _totalReturnCount;
        private int _peakActiveCount;

        /// <summary>
        /// Number of active (in-use) objects.
        /// </summary>
        public int CountActive => _activeCount;

        /// <summary>
        /// Number of inactive (available) objects in the pool.
        /// </summary>
        public int CountInactive => _pool.Count;

        /// <summary>
        /// Total pool size (active + inactive).
        /// </summary>
        public int TotalSize => _activeCount + _pool.Count;

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

            _pool = new Stack<T>(initialSize);

            // Pre-warm pool
            if (initialSize > 0)
            {
                PreWarm(initialSize);
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
            _activeCount++;
            _totalGetCount++;

            // Update peak tracking
            if (_activeCount > _peakActiveCount)
            {
                _peakActiveCount = _activeCount;
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

            _activeCount--;
        }

        /// <summary>
        /// Clears the pool, destroying all pooled objects.
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
            _activeCount = 0;

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

            // Remove excess objects
            while (_pool.Count > newSize)
            {
                var obj = _pool.Pop();
                _onDestroy?.Invoke(obj);
            }

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
        /// Gets current pool statistics.
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            return new PoolStatistics
            {
                PoolKey = _poolName,
                PoolType = typeof(T).Name,
                InitialSize = 0, // Not tracked in current implementation
                ActiveCount = _activeCount,
                InactiveCount = _pool.Count,
                TotalCreated = _totalCreated,
                CreatedOnDemand = _createdOnDemand,
                TotalGetCount = _totalGetCount,
                TotalReturnCount = _totalReturnCount,
                PeakActiveCount = _peakActiveCount,
                AverageActiveTime = 0f // Not tracked without timestamp data
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
    }
}
