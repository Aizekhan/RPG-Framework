using System;
using System.Collections.Generic;
using RPGFramework.Logging;

namespace RPGFramework.Pooling
{
    /// <summary>
    /// An active object held longer than a threshold — a likely forgotten Release.
    /// </summary>
    public readonly struct PoolLeak
    {
        public PoolLeak(string poolName, object instance, double activeSeconds)
        {
            PoolName = poolName;
            Instance = instance;
            ActiveSeconds = activeSeconds;
        }

        public string PoolName { get; }
        public object Instance { get; }
        public double ActiveSeconds { get; }

        public override string ToString() => $"{PoolName}: {Instance} active for {ActiveSeconds:F1}s";
    }

    /// <summary>
    /// Pool that a <see cref="IPoolManager"/> can monitor and maintain without knowing its item type.
    /// </summary>
    public interface IManagedPool : IObjectPool
    {
        string Name { get; }
        Type ItemType { get; }
        PoolStatistics GetStatistics();
        int TrimExcess(int maxInactive);
        IReadOnlyList<PoolLeak> FindLeaks(double thresholdSeconds);
    }

    /// <summary>
    /// Registry of named pools with optional owners (e.g. a scene name), so every pool created for a scene
    /// can be cleared when that scene unloads.
    /// </summary>
    public interface IPoolManager
    {
        IReadOnlyCollection<string> Keys { get; }
        int TotalActive { get; }

        void Register(string key, IManagedPool pool, string owner = null);
        bool Contains(string key);
        bool TryGet<TPool>(string key, out TPool pool) where TPool : class, IManagedPool;

        /// <summary>
        /// Typed pool by key; throws if missing or of a different item type.
        /// </summary>
        IObjectPool<T> Get<T>(string key) where T : class;

        /// <summary>
        /// Unregisters (and by default clears) the pool.
        /// </summary>
        bool Remove(string key, bool clear = true);

        /// <summary>
        /// Clears and unregisters every pool registered with this owner.
        /// </summary>
        /// <returns>Number of pools removed.</returns>
        int RemoveOwnedBy(string owner);

        void ClearAll();
        IReadOnlyList<PoolStatistics> GetAllStatistics();
        IReadOnlyList<PoolLeak> FindLeaks(double thresholdSeconds);

        /// <returns>Total objects destroyed.</returns>
        int TrimExcess(int maxInactivePerPool);
    }

    public sealed class PoolManager : IPoolManager
    {
        private readonly Dictionary<string, Entry> _pools = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly ILogger _logger;

        public PoolManager(ILogger logger = null)
        {
            _logger = logger;
        }

        public IReadOnlyCollection<string> Keys => _pools.Keys;

        public int TotalActive
        {
            get
            {
                int total = 0;
                foreach (var entry in _pools.Values)
                    total += entry.Pool.CountActive;
                return total;
            }
        }

        public void Register(string key, IManagedPool pool, string owner = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Pool key is required", nameof(key));
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (_pools.ContainsKey(key))
                throw new InvalidOperationException($"Pool '{key}' is already registered");

            _pools.Add(key, new Entry(pool, owner));
            _logger.LogDebug($"Registered pool '{key}' ({pool.ItemType.Name}){(owner != null ? $" owned by '{owner}'" : "")}", "Pooling");
        }

        public bool Contains(string key) => key != null && _pools.ContainsKey(key);

        public bool TryGet<TPool>(string key, out TPool pool) where TPool : class, IManagedPool
        {
            pool = key != null && _pools.TryGetValue(key, out var entry) ? entry.Pool as TPool : null;
            return pool != null;
        }

        public IObjectPool<T> Get<T>(string key) where T : class
        {
            if (key == null || !_pools.TryGetValue(key, out var entry))
                throw new KeyNotFoundException($"Pool '{key}' is not registered");
            return entry.Pool as IObjectPool<T>
                ?? throw new InvalidCastException($"Pool '{key}' pools {entry.Pool.ItemType.Name}, not {typeof(T).Name}");
        }

        public bool Remove(string key, bool clear = true)
        {
            if (key == null || !_pools.TryGetValue(key, out var entry))
                return false;

            _pools.Remove(key);
            if (clear)
                entry.Pool.Clear();
            return true;
        }

