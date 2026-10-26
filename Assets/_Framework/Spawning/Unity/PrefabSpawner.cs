using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.AssetManagement;
using RPGFramework.AssetManagement.Unity;
using RPGFramework.Logging;
using RPGFramework.Pooling;
using RPGFramework.Pooling.Unity;
using UnityEngine;
using ILogger = RPGFramework.Logging.ILogger;
using Object = UnityEngine.Object;

namespace RPGFramework.Spawning
{
    /// <summary>
    /// Spawns gameplay prefabs (enemies, projectiles, effects, loot drops) by key. A key resolves to a prefab
    /// registered directly or loaded through <see cref="IAssetManager"/> (Resources, Addressables, ...);
    /// keys configured for pooling are recycled through a <see cref="GameObjectPool"/>.
    /// </summary>
    public interface IPrefabSpawner : IDisposable
    {
        /// <summary>
        /// Uses this prefab for the key instead of loading it through the asset manager.
        /// </summary>
        void Register(string key, GameObject prefab);

        /// <summary>
        /// Recycles instances of this key. Takes effect when the pool is created (first spawn or <see cref="PrewarmAsync"/>).
        /// </summary>
        void ConfigurePool(string key, int initialSize = 0, int maxSize = 64);

        bool IsPooled(string key);

        /// <summary>
        /// Loads the prefab and creates its pool (with the configured initial instances) ahead of the first spawn.
        /// </summary>
        UniTask PrewarmAsync(string key, CancellationToken cancellationToken = default);

        UniTask<GameObject> SpawnAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a pooled instance to its pool, destroys any other instance.
        /// </summary>
        void Despawn(GameObject instance);

        /// <summary>
        /// Destroys the key's pool and every instance from it (including ones still in the scene), then releases the prefab.
        /// </summary>
        bool ReleasePool(string key);
    }

    public sealed class PrefabSpawner : IPrefabSpawner
    {
        private const string Category = "Spawning";
        private const string PoolKeyPrefix = "Prefab/";

        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoolSettings> _settings = new Dictionary<string, PoolSettings>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoolEntry> _pools = new Dictionary<string, PoolEntry>(StringComparer.Ordinal);
        private readonly IAssetManager _assets;
        private readonly IPoolManager _poolManager;
        private readonly ILogger _logger;
        private bool _disposed;

        /// <param name="assets">Loads prefabs for keys that were not registered directly.</param>
        /// <param name="pools">Where pools are registered (statistics, leak checks); a private manager if null.</param>
        public PrefabSpawner(IAssetManager assets = null, IPoolManager pools = null, ILogger logger = null)
        {
            _assets = assets;
            _poolManager = pools ?? new PoolManager(logger);
            _logger = logger;
        }

        public void Register(string key, GameObject prefab)
        {
            ValidateKey(key);
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            _prefabs[key] = prefab;
        }

        public void ConfigurePool(string key, int initialSize = 0, int maxSize = 64)
        {
            ValidateKey(key);
            if (maxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive");
            _settings[key] = new PoolSettings(Math.Max(0, initialSize), maxSize);
        }

        public bool IsPooled(string key) => key != null && _settings.ContainsKey(key);

        /// <summary>
        /// The key's pool, once created.
        /// </summary>
        public bool TryGetPool(string key, out GameObjectPool pool)
        {
            pool = key != null && _pools.TryGetValue(key, out var entry) ? entry.Pool : null;
            return pool != null;
        }

        public async UniTask PrewarmAsync(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!IsPooled(key))
                throw new InvalidOperationException($"Prefab '{key}' is not configured for pooling");
            await GetOrCreatePoolAsync(key, cancellationToken);
        }

        public async UniTask<GameObject> SpawnAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (IsPooled(key))
            {
                var entry = await GetOrCreatePoolAsync(key, cancellationToken);
                var pooled = entry.Pool.Spawn(position, rotation, parent);
                entry.Track(pooled);
                return pooled;
            }

            if (_prefabs.TryGetValue(key, out var prefab))
                return Object.Instantiate(prefab, position, rotation, parent);

            // The instance keeps the prefab loaded until it is destroyed
            return await RequireAssets(key).InstantiateAsync(key, position, rotation, parent, cancellationToken);
        }

