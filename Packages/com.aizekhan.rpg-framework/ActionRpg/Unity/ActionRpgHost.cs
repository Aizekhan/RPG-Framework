using System;
using System.Collections.Generic;
using System.IO;
using RPGFramework.ActionRpg.Coop;
using RPGFramework.Bootstrap.Unity;
using RPGFramework.Cameras;
using RPGFramework.Core.Composition;
using RPGFramework.Diagnostics;
using RPGFramework.Diagnostics.Unity;
using RPGFramework.Events;
using RPGFramework.Logging;
using Cysharp.Threading.Tasks;
using RPGFramework.Networking;
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
    /// Co-op (F2, or <c>-host</c> / <c>-join &lt;code&gt;</c> on the command line): one player hosts, friends join (up to
    /// Max Players) on the same network, or over the internet when a session module such as Unity Relay is installed;
    /// the host's machine runs the rules and the others see and play its game.
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

        [Tooltip("Also save every this many seconds, so a crash loses little (0 = only on level-up, quests and quit)")]
        [SerializeField, Min(0f)] private float _autosaveSeconds = 60f;

        [Tooltip("In the Editor and development builds, F3 shows systems, events and frame times (off in release builds)")]
        [SerializeField] private bool _diagnostics = true;

        [Header("Co-op")]
        [Tooltip("Friends can host and join games (F2); the host's machine runs the rules")]
        [SerializeField] private bool _coop = true;

        [Tooltip("Most players in a hosted game, the host included (tested up to 8)")]
        [SerializeField, Range(2, CoopProtocol.MaxSupportedPlayers)] private int _maxPlayers = CoopProtocol.DefaultMaxPlayers;

        [Tooltip("UDP port of games hosted on the local network (the default session provider)")]
        [SerializeField, Range(1024, 65535)] private int _coopPort = CoopProtocol.DefaultPort;

        [Tooltip("Players' colours by party seat: the host, then each friend in the order they joined. Seats past the list get generated colours")]
        [SerializeField] private Color[] _partyColors = PlayerTagView.DefaultPalette();

        private const string PlayerNameKey = "RPGFramework.Coop.PlayerName";
        private string _playerName;

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
        private INetworkSessionProvider _sessions;
        private LanSessionProvider _lan;
        private List<INetworkSessionProvider> _providers;
        private INetworkTransport _transport;
        private int _generation;
        private bool _opening;
        private CoopHost _coopHost;
        private CoopClient _coopClient;
        private string _joinCode;
        private bool _restartPending;
        private float _sinceSave;

        /// <summary>
        /// Seconds without a word from the other side before a co-op link counts as lost (a crash, a pulled cable).
        /// </summary>
        public float ConnectionTimeout { get; set; } = CoopProtocol.ConnectionTimeout;

        /// <summary>
        /// Times this machine tries to get back into a friend's game after the link was lost.
        /// </summary>
        public const int ReconnectAttempts = 3;

        private int _reconnectAttempt;
        // The host's token for this player's seat, and the game it is for
        private string _rejoinToken = "";
        private string _rejoinCode;

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
        /// How games are opened and found: the local network by default (<see cref="LanSessionProvider"/>). Set another
        /// provider (a relay service, a platform's lobbies) before hosting or joining; the co-op itself stays the same.
        /// </summary>
        public INetworkSessionProvider SessionProvider
        {
            get => _sessions ?? Lan;
            set => _sessions = value;
        }

        /// <summary>
        /// The ways to play together this game offers (F2 lets the player pick): the local network, then one for each
        /// installed session module (<see cref="NetworkSessionProviders"/>), e.g. Unity Relay once the Multiplayer
        /// Services SDK is in the project. No game code is needed for them to appear.
        /// </summary>
        public IReadOnlyList<INetworkSessionProvider> SessionProviders
        {
            get
            {
                if (_providers == null)
                {
                    _providers = new List<INetworkSessionProvider> { Lan };
                    foreach (var entry in NetworkSessionProviders.Available)
                        _providers.Add(entry.Create());
                }
                if (_sessions != null && !_providers.Contains(_sessions))
                    _providers.Add(_sessions);
                return _providers;
            }
        }

        /// <summary>
        /// Picks how to play together by id: "lan", or a module's id such as "unity-relay" (also <c>-provider &lt;id&gt;</c>
        /// on the command line).
        /// </summary>
        /// <returns>False when no such provider is installed (the choice stays as it was).</returns>
        public bool UseSessionProvider(string id)
        {
            if (string.Equals(id, "lan", StringComparison.OrdinalIgnoreCase))
            {
                SessionProvider = Lan;
                return true;
            }
            var available = NetworkSessionProviders.Available;
            for (int i = 0; i < available.Count; i++)
            {
                if (string.Equals(available[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    SessionProvider = SessionProviders[i + 1];
                    return true;
                }
            }
            return false;
        }

        private LanSessionProvider Lan => _lan ?? (_lan = new LanSessionProvider(_coopPort));

        /// <summary>
        /// Most players in the games this machine hosts, the host included (2 to <see cref="CoopProtocol.MaxSupportedPlayers"/>).
        /// Applies to the next hosted game.
        /// </summary>
        public int MaxPlayers
        {
            get => Mathf.Clamp(_maxPlayers, 2, CoopProtocol.MaxSupportedPlayers);
            set => _maxPlayers = Mathf.Clamp(value, 2, CoopProtocol.MaxSupportedPlayers);
        }

        /// <summary>
        /// What friends type to join while hosting (empty otherwise).
        /// </summary>
        public string JoinCode { get; private set; } = "";

        /// <summary>
        /// The latest co-op news for the player ("The host left the game.").
        /// </summary>
        public string CoopStatus { get; private set; } = "";

        /// <summary>
        /// The name others see in co-op. Remembered on this machine; starts as the computer's user name.
        /// Changes take effect the next time this player hosts or joins.
        /// </summary>
        public string PlayerName
        {
            get
            {
                if (_playerName == null)
                {
                    string saved = PlayerPrefs.GetString(PlayerNameKey, "");
                    _playerName = ActorWorld.CleanName(saved) ?? ActorWorld.CleanName(Environment.UserName) ?? "Player";
                }
                return _playerName;
            }
            set
            {
                UsePlayerName(value);
                PlayerPrefs.SetString(PlayerNameKey, _playerName);
            }
        }

        /// <summary>
        /// Sets the co-op name for this run only, without remembering it (tests, several games on one machine).
        /// </summary>
        public void UsePlayerName(string name) => _playerName = ActorWorld.CleanName(name) ?? "Player";

        /// <summary>
        /// A party seat's colour (<see cref="Actor.Seat"/>), from 'Party Colors'.
        /// </summary>
        public Color ColorOfSeat(int seat) => PlayerTagView.ColorOf(seat, _partyColors);

        /// <summary>
        /// The co-op role is changing: the game restarts at the next frame.
        /// </summary>
        public bool IsSwitching => _restartPending || _opening || (_joinCode != null && _coopClient == null);

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
        public void UseCoopPort(int port)
        {
            _coopPort = port;
            if (_lan != null)
                _lan.Port = port;
        }

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
            if (_joinCode != null)
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
        /// Opens this game for friends to join, through <see cref="SessionProvider"/>. When it is open,
        /// <see cref="Role"/> is Hosting and <see cref="JoinCode"/> says what friends type; otherwise
        /// <see cref="CoopStatus"/> says why not.
        /// </summary>
        /// <returns>False when it cannot start (not playing alone, or already opening).</returns>
        public bool HostGame()
        {
            if (!_coop || Role != CoopRole.Solo || World == null || _presenter == null || IsSwitching)
                return false;
            _opening = true;
            CoopStatus = "Opening the game...";
            HostAsync(_generation).Forget();
            return true;
        }

        private async UniTaskVoid HostAsync(int generation)
        {
            HostedSession session;
            try
            {
                session = await SessionProvider.HostAsync(new SessionOptions { MaxPlayers = MaxPlayers, Name = PlayerName });
            }
            catch (NetworkSessionException exception)
            {
                _opening = false;
                CoopStatus = exception.Message;
                return;
            }
            _opening = false;
            if (this == null || generation != _generation || World == null)
            {
                session.Transport.Disconnect();
                (session.Transport as IDisposable)?.Dispose();
                return;
            }

            _transport = session.Transport;
            _coopHost = new CoopHost(World, Host.Container.Resolve<IEventBus>(), CoopProtocol.CreateNetwork(_transport, Host.Logger, ConnectionTimeout),
                _presenter, CoopProtocol.Fingerprint(_settings, _content), Host.Logger)
            {
                // A friend's strike is checked against where this machine sees them, with slack for lag
                MaxAttackDistance = _setup.Player.AttackRange + 1.5f,
                MaxPlayers = MaxPlayers
            };
            World.SetPlayerName(World.PlayerId, PlayerName);
            _coopHost.Open();
            JoinCode = session.JoinCode;
            CoopStatus = $"Hosting: friends join with {JoinCode}";
            Host.Logger.LogInfo(CoopStatus, Category);
        }

        /// <summary>
        /// Leaves this game and joins a friend's (it restarts at the next frame); their machine then runs the rules.
        /// </summary>
        /// <param name="joinCode">What the host's panel shows: with the default provider their IP address
        /// (192.168.1.20, or 192.168.1.20:7790).</param>
        public void JoinGame(string joinCode)
        {
            if (!_coop || string.IsNullOrWhiteSpace(joinCode))
                return;
            _joinCode = joinCode.Trim();
            _reconnectAttempt = 0;
            CoopStatus = $"Joining {_joinCode}...";
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
            else if (Role == CoopRole.Joined || _joinCode != null)
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
            Autosave(Time.unscaledDeltaTime);
        }

        private void Autosave(float deltaTime)
        {
            if (_saves == null || _autosaveSeconds <= 0f)
                return;
            _sinceSave += deltaTime;
            if (_sinceSave < _autosaveSeconds)
                return;
            _sinceSave = 0f;
            SaveProgress();
        }

        private void StartFromCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-port" && int.TryParse(args[i + 1], out int port))
                    UseCoopPort(port);
                else if (args[i] == "-provider" && !UseSessionProvider(args[i + 1]))
                    CoopStatus = $"'{args[i + 1]}' is not an installed way to play together; using {SessionProvider.DisplayName}.";
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host")
                    HostGame();
                else if (args[i] == "-join" && i + 1 < args.Length)
                    JoinGame(args[i + 1]);
            }
        }

        private void ShowGame()
        {
            if (Host?.Container == null || !enabled)
                return; // the start failed and was logged
            if (_joinCode != null)
                JoinAsync(_generation).Forget();
            else if (World != null)
                ShowWorld();
        }

        private async UniTaskVoid JoinAsync(int generation)
        {
            INetworkTransport transport;
            try
            {
                transport = await SessionProvider.JoinAsync(_joinCode);
            }
            catch (NetworkSessionException exception)
            {
                if (generation == _generation && !TryReconnect(exception.Message, CoopEndKind.Unreachable))
                    ReturnToSolo(exception.Message);
                return;
            }
            if (this == null || generation != _generation)
            {
                transport.Disconnect();
                (transport as IDisposable)?.Dispose();
                return;
            }

            _transport = transport;
            _coopClient = new CoopClient(_settings, _content, Host.Container.Resolve<IEventBus>(),
                CoopProtocol.CreateNetwork(_transport, Host.Logger, ConnectionTimeout), null, Host.Logger)
            {
                PlayerName = PlayerName,
                RejoinToken = _rejoinCode == _joinCode ? _rejoinToken : ""
            };
            _coopClient.Joined += OnJoined;
            _coopClient.Ended += OnClientEnded;
            ShowWorld();
            _coopClient.Motion = _presenter;
            _coopClient.JoinSession(_joinCode);
        }

        private void ShowWorld()
        {
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
            _presenter.PartyColors = _partyColors;
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
        }

        private void HideGame()
        {
            SaveProgress();
            _saveTriggers.Dispose();
            _saveTriggers = new SubscriptionBag();
            _saves = null;
            _generation++;
            _opening = false;
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
            if (_coopClient != null)
            {
                // Closing the connection here is a restart (or a leave already handled), not the player walking out
                _coopClient.Joined -= OnJoined;
                _coopClient.Ended -= OnClientEnded;
                _coopClient.Dispose();
                _coopClient = null;
            }
            (_transport as IDisposable)?.Dispose();
            _transport = null;
            JoinCode = "";
        }

        private void OnJoined()
        {
            _rejoinToken = _coopClient.RejoinToken;
            _rejoinCode = _joinCode;
            CoopStatus = _coopClient.Rejoined ? "Reconnected: back in your seat." : "";
            _reconnectAttempt = 0;
        }

        private void OnClientEnded(string reason)
        {
            if (!TryReconnect(reason, _coopClient.EndKind))
                ReturnToSolo(reason);
        }

        /// <summary>
        /// After a lost link, joins the same game again (the host keeps the seat a while); a failed try counts too.
        /// </summary>
        private bool TryReconnect(string reason, CoopEndKind kind)
        {
            bool lost = kind == CoopEndKind.ConnectionLost || (_reconnectAttempt > 0 && kind == CoopEndKind.Unreachable);
            if (!lost || _joinCode == null || _reconnectAttempt >= ReconnectAttempts)
                return false;
            _reconnectAttempt++;
            CoopStatus = $"{reason} Reconnecting ({_reconnectAttempt}/{ReconnectAttempts})...";
            Host.Logger.LogInfo(CoopStatus, Category);
            _restartPending = true;
            return true;
        }

        private void ReturnToSolo(string reason)
        {
            _reconnectAttempt = 0;
            CoopStatus = reason;
            if (_joinCode == null)
                return;
            _joinCode = null;
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
