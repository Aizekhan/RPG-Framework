using System;
using System.Collections.Generic;
using RPGFramework.AssetManagement;
using RPGFramework.AssetManagement.Unity;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Pooling;
using RPGFramework.Pooling.Unity;
using RPGFramework.Scenes;
using RPGFramework.Scenes.Unity;
using RPGFramework.Spawning;

namespace RPGFramework.Bootstrap.UnityServices
{
    public sealed class UnityServicesOptions
    {
        private readonly List<Func<IAssetProvider>> _assetProviders = new List<Func<IAssetProvider>>();

        /// <summary>
        /// Loads assets from <c>Resources/</c> folders (on by default).
        /// </summary>
        public bool UseResources { get; set; } = true;

        /// <summary>
        /// Priority of the Resources provider; providers with a higher priority are asked first.
        /// </summary>
        public int ResourcesPriority { get; set; }

        /// <summary>
        /// Engine behind <see cref="ISceneDispatcher"/>; null uses Unity's SceneManager (<see cref="UnitySceneBackend"/>).
        /// </summary>
        public Func<ISceneBackend> SceneBackend { get; set; }

        public IReadOnlyList<Func<IAssetProvider>> AssetProviders => _assetProviders;

        /// <summary>
        /// Adds an asset source, created when the asset manager is first used (once per game start), e.g.
        /// <c>options.AddAssetProvider(() => new AddressablesAssetProvider())</c>.
        /// </summary>
        public UnityServicesOptions AddAssetProvider(Func<IAssetProvider> create)
        {
            _assetProviders.Add(create ?? throw new ArgumentNullException(nameof(create)));
            return this;
        }
    }

    /// <summary>
    /// Registers the framework's Unity services, shared by the whole game:
    /// <see cref="IPoolManager"/>, <see cref="IAssetManager"/> (Resources plus any providers in the options),
    /// <see cref="IPrefabSpawner"/> (loads through that asset manager, registers its pools in that pool manager)
    /// and <see cref="ISceneDispatcher"/> (Unity's SceneManager, events on the bus). Pools registered with a scene name
    /// as owner (<c>pools.CreateGameObjectPool(key, prefab, owner: sceneName)</c>) are removed when that scene unloads.
    /// A service registered before this module configures is kept (a game or a test can replace one). On shutdown the
    /// spawner releases its pools, the remaining pools are cleared and every loaded asset is unloaded.
    /// </summary>
    public sealed class UnityServicesModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.UnityServices";

        private readonly UnityServicesOptions _options;
        private PoolManager _pools;
        private ScenePoolCleanup _sceneCleanup;
        private AssetManager _assets;
        private PrefabSpawner _spawner;

        public UnityServicesModule() : this(null)
        {
        }

        public UnityServicesModule(UnityServicesOptions options)
        {
            _options = options ?? new UnityServicesOptions();
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            // Each service is created on first use; the module remembers the ones it made so shutdown releases only those
            if (!Container.IsRegistered<IPoolManager>())
            {
                Container.RegisterFactory<IPoolManager>(c =>
                {
                    _pools = new PoolManager(c.Resolve<ILogger>());
                    // Pools registered with a scene name as owner are removed when that scene unloads
                    _sceneCleanup = new ScenePoolCleanup(_pools);
                    return _pools;
                });
            }

            if (!Container.IsRegistered<IAssetManager>())
                Container.RegisterFactory<IAssetManager>(c => _assets = CreateAssetManager(c.Resolve<ILogger>()));

            if (!Container.IsRegistered<IPrefabSpawner>())
            {
                Container.RegisterFactory<IPrefabSpawner>(c => _spawner = new PrefabSpawner(
                    c.Resolve<IAssetManager>(), c.Resolve<IPoolManager>(), c.Resolve<ILogger>()));
            }

            if (!Container.IsRegistered<ISceneDispatcher>())
            {
                Container.RegisterFactory<ISceneDispatcher>(c => new SceneDispatcher(
                    _options.SceneBackend?.Invoke() ?? new UnitySceneBackend(), c.Resolve<IEventPublisher>(), c.Resolve<ILogger>()));
            }
        }

        protected override void OnShutdown()
        {
            // Spawner first: it destroys its instances and gives its prefabs back before the assets are unloaded
            _spawner?.Dispose();
            _sceneCleanup?.Dispose();
            _pools?.ClearAll();
            _assets?.UnloadAll();
            _spawner = null;
            _pools = null;
            _sceneCleanup = null;
            _assets = null;
        }

        private AssetManager CreateAssetManager(ILogger logger)
        {
            var assets = new AssetManager(logger);
            if (_options.UseResources)
                assets.AddProvider(new ResourcesAssetProvider(_options.ResourcesPriority));
            foreach (var create in _options.AssetProviders)
                assets.AddProvider(create());
            return assets;
        }
    }
}