        public async UniTask<T> SpawnAsync<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null,
            CancellationToken cancellationToken = default) where T : Component
        {
            var instance = await SpawnAsync(key, position, rotation, parent, cancellationToken);
            if (instance.TryGetComponent<T>(out var component))
                return component;

            Despawn(instance);
            throw new InvalidOperationException($"Prefab '{key}' has no {typeof(T).Name} component");
        }

        public void Despawn(GameObject instance)
        {
            if (!instance)
                return;
            if (instance.TryGetComponent<PooledObject>(out var pooled) && pooled.Pool != null)
                pooled.ReturnToPool();
            else
                DestroyObject(instance);
        }

        public bool ReleasePool(string key)
        {
            if (key == null || !_pools.TryGetValue(key, out var entry))
                return false;
            _pools.Remove(key);
            entry.Release(_poolManager);
            _logger.LogDebug($"Released pool for '{key}'", Category);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var entry in _pools.Values)
                entry.Release(_poolManager);
            _pools.Clear();
        }

        private async UniTask<PoolEntry> GetOrCreatePoolAsync(string key, CancellationToken cancellationToken)
        {
            if (_pools.TryGetValue(key, out var entry))
                return entry;

            AssetHandle<GameObject> handle = null;
            if (!_prefabs.TryGetValue(key, out var prefab))
            {
                handle = await RequireAssets(key).LoadAsync<GameObject>(key, cancellationToken);
                prefab = handle.Asset;

                // Disposed, or another spawn created the pool while this one was loading
                if (_disposed || _pools.TryGetValue(key, out entry))
                {
                    handle.Dispose();
                    ThrowIfDisposed();
                    return entry;
                }
            }

            var settings = _settings[key];
            var poolKey = PoolKeyPrefix + key;
            var pool = new GameObjectPool(prefab, settings.InitialSize, settings.MaxSize, null, _logger, poolKey);
            try
            {
                _poolManager.Register(poolKey, pool);
            }
            catch
            {
                pool.Dispose();
                handle?.Dispose();
                throw;
            }

            entry = new PoolEntry(poolKey, pool, handle);
            _pools.Add(key, entry);
            return entry;
        }

        private IAssetManager RequireAssets(string key) =>
            _assets ?? throw new InvalidOperationException($"Prefab '{key}' is not registered and the spawner has no asset manager");

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PrefabSpawner));
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Prefab key is required", nameof(key));
        }

        private static void DestroyObject(GameObject instance)
        {
            if (Application.isPlaying)
                Object.Destroy(instance);
            else
                Object.DestroyImmediate(instance);
        }

        private readonly struct PoolSettings
        {
            public PoolSettings(int initialSize, int maxSize)
            {
                InitialSize = initialSize;
                MaxSize = maxSize;
            }

            public int InitialSize { get; }
            public int MaxSize { get; }
        }

        private sealed class PoolEntry
        {
            // Every instance the pool handed out, so releasing the pool also destroys the ones still in the scene
            private readonly HashSet<GameObject> _spawned = new HashSet<GameObject>();
            private readonly string _poolKey;
            private readonly AssetHandle<GameObject> _handle;

            public PoolEntry(string poolKey, GameObjectPool pool, AssetHandle<GameObject> handle)
            {
                _poolKey = poolKey;
                Pool = pool;
                _handle = handle;
            }

            public GameObjectPool Pool { get; }

            public void Track(GameObject instance)
            {
                // Instances destroyed beyond the pool's max size leave dead entries; drop them now and then
                if (_spawned.Count > Pool.CountActive + Pool.CountInactive + 16)
                    _spawned.RemoveWhere(spawned => !spawned);
                _spawned.Add(instance);
            }

            public void Release(IPoolManager manager)
            {
                foreach (var instance in _spawned)
                {
                    if (instance && Pool.IsActive(instance))
                        DestroyObject(instance);
                }
                _spawned.Clear();

                manager.Remove(_poolKey, clear: false);
                Pool.Dispose();
                // Last: the prefab's assets may be unloaded once no instance needs them
                _handle?.Dispose();
            }
        }
    }
}
