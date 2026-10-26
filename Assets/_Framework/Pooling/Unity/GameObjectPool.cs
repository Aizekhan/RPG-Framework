using System;
using System.Collections.Generic;
using RPGFramework.Logging;
using UnityEngine;
using ILogger = RPGFramework.Logging.ILogger;
using Object = UnityEngine.Object;

namespace RPGFramework.Pooling.Unity
{
    /// <summary>
    /// Prefab pool: instantiates under an inactive-objects container, toggles active state, re-parents,
    /// and invokes <see cref="IPoolable"/> callbacks. Built on <see cref="ObjectPool{T}"/>, so it gets leak
    /// tracking, double-release protection and statistics, and can be registered in an <see cref="IPoolManager"/>.
    /// </summary>
    public sealed class GameObjectPool : IManagedPool, IObjectPool<GameObject>, IDisposable
    {
        private readonly GameObject _prefab;
        private readonly ObjectPool<GameObject> _pool;
        private readonly List<IPoolable> _poolables = new List<IPoolable>();

        public GameObjectPool(GameObject prefab, int initialSize = 0, int maxSize = 64, Transform container = null,
            ILogger logger = null, string name = null)
        {
            _prefab = prefab ? prefab : throw new ArgumentNullException(nameof(prefab));
            Name = name ?? prefab.name;
            Container = container ? container : new GameObject($"[Pool] {Name}").transform;
            OwnsContainer = !container;

            _pool = new ObjectPool<GameObject>(Create, null, OnRelease, OnDestroy, initialSize, maxSize, logger, Name);
        }

        public string Name { get; }
        public Type ItemType => typeof(GameObject);
        public Transform Container { get; }
        public bool IsDisposed { get; private set; }
        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;

        private bool OwnsContainer { get; }

        public Func<double> Clock
        {
            get => _pool.Clock;
            set => _pool.Clock = value;
        }

        public GameObject Get() => Spawn(Vector3.zero, Quaternion.identity, null);

        public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            ThrowIfDisposed();
            var instance = _pool.Get();
            var transform = instance.transform;
            transform.SetParent(parent, false);
            transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            Notify(instance, spawned: true);
            return instance;
        }

        public T Spawn<T>(Vector3 position, Quaternion rotation, Transform parent = null) where T : Component =>
            Spawn(position, rotation, parent).GetComponent<T>();

        public void Release(GameObject instance)
        {
            if (!instance)
            {
                // Destroyed externally: stop tracking it, nothing to recycle
                _pool.Forget(instance);
                return;
            }
            _pool.Release(instance);
        }

        public bool IsActive(GameObject instance) => _pool.IsActive(instance);

        public void Clear() => _pool.Clear();

        public int TrimExcess(int maxInactive) => _pool.TrimExcess(maxInactive);

        public IReadOnlyList<PoolLeak> FindLeaks(double thresholdSeconds) => _pool.FindLeaks(thresholdSeconds);

        public PoolStatistics GetStatistics() => _pool.GetStatistics();

        /// <summary>
        /// Destroys pooled (inactive) instances and the container if this pool created it.
        /// Active instances stay in the scene; returning them later destroys them.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;
            _pool.Clear();
            if (OwnsContainer && Container)
                UnityObjects.Destroy(Container.gameObject);
        }

        private GameObject Create()
        {
            var instance = Object.Instantiate(_prefab, Container, false);
            instance.name = _prefab.name;
            instance.SetActive(false);

            var marker = instance.GetComponent<PooledObject>();
            if (!marker)
                marker = instance.AddComponent<PooledObject>();
            marker.Pool = this;
            return instance;
        }

        private void OnRelease(GameObject instance)
        {
            Notify(instance, spawned: false);
            instance.SetActive(false);
            instance.transform.SetParent(Container, false);
        }

        private static void OnDestroy(GameObject instance)
        {
            if (instance)
                UnityObjects.Destroy(instance);
        }

        private void Notify(GameObject instance, bool spawned)
        {
            instance.GetComponentsInChildren(true, _poolables);
            foreach (var poolable in _poolables)
            {
                if (spawned)
                    poolable.OnSpawned();
                else
                    poolable.OnDespawned();
            }
            _poolables.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException($"GameObjectPool '{Name}'");
        }
    }

    public static class PoolManagerUnityExtensions
    {
        /// <summary>
        /// Creates a prefab pool and registers it under <paramref name="key"/>
        /// (pass the scene name as <paramref name="owner"/> to have it cleared with <see cref="ScenePoolCleanup"/>).
        /// </summary>
        public static GameObjectPool CreateGameObjectPool(this IPoolManager manager, string key, GameObject prefab,
            int initialSize = 0, int maxSize = 64, string owner = null, ILogger logger = null)
        {
            var pool = new GameObjectPool(prefab, initialSize, maxSize, null, logger, key);
            manager.Register(key, pool, owner);
            return pool;
        }

        public static GameObject Spawn(this IPoolManager manager, string key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!manager.TryGet<GameObjectPool>(key, out var pool))
                throw new KeyNotFoundException($"GameObject pool '{key}' is not registered");
            return pool.Spawn(position, rotation, parent);
        }
    }

    /// <summary>
    /// Removes pools owned by a scene when that scene unloads. Dispose to stop listening.
    /// </summary>
    public sealed class ScenePoolCleanup : IDisposable
    {
        private readonly IPoolManager _manager;

        public ScenePoolCleanup(IPoolManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Dispose() => UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => _manager.RemoveOwnedBy(scene.name);
    }

    internal static class UnityObjects
    {
        /// <summary>
        /// Destroy that also works in edit mode (tests, editor tools).
        /// </summary>
        public static void Destroy(Object target)
        {
            if (!target)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
