using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.AssetManagement;
using RPGFramework.AssetManagement.Unity;
using RPGFramework.DI;
using RPGFramework.Pooling;
using RPGFramework.Scenes;
using RPGFramework.Spawning;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.Bootstrap.UnityServices.Tests
{
    public sealed class UnityServicesModuleTests
    {
        private sealed class TestAsset
        {
        }

        /// <summary>
        /// Hands out one asset synchronously and records releases.
        /// </summary>
        private sealed class FakeProvider : IAssetProvider
        {
            public readonly TestAsset Asset = new TestAsset();
            public readonly List<string> Releases = new List<string>();

            public string Name => "Fake";
            public int Priority => 10;

            public UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class =>
                UniTask.FromResult(key == "fake" ? Asset as T : null);

            public void Release(string key, object asset) => Releases.Add(key);
        }

        private sealed class FakeSceneBackend : ISceneBackend
        {
            public string ActiveScene => "Fake";
            public bool IsLoaded(string sceneName) => sceneName == ActiveScene;

            public UniTask LoadAsync(string sceneName, bool additive, IProgress<float> progress, CancellationToken cancellationToken) =>
                UniTask.CompletedTask;

            public UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken) => UniTask.CompletedTask;

            public void SetActive(string sceneName)
            {
            }
        }

        private readonly List<Object> _cleanup = new List<Object>();
        private GameHost _host;

        [TearDown]
        public void TearDown()
        {
            _host?.Dispose();
            _host = null;
            foreach (var obj in _cleanup)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private T Resolve<T>() => _host.Container.Resolve<T>();

        private void Start(UnityServicesOptions options = null)
        {
            _host = new GameHost();
            _host.Start(builder => builder.UseModule(new UnityServicesModule(options)));
        }

        [Test]
        public void Host_ResolvesEveryService()
        {
            Start();

            Assert.That(Resolve<IPoolManager>(), Is.InstanceOf<PoolManager>());
            Assert.That(Resolve<IAssetManager>(), Is.InstanceOf<AssetManager>());
            Assert.That(Resolve<IPrefabSpawner>(), Is.InstanceOf<PrefabSpawner>());
            Assert.That(Resolve<ISceneDispatcher>(), Is.InstanceOf<SceneDispatcher>());
            Assert.That(Resolve<IAssetManager>().Providers.Single(), Is.InstanceOf<ResourcesAssetProvider>());
        }

        [Test]
        public void Services_AreSharedSingletons_AndTheSpawnerUsesTheSharedPools()
        {
            Start();

            Assert.That(Resolve<IPoolManager>(), Is.SameAs(Resolve<IPoolManager>()));
            Assert.That(Resolve<IAssetManager>(), Is.SameAs(Resolve<IAssetManager>()));
            Assert.That(Resolve<IPrefabSpawner>(), Is.SameAs(Resolve<IPrefabSpawner>()));
            Assert.That(Resolve<ISceneDispatcher>(), Is.SameAs(Resolve<ISceneDispatcher>()));

            var prefab = new GameObject("Goblin");
            _cleanup.Add(prefab);
            var spawner = Resolve<IPrefabSpawner>();
            spawner.Register("goblin", prefab);
            spawner.ConfigurePool("goblin", initialSize: 2);
            var prewarm = spawner.PrewarmAsync("goblin");
            Assert.That(prewarm.Status, Is.EqualTo(UniTaskStatus.Succeeded));

            var pools = Resolve<IPoolManager>();
            Assert.That(pools.Keys.Single(), Does.Contain("goblin"));
            Assert.That(pools.GetAllStatistics().Single().InactiveCount, Is.EqualTo(2));
        }

        [Test]
        public void Options_AddProviders_AndReplaceTheSceneBackend()
        {
            var provider = new FakeProvider();
            var options = new UnityServicesOptions { UseResources = false, SceneBackend = () => new FakeSceneBackend() }
                .AddAssetProvider(() => provider);
            Start(options);

            var assets = Resolve<IAssetManager>();
            Assert.That(assets.Providers, Is.EqualTo(new IAssetProvider[] { provider }));
            Assert.That(Resolve<ISceneDispatcher>().ActiveScene, Is.EqualTo("Fake"));
        }

        [Test]
        public void Shutdown_ReleasesPoolsAndAssets_AndDisposesTheSpawner()
        {
            var provider = new FakeProvider();
            Start(new UnityServicesOptions().AddAssetProvider(() => provider));

            var assets = Resolve<IAssetManager>();
            var load = assets.LoadAsync<TestAsset>("fake");
            Assert.That(load.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(load.GetAwaiter().GetResult().Asset, Is.SameAs(provider.Asset));

            var prefab = new GameObject("Arrow");
            _cleanup.Add(prefab);
            var spawner = Resolve<IPrefabSpawner>();
            spawner.Register("arrow", prefab);
            spawner.ConfigurePool("arrow");
            spawner.PrewarmAsync("arrow").GetAwaiter().GetResult();
            var pools = Resolve<IPoolManager>();

            _host.Dispose();

            Assert.That(pools.Keys, Is.Empty, "the spawner gave its pool back");
            Assert.That(assets.IsLoaded("fake"), Is.False);
            Assert.That(provider.Releases, Is.EqualTo(new[] { "fake" }));
            Assert.Throws<ObjectDisposedException>(() => spawner.PrewarmAsync("arrow").GetAwaiter().GetResult());
        }

        [Test]
        public void AServiceRegisteredFirst_IsKept_AndNotReleasedByTheModule()
        {
            var own = new PoolManager();
            var container = new DIContainer();
            container.RegisterInstance<IPoolManager>(own);
            _host = new GameHost();
            _host.Start(builder => builder.UseModule(new UnityServicesModule()), container);

            Assert.That(Resolve<IPoolManager>(), Is.SameAs(own));
            var prefab = new GameObject("Bolt");
            _cleanup.Add(prefab);
            var spawner = Resolve<IPrefabSpawner>();
            spawner.Register("bolt", prefab);
            spawner.ConfigurePool("bolt");
            spawner.PrewarmAsync("bolt").GetAwaiter().GetResult();
            Assert.That(own.Keys, Has.Count.EqualTo(1), "the spawner registers its pools in the game's pool manager");
        }
    }
}
