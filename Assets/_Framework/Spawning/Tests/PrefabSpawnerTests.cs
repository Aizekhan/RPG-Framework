using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.AssetManagement;
using RPGFramework.Pooling;
using RPGFramework.Pooling.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.Spawning.Tests
{
    /// <summary>
    /// Provider that hands out test prefabs synchronously and records releases.
    /// </summary>
    internal sealed class PrefabProvider : IAssetProvider
    {
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

        public string Name => "Test";
        public int Priority => 0;
        public List<string> Releases { get; } = new List<string>();

        public void Add(string key, GameObject prefab) => _prefabs[key] = prefab;

        public UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class =>
            UniTask.FromResult(_prefabs.TryGetValue(key, out var prefab) ? prefab as T : null);

        public void Release(string key, object asset) => Releases.Add(key);
    }

    public sealed class PrefabSpawnerTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private PrefabProvider _provider;
        private AssetManager _assets;
        private PoolManager _pools;
        private PrefabSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _provider = new PrefabProvider();
            _assets = new AssetManager(null, _provider);
            _pools = new PoolManager();
            _spawner = new PrefabSpawner(_assets, _pools);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner.Dispose();
            foreach (var obj in _cleanup)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private GameObject Prefab(string name, params Type[] components)
        {
            var prefab = new GameObject(name, components);
            _cleanup.Add(prefab);
            return prefab;
        }

        private GameObject Spawn(string key, Vector3 position = default)
        {
            var task = _spawner.SpawnAsync(key, position, Quaternion.identity);
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            var instance = task.GetAwaiter().GetResult();
            _cleanup.Add(instance);
            return instance;
        }

        [Test]
        public void Spawn_RegisteredPrefab_InstantiatesAtPosition()
        {
            _spawner.Register("Goblin", Prefab("Goblin"));

            var instance = Spawn("Goblin", new Vector3(1f, 2f, 3f));

            Assert.That(instance.name, Does.StartWith("Goblin"));
            Assert.That(instance.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void Despawn_UnpooledInstance_DestroysIt()
        {
            _spawner.Register("Goblin", Prefab("Goblin"));
            var instance = Spawn("Goblin");

            _spawner.Despawn(instance);

            Assert.That(instance == null, Is.True);
        }

        [Test]
        public void Spawn_AssetKey_KeepsPrefabLoadedUntilInstanceIsDestroyed()
        {
            _provider.Add("Arrow", Prefab("Arrow"));

            var instance = Spawn("Arrow");
            Assert.That(_assets.GetReferenceCount("Arrow"), Is.EqualTo(1));

            _spawner.Despawn(instance);

            Assert.That(_assets.GetReferenceCount("Arrow"), Is.EqualTo(0));
            Assert.That(_provider.Releases, Is.EqualTo(new[] { "Arrow" }));
        }

        [Test]
        public void Spawn_PooledKey_ReusesDespawnedInstance()
        {
            _spawner.Register("Spark", Prefab("Spark"));
            _spawner.ConfigurePool("Spark");

            var first = Spawn("Spark");
            _spawner.Despawn(first);
            var second = Spawn("Spark", Vector3.up);

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.activeSelf, Is.True);
            Assert.That(second.transform.position, Is.EqualTo(Vector3.up));
            Assert.That(_spawner.TryGetPool("Spark", out var pool), Is.True);
            Assert.That(pool.CountActive, Is.EqualTo(1));
        }

        [Test]
        public void Despawn_PooledInstance_DeactivatesInsteadOfDestroying()
        {
            _spawner.Register("Spark", Prefab("Spark"));
            _spawner.ConfigurePool("Spark");
            var instance = Spawn("Spark");

            _spawner.Despawn(instance);

            Assert.That(instance != null, Is.True);
            Assert.That(instance.activeSelf, Is.False);
        }

        [Test]
        public void Prewarm_CreatesConfiguredInstancesAndRegistersThePool()
        {
            _spawner.Register("Spark", Prefab("Spark"));
            _spawner.ConfigurePool("Spark", initialSize: 3, maxSize: 8);

            _spawner.PrewarmAsync("Spark").GetAwaiter().GetResult();

            Assert.That(_spawner.TryGetPool("Spark", out var pool), Is.True);
            Assert.That(pool.CountInactive, Is.EqualTo(3));
            Assert.That(_pools.Contains("Prefab/Spark"), Is.True);
        }

        [Test]
        public void Prewarm_KeyWithoutPoolSettings_Throws()
        {
            _spawner.Register("Goblin", Prefab("Goblin"));

            Assert.Throws<InvalidOperationException>(() => _spawner.PrewarmAsync("Goblin").GetAwaiter().GetResult());
        }

        [Test]
        public void ReleasePool_DestroysLiveInstancesThenReleasesThePrefab()
        {
            _provider.Add("Arrow", Prefab("Arrow"));
            _spawner.ConfigurePool("Arrow");
            var live = Spawn("Arrow");
            Assert.That(_assets.GetReferenceCount("Arrow"), Is.EqualTo(1));

            Assert.That(_spawner.ReleasePool("Arrow"), Is.True);

            Assert.That(live == null, Is.True);
            Assert.That(_assets.GetReferenceCount("Arrow"), Is.EqualTo(0));
            Assert.That(_pools.Contains("Prefab/Arrow"), Is.False);
            Assert.That(_spawner.ReleasePool("Arrow"), Is.False);
        }

        [Test]
        public void Spawn_UnknownKeyWithoutAssetManager_Throws()
        {
            var spawner = new PrefabSpawner();

            Assert.Throws<InvalidOperationException>(() =>
                spawner.SpawnAsync("Missing", Vector3.zero, Quaternion.identity).GetAwaiter().GetResult());
        }

        [Test]
        public void Spawn_KeyNoProviderHas_ThrowsAssetNotFound()
        {
            Assert.Throws<AssetNotFoundException>(() =>
                _spawner.SpawnAsync("Missing", Vector3.zero, Quaternion.identity).GetAwaiter().GetResult());
        }

        [Test]
        public void SpawnTyped_MissingComponent_ThrowsAndDestroysTheInstance()
        {
            _spawner.Register("Goblin", Prefab("Goblin"));

            Assert.Throws<InvalidOperationException>(() =>
                _spawner.SpawnAsync<Rigidbody>("Goblin", Vector3.zero, Quaternion.identity).GetAwaiter().GetResult());
            Assert.That(GameObject.Find("Goblin(Clone)"), Is.Null);
        }

        [Test]
        public void SpawnTyped_ReturnsTheComponent()
        {
            _spawner.Register("Body", Prefab("Body", typeof(Rigidbody)));

            var body = _spawner.SpawnAsync<Rigidbody>("Body", Vector3.zero, Quaternion.identity).GetAwaiter().GetResult();
            _cleanup.Add(body.gameObject);

            Assert.That(body, Is.Not.Null);
        }

        [Test]
        public void Dispose_ReleasesEveryPoolAndRejectsNewSpawns()
        {
            _provider.Add("Arrow", Prefab("Arrow"));
            _spawner.ConfigurePool("Arrow", initialSize: 2);
            _spawner.PrewarmAsync("Arrow").GetAwaiter().GetResult();

            _spawner.Dispose();

            Assert.That(_assets.GetReferenceCount("Arrow"), Is.EqualTo(0));
            Assert.That(_pools.Keys, Is.Empty);
            Assert.Throws<ObjectDisposedException>(() =>
                _spawner.SpawnAsync("Arrow", Vector3.zero, Quaternion.identity).GetAwaiter().GetResult());
        }

        [Test]
        public void Catalog_RegistersPrefabsAndPoolSettings()
        {
            var catalog = PrefabCatalog.Create(new[]
            {
                new PrefabCatalog.Entry { Key = "Goblin", Prefab = Prefab("Goblin") },
                new PrefabCatalog.Entry { Key = "Spark", Prefab = Prefab("Spark"), Pooled = true, PrewarmCount = 2, MaxPoolSize = 4 },
                new PrefabCatalog.Entry { Key = "" }
            });
            _cleanup.Add(catalog);

            catalog.ApplyTo(_spawner);
            catalog.PrewarmAsync(_spawner).GetAwaiter().GetResult();

            Assert.That(_spawner.IsPooled("Goblin"), Is.False);
            Assert.That(_spawner.IsPooled("Spark"), Is.True);
            Assert.That(_spawner.TryGetPool("Spark", out var pool), Is.True);
            Assert.That(pool.CountInactive, Is.EqualTo(2));
            Assert.That(Spawn("Goblin"), Is.Not.Null);
        }
    }
}
