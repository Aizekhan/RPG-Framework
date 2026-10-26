using System;
using System.IO;
using RPGFramework.ActionRpg.Coop;
using RPGFramework.Bootstrap.Unity;
using RPGFramework.Cameras;
using RPGFramework.Core.Composition;
using RPGFramework.Diagnostics;
using RPGFramework.Diagnostics.Unity;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Networking.UnityTransport;
using RPGFramework.Presentation;
using RPGFramework.Progression;
using RPGFramework.Quests;
using RPGFramework.Spawning;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    public enum CoopRole
    {
        /// <summary>Playing alone.</summary>
        Solo,

        /// <summary>Playing, with the game open for friends to join.</summary>
        Hosting,

        /// <summary>Playing in a friend's game: their machine runs the rules.</summary>
        Joined
    }

    /// <summary>
    /// Put this on one GameObject in the scene, drag a <see cref="GameSetup"/> into it and press Play: the player
    /// spawns here, enemies come in waves, the camera follows and a HUD shows health, quests and loot.
    /// Derive from it and override <see cref="ConfigureGameModules"/> to add rules of your own.
    /// Co-op (F2, or <c>-host</c> / <c>-join &lt;ip&gt;</c> on the command line): one player hosts, up to three friends
    /// on the same network join; the host's machine runs the rules and the others see and play its game.
    /// </summary>
    public class ActionRpgHost : GameHostBehaviour
    {
        private const string Category = "ActionRpg";

        [Tooltip("The game: characters, waves, items, loot, quests")]
        [SerializeField] private GameSetup _setup;

        [Tooltip("Follows the player; empty = the main camera, which gets a CameraFollow if it has none")]
        [SerializeField] private CameraFollow _camera;

        [SerializeField] private bool _showHud = true;

        [Tooltip("Keep the player's level, backpack and quests between sessions (saved on level-up, finished quests and quit)")]
        [SerializeField] private bool _saveProgress;

        [Tooltip("In the Editor and development builds, F3 shows systems, events and frame times (off in release builds)")]
        [SerializeField] private bool _diagnostics = true;

        [Header("Co-op")]
        [Tooltip("Friends can host and join games (F2): up to 4 players, the host's machine runs the rules")]
        [SerializeField] private bool _coop = true;

        [SerializeField, Range(1024, 65535)] private int _coopPort = CoopProtocol.DefaultPort;

        private ActionRpgModule _module;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private PrefabSpawner _spawner;
        private ActorPresenter _presenter;
        private ActionRpgSaves _saves;
        private SubscriptionBag _saveTriggers = new SubscriptionBag();
        private DiagnosticsOverlay _overlay;
        private ActionRpgHud _hud;
        private Transform _charactersRoot;
        private UnityNetworkTransport _transport;
        private CoopHost _coopHost;
        private CoopClient _coopClient;
        private string _joinAddress;
        private int _joinPort;
        private bool _restartPending;

        /// <summary>
        /// The F3 diagnostics panel (null when diagnostics are off or in a release build).
        /// </summary>
        public DiagnosticsOverlay Diagnostics => _overlay;

        private bool DiagnosticsEnabled => _diagnostics && Debug.isDebugBuild;

        public GameSetup Setup => _setup;

        /// <summary>
        /// The game this machine runs (null when the start failed, and while joined to a friend's game).
        /// </summary>
        public ActorWorld World => _module?.World;

        /// <summary>
        /// The game on screen: <see cref="World"/>, or the friend's game while joined.
        /// </summary>
        public IActorWorld CurrentWorld => _coopClient != null ? _coopClient.World : (IActorWorld)World;

        /// <summary>
        /// Views of the characters (null until the game is shown).
        /// </summary>
        public IEntityViewRegistry Views => _presenter?.Views;

        /// <summary>
        /// The player's saved progress (null when 'Save Progress' is off, the game did not start, or while joined).
        /// </summary>
        public ActionRpgSaves Saves => _saves;

        /// <summary>
        /// Where this game's progress is saved: a folder per Game Setup under the persistent data path.
        /// </summary>
        public string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves", _setup != null ? _setup.name : "Game");

        public CoopRole Role => _coopClient != null ? CoopRole.Joined : _coopHost != null ? CoopRole.Hosting : CoopRole.Solo;

        /// <summary>
        /// Friends can host and join (the 'Co-op' setting).
        /// </summary>
        public bool CoopEnabled => _coop;

        /// <summary>
        /// The open game while hosting (null otherwise).
        /// </summary>
        public CoopHost CoopHost => _coopHost;

        /// <summary>
        /// The connection to the friend's game while joined (null otherwise).
        /// </summary>
        public CoopClient CoopClient => _coopClient;

        /// <summary>
        /// The port friends join at while hosting (0 otherwise).
        /// </summary>
        public int HostingPort => _coopHost != null && _transport != null ? _transport.ListeningPort : 0;

        /// <summary>
        /// The latest co-op news for the player ("The host left the game.").
        /// </summary>
        public string CoopStatus { get; private set; } = "";

        /// <summary>
        /// The co-op role is changing: the game restarts at the next frame.
        /// </summary>
        public bool IsSwitching => _restartPending;

        /// <summary>
        /// Raised when a character's view is in the scene.
        /// </summary>
        public event Action<int, GameObject> ViewShown;

        /// <summary>
        /// Sets the setup before the host starts (tests, code-built scenes). Call on an inactive GameObject.
        /// </summary>
        public void UseSetup(GameSetup setup) => _setup = setup;

        /// <summary>
        /// Turns progress saving on or off before the host starts. Call on an inactive GameObject.
        /// </summary>
        public void UseSaving(bool saveProgress) => _saveProgress = saveProgress;

        /// <summary>
        /// Sets the co-op port before the host starts (tests, several games on one machine).
        /// </summary>
        public void UseCoopPort(int port) => _coopPort = port;

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

            _settings = _setup.BuildSettings();
            _content = _setup.BuildContent();
            if (DiagnosticsEnabled)
                builder.UseModule(new DiagnosticsModule());
            if (_joinAddress != null)
            {
                // Joined: the friend's machine runs the rules, this one shows them
                _module = null;
                return;
            }

            _module = ActionRpgComposition.Configure(builder, _settings, _content);
            ConfigureGameModules(builder);
        }

        /// <summary>
        /// Add your own <c>GameFeatureModule</c>s here; they can resolve <see cref="ActorWorld"/> and subscribe to its events.
        /// They run where the rules run: alone or hosting, not on a machine that joined a friend's game.
        /// </summary>
        protected virtual void ConfigureGameModules(IGameCompositionBuilder builder)
        {
        }

        protected virtual void Start()
        {
            ShowGame();
            if (_coop)
                StartFromCommandLine(Environment.GetCommandLineArgs());
        }

        /// <summary>
        /// Opens this game for friends to join on the local network.
        /// </summary>
        /// <param name="port">0 = the 'Coop Port' setting.</param>
        /// <returns>False when it could not (not playing alone, or the port is taken; see <see cref="CoopStatus"/>).</returns>
        public bool HostGame(int port = 0)
        {
            if (!_coop || Role != CoopRole.Solo || World == null || _presenter == null || _restartPending)
                return false;
            port = port > 0 ? port : _coopPort;
            _transport = new UnityNetworkTransport();
            _coopHost = new CoopHost(World, Host.Container.Resolve<IEventBus>(), CoopProtocol.CreateNetwork(_transport, Host.Logger),
                _presenter, CoopProtocol.Fingerprint(_settings, _content), Host.Logger)
            {
                // A friend's strike is checked against where this machine sees them, with slack for lag
                MaxAttackDistance = _setup.Player.AttackRange + 1.5f
            };
            try
            {
                _coopHost.Open(port);
            }
            catch (InvalidOperationException exception)
            {
                StopNetwork();
                CoopStatus = exception.Message;
                return false;
            }
            CoopStatus = $"Hosting on port {HostingPort}";
            Host.Logger.LogInfo(CoopStatus, Category);
            return true;
        }

        /// <summary>
        /// Leaves this game and joins a friend's (at the next frame); their machine then runs the rules.
        /// </summary>
        /// <param name="address">The host's IP address, e.g. 192.168.1.20.</param>
        /// <param name="port">0 = the 'Coop Port' setting.</param>
        public void JoinGame(string address, int port = 0)
        {
            if (!_coop || string.IsNullOrWhiteSpace(address))
                return;
            _joinAddress = address.Trim();
            _joinPort = port > 0 ? port : _coopPort;
            CoopStatus = $"Joining {_joinAddress}...";
            _restartPending = true;
        }

        /// <summary>
        /// Stops hosting (friends are dropped), or leaves the friend's game and plays alone again.
        /// </summary>
        public void LeaveGame()
        {
            if (Role == CoopRole.Hosting)
            {
                StopNetwork();
                CoopStatus = "Stopped hosting.";
            }
            else if (Role == CoopRole.Joined || _joinAddress != null)
            {
                ReturnToSolo("You left the game.");
            }
        }

        protected override void Update()
        {
            if (_restartPending)
            {
                _restartPending = false;
                HideGame();
                Restart();
                ShowGame();
                return;
            }
            base.Update();
            _coopHost?.Update(Time.unscaledDeltaTime);
            _coopClient?.Update(Time.unscaledDeltaTime);
        }

        private void StartFromCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host")
                    HostGame(i + 1 < args.Length && int.TryParse(args[i + 1], out int port) ? port : 0);
                else if (args[i] == "-join" && i + 1 < args.Length)
                    JoinGame(args[i + 1], i + 2 < args.Length && int.TryParse(args[i + 2], out int port) ? port : 0);
            }
        }

        private void ShowGame()
        {
            if (Host?.Container == null || !enabled)
                return; // the start failed and was logged
            if (_joinAddress != null)
            {
                _transport = new UnityNetworkTransport();
                _coopClient = new CoopClient(_settings, _content, Host.Container.Resolve<IEventBus>(),
                    CoopProtocol.CreateNetwork(_transport, Host.Logger), null, Host.Logger);
                _coopClient.Ended += ReturnToSolo;
            }
            else if (World == null)
            {
                return;
            }

            if (_saveProgress && World != null)
                StartSaving();

            if (_charactersRoot == null)
            {
                _charactersRoot = new GameObject("Characters").transform;
                _charactersRoot.SetParent(transform, false);
            }

            _spawner = new PrefabSpawner(logger: Host.Logger);
            _presenter = new ActorPresenter(CurrentWorld, _setup, Host.Container.Resolve<IEventBus>(), _spawner,
                new EntityViewRegistry(), _charactersRoot, Host.Logger, enemiesDrivenElsewhere: _coopClient != null);
            _presenter.ViewShown += OnViewShown;
            _presenter.Start();

            if (DiagnosticsEnabled)
                StartDiagnostics();

            if (_showHud)
            {
                if (_hud == null)
                    _hud = gameObject.AddComponent<ActionRpgHud>();
                _hud.Bind(CurrentWorld, _setup.IconOf);
            }

            if (_coop && GetComponent<CoopPanel>() == null)
                gameObject.AddComponent<CoopPanel>().Bind(this);

            if (_coopClient != null)
            {
                _coopClient.Motion = _presenter;
                try
                {
                    _coopClient.Join(_joinAddress, _joinPort);
                }
                catch (ArgumentException exception)
                {
                    ReturnToSolo(exception.Message);
                }
            }
        }

        private void HideGame()
        {
            SaveProgress();
            _saveTriggers.Dispose();
            _saveTriggers = new SubscriptionBag();
            _saves = null;
            StopNetwork();
            _presenter?.Dispose();
            _presenter = null;
            _spawner?.Dispose();
            _spawner = null;
        }

        private void StopNetwork()
        {
            _coopHost?.Dispose();
            _coopHost = null;
            _coopClient?.Dispose();
            _coopClient = null;
            _transport?.Dispose();
            _transport = null;
        }

        private void ReturnToSolo(string reason)
        {
            CoopStatus = reason;
            if (_joinAddress == null)
                return;
            _joinAddress = null;
            _restartPending = true;
            Host.Logger.LogInfo($"Back to playing alone: {reason}", Category);
        }

        private void StartDiagnostics()
        {
            if (_overlay == null)
            {
                _overlay = gameObject.AddComponent<DiagnosticsOverlay>();
                _overlay.Visible = false;
            }
            _overlay.Hub = Host.Container.Resolve<DiagnosticsHub>();
        }

        private void StartSaving()
        {
            _saves = new ActionRpgSaves(World, SaveDirectory, Application.version);
            string error = _saves.Load();
            if (error != null)
                Host.Logger.LogError($"Saved progress could not be loaded: {error}", Category);

            var bus = Host.Container.Resolve<IEventBus>();
            bus.Subscribe<LevelUpEvent>(_ => SaveProgress()).AddTo(_saveTriggers);
            bus.Subscribe<QuestCompletedEvent>(_ => SaveProgress()).AddTo(_saveTriggers);
        }

        /// <summary>
        /// Saves now (does nothing when 'Save Progress' is off or while joined). Returns false and logs when the save failed.
        /// </summary>
        public bool SaveProgress()
        {
            if (_saves == null || World == null || World.IsRestoring)
                return false;
            string error = _saves.Save();
            if (error == null)
                return true;
            Host.Logger.LogError($"Progress was not saved: {error}", Category);
            return false;
        }

        [ContextMenu("Delete Saved Progress")]
        private void DeleteSavedProgress()
        {
            if (Directory.Exists(SaveDirectory))
                Directory.Delete(SaveDirectory, true);
            Debug.Log($"Saved progress deleted: {SaveDirectory}", this);
        }

        private void OnViewShown(int entityId, GameObject view)
        {
            if (entityId == CurrentWorld?.PlayerId)
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
            // Save, leave and drop views first: they read the world, which the host shuts down
            HideGame();
            base.OnDestroy();
        }
    }
}
