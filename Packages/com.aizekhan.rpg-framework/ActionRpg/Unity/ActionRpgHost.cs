using System;
using RPGFramework.Bootstrap.Unity;
using RPGFramework.Cameras;
using RPGFramework.Core.Composition;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Presentation;
using RPGFramework.Spawning;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Put this on one GameObject in the scene, drag a <see cref="GameSetup"/> into it and press Play: the player
    /// spawns here, enemies come in waves, the camera follows and a HUD shows health, quests and loot.
    /// Derive from it and override <see cref="ConfigureGameModules"/> to add rules of your own.
    /// </summary>
    public class ActionRpgHost : GameHostBehaviour
    {
        private const string Category = "ActionRpg";

        [Tooltip("The game: characters, waves, items, loot, quests")]
        [SerializeField] private GameSetup _setup;

        [Tooltip("Follows the player; empty = the main camera, which gets a CameraFollow if it has none")]
        [SerializeField] private CameraFollow _camera;

        [SerializeField] private bool _showHud = true;

        private ActionRpgModule _module;
        private PrefabSpawner _spawner;
        private ActorPresenter _presenter;

        public GameSetup Setup => _setup;

        /// <summary>
        /// The running game (null when the start failed).
        /// </summary>
        public ActorWorld World => _module?.World;

        /// <summary>
        /// Views of the characters (null until the game is shown).
        /// </summary>
        public IEntityViewRegistry Views => _presenter?.Views;

        /// <summary>
        /// Raised when a character's view is in the scene.
        /// </summary>
        public event Action<int, GameObject> ViewShown;

        /// <summary>
        /// Sets the setup before the host starts (tests, code-built scenes). Call on an inactive GameObject.
        /// </summary>
        public void UseSetup(GameSetup setup) => _setup = setup;

        protected sealed override void ConfigureModules(IGameCompositionBuilder builder)
        {
            if (_setup == null)
            {
                Debug.LogError("ActionRpgHost: drag a Game Setup asset into the 'Setup' field", this);
                throw new InvalidOperationException("ActionRpgHost has no Game Setup");
            }

            var problems = _setup.Validate();
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                    Debug.LogError($"Game Setup '{_setup.name}': {problem}", _setup);
                throw new InvalidOperationException($"Game Setup '{_setup.name}' has {problems.Count} problem(s)");
            }

            _module = ActionRpgComposition.Configure(builder, _setup.BuildSettings(), _setup.BuildContent());
            ConfigureGameModules(builder);
        }

        /// <summary>
        /// Add your own <c>GameFeatureModule</c>s here; they can resolve <see cref="ActorWorld"/> and subscribe to its events.
        /// </summary>
        protected virtual void ConfigureGameModules(IGameCompositionBuilder builder)
        {
        }

        protected virtual void Start()
        {
            if (World == null)
                return; // the start failed and was logged

            var root = new GameObject("Characters").transform;
            root.SetParent(transform, false);

            _spawner = new PrefabSpawner(logger: Host.Logger);
            _presenter = new ActorPresenter(World, _setup, Host.Container.Resolve<IEventBus>(), _spawner,
                new EntityViewRegistry(), root, Host.Logger);
            _presenter.ViewShown += OnViewShown;
            _presenter.Start();

            if (_showHud)
                gameObject.AddComponent<ActionRpgHud>().Bind(World);
        }

        private void OnViewShown(int entityId, GameObject view)
        {
            if (entityId == World.PlayerId)
                FollowWithCamera(view.transform);
            ViewShown?.Invoke(entityId, view);
        }

        private void FollowWithCamera(Transform player)
        {
            if (_camera == null)
            {
                var main = Camera.main;
                if (main == null)
                {
                    Host.Logger.LogWarning("No main camera to follow the player", Category);
                    return;
                }
                _camera = main.GetComponent<CameraFollow>();
                if (_camera == null)
                {
                    _camera = main.gameObject.AddComponent<CameraFollow>();
                    _camera.Offset = _setup.CameraOffset;
                }
            }
            _camera.SetTarget(player);
            _camera.transform.position = player.position + _camera.Offset;
            _camera.transform.LookAt(player.position + Vector3.up);
        }

        protected override void OnDestroy()
        {
            // Views first: they read the world, which the host shuts down
            _presenter?.Dispose();
            _spawner?.Dispose();
            base.OnDestroy();
        }
    }
}
