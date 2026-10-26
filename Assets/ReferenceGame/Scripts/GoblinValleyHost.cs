using RPGFramework.Bootstrap.Unity;
using RPGFramework.Cameras;
using RPGFramework.Core.Composition;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Presentation;
using RPGFramework.Spawning;
using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// Scene entry point of Goblin Valley: starts the framework with the game's modules, then shows the game:
    /// combatant views from the config's prefab catalog, the camera on the hero, the HUD.
    /// </summary>
    public sealed class GoblinValleyHost : GameHostBehaviour
    {
        [Tooltip("Rules and prefabs; Assets/ReferenceGame/Config/GoblinValleyConfig")]
        [SerializeField] private GoblinValleyConfig _config;

        [Tooltip("Follows the hero")]
        [SerializeField] private CameraFollow _camera;

        private GoblinValleyModule _module;
        private PrefabSpawner _spawner;
        private GoblinValleyPresenter _presenter;
        private Transform _viewRoot;

        public GoblinValleyGame Game => _module?.Game;

        /// <summary>
        /// Views of the combatants (null until the game is shown).
        /// </summary>
        public IEntityViewRegistry Views => _presenter?.Views;

        protected override void ConfigureModules(IGameCompositionBuilder builder)
        {
            _module = GoblinValleyComposition.Configure(builder, _config != null ? _config.Rules : new GoblinValleyOptions());
        }

        private void Start()
        {
            if (Game == null)
                return; // the start failed and was logged
            if (_config == null || _config.Prefabs == null)
            {
                Host.Logger.LogError("GoblinValleyHost needs a config with a prefab catalog", "GoblinValley");
                return;
            }

            _viewRoot = new GameObject("Combatants").transform;
            _viewRoot.SetParent(transform, false);

            _spawner = new PrefabSpawner(logger: Host.Logger);
            _config.Prefabs.ApplyTo(_spawner);

            _presenter = new GoblinValleyPresenter(Game, Host.Container.Resolve<IEventBus>(), _spawner,
                new EntityViewRegistry(), _viewRoot, _config.SpawnRadius, Host.Logger);
            _presenter.ViewShown += OnViewShown;
            _presenter.Start();

            gameObject.AddComponent<GoblinValleyHud>().Bind(Game);
        }

        private void OnViewShown(int entityId, GameObject view)
        {
            if (entityId != Game.PlayerId || _camera == null)
                return;
            _camera.SetTarget(view.transform);
            _camera.transform.position = view.transform.position + _camera.Offset;
        }

        protected override void OnDestroy()
        {
            // Views first: they read the game, which the host shuts down
            _presenter?.Dispose();
            _spawner?.Dispose();
            base.OnDestroy();
        }
    }
}