        public int RemoveOwnedBy(string owner)
        {
            var keys = new List<string>();
            foreach (var pair in _pools)
            {
                if (pair.Value.Owner == owner)
                    keys.Add(pair.Key);
            }

            foreach (var key in keys)
                Remove(key);

            if (keys.Count > 0)
                _logger.LogDebug($"Removed {keys.Count} pools owned by '{owner}'", "Pooling");
            return keys.Count;
        }

        public void ClearAll()
        {
            foreach (var entry in _pools.Values)
                entry.Pool.Clear();
        }

        public IReadOnlyList<PoolStatistics> GetAllStatistics()
        {
            var result = new List<PoolStatistics>(_pools.Count);
            foreach (var pair in _pools)
            {
                var statistics = pair.Value.Pool.GetStatistics();
                statistics.PoolKey = pair.Key;
                result.Add(statistics);
            }
            return result;
        }

        public IReadOnlyList<PoolLeak> FindLeaks(double thresholdSeconds)
        {
            var result = new List<PoolLeak>();
            foreach (var entry in _pools.Values)
                result.AddRange(entry.Pool.FindLeaks(thresholdSeconds));
            result.Sort((a, b) => b.ActiveSeconds.CompareTo(a.ActiveSeconds));
            return result;
        }

        public int TrimExcess(int maxInactivePerPool)
        {
            int removed = 0;
            foreach (var entry in _pools.Values)
                removed += entry.Pool.TrimExcess(maxInactivePerPool);
            return removed;
        }

        private readonly struct Entry
        {
            public Entry(IManagedPool pool, string owner)
            {
                Pool = pool;
                Owner = owner;
            }

            public IManagedPool Pool { get; }
            public string Owner { get; }
        }
    }

    /// <summary>
    /// Periodic pool maintenance: logs suspected leaks and trims oversized pools.
    /// Call <see cref="Update"/> every frame (e.g. wrap it in an ActionSystem).
    /// </summary>
    public sealed class PoolMonitor
    {
        private readonly IPoolManager _pools;
        private readonly ILogger _logger;
        private readonly HashSet<object> _reported = new HashSet<object>();
        private float _sinceLeakCheck;
        private float _sinceTrim;

        public PoolMonitor(IPoolManager pools, ILogger logger = null)
        {
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
            _logger = logger;
        }

        /// <summary>
        /// An active object older than this is reported as a leak (once per activation). 0 disables.
        /// </summary>
        public double LeakThresholdSeconds { get; set; } = 120d;

        public float LeakCheckIntervalSeconds { get; set; } = 10f;

        /// <summary>
        /// 0 disables automatic trimming.
        /// </summary>
        public float TrimIntervalSeconds { get; set; }

        public int MaxInactivePerPool { get; set; } = 32;

        public int ReportedLeakCount { get; private set; }

        public void Update(float deltaTime)
        {
            _sinceLeakCheck += deltaTime;
            _sinceTrim += deltaTime;

            if (LeakThresholdSeconds > 0 && _sinceLeakCheck >= LeakCheckIntervalSeconds)
            {
                _sinceLeakCheck = 0f;
                CheckLeaks();
            }

            if (TrimIntervalSeconds > 0 && _sinceTrim >= TrimIntervalSeconds)
            {
                _sinceTrim = 0f;
                int removed = _pools.TrimExcess(MaxInactivePerPool);
                if (removed > 0)
                    _logger.LogDebug($"Trimmed {removed} idle pooled objects", "Pooling");
            }
        }

        /// <returns>Leaks reported by this call (not reported before).</returns>
        public IReadOnlyList<PoolLeak> CheckLeaks()
        {
            var leaks = _pools.FindLeaks(LeakThresholdSeconds);
            var current = new HashSet<object>();
            var fresh = new List<PoolLeak>();

            foreach (var leak in leaks)
            {
                current.Add(leak.Instance);
                if (_reported.Add(leak.Instance))
                {
                    fresh.Add(leak);
                    _logger.LogWarning($"Possible pool leak: {leak}", "Pooling");
                }
            }

            // Objects released since the last check may leak again later
            _reported.IntersectWith(current);
            ReportedLeakCount += fresh.Count;
            return fresh;
        }
    }
}
