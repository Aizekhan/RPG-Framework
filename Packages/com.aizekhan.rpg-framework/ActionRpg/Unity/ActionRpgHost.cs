using System;
using System.Collections.Generic;
using System.IO;
using RPGFramework.ActionRpg.Coop;
using RPGFramework.Audio;
using RPGFramework.Audio.Unity;
using RPGFramework.Bootstrap.UnityServices;
using RPGFramework.Bootstrap.Unity;
using RPGFramework.Cameras;
using RPGFramework.Core.Composition;
using RPGFramework.Diagnostics;
using RPGFramework.Diagnostics.Unity;
using RPGFramework.Events;
using RPGFramework.Interaction;
using RPGFramework.Logging;
using Cysharp.Threading.Tasks;
using RPGFramework.Networking;
using RPGFramework.Networking.UnityTransport;
using RPGFramework.Presentation;
using RPGFramework.Progression;
using RPGFramework.Quests;
using RPGFramework.Scenes;
using RPGFramework.Scenes.Unity;
using RPGFramework.Spawning;
using RPGFramework.UINavigation;
using RPGFramework.UINavigation.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

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
    /// With 'Start On Request' the game waits for <see cref="StartGame"/> (a start menu: continue, new game, host,
    /// join); a menu scene starts the game scene with <see cref="Launch"/>.
    /// </summary>
    public class ActionRpgHost : GameHostBehaviour
    {
        private const string Category = "ActionRpg";

        [Tooltip("The game: characters, waves, items, loot, quests")]
        [SerializeField] private GameSetup _setup;

        [Tooltip("Follows the player; empty = the main camera, which gets a CameraFollow if it has none")]
        [SerializeField] private CameraFollow _camera;

        [SerializeField] private bool _showHud = true;

        [Tooltip("The game's sounds; the kit plays the ActionRpgSounds cues it has (kit.hit, kit.killed...). Empty: no audio module")]
        [SerializeField] private AudioLibrary _audio;

        [Tooltip("Keep the player's level, backpack and quests between sessions (saved on level-up, finished quests and quit)")]
        [SerializeField] private bool _saveProgress;

        [Tooltip("Also save every this many seconds, so a crash loses little (0 = only on level-up, quests and quit)")]
        [SerializeField, Min(0f)] private float _autosaveSeconds = 60f;

        [Tooltip("In the Editor and development builds, F3 shows systems, events and frame times (off in release builds)")]
        [SerializeField] private bool _diagnostics = true;

        [Tooltip("Wait for StartGame (a start menu: continue, new game, host, join) instead of starting at once; the world does not run until then")]
        [SerializeField] private bool _startOnRequest;

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
        private IPrefabSpawner _spawner;
        private ActorPresenter _presenter;
        private CodeViewFactory _screens;
        private NavigationService _navigation;
        private InputAction _back;
        private ActionRpgSaves _saves;

        // A guest's own progress, loaded from the save to bring along (ProgressMode.PerPlayer)
        private PlayerProgress _profile;
        private SubscriptionBag _saveTriggers = new SubscriptionBag();
        private SubscriptionBag _pvpTriggers = new SubscriptionBag();
        private IDisposable _sounds;
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

        // The host being carried into a scene it is loading: a host in that scene steps aside for it
        private static ActionRpgHost _carried;
        private bool _changingScene;
        private Transform _followed;

        // How a menu scene asked the game scene's host to start (Launch)
        private static GameStart? _launch;
        private bool _started;

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
        /// Screens over the game (a shop, a stats page, a map, a pause menu): <c>Navigation.NavigateToAsync(id)</c>
        /// opens one, Escape or the gamepad's East button closes the top one. Screens are made in code, registered in
        /// <see cref="ConfigureScreens"/> (panels such as <c>StatListPanel</c> build themselves); made on first use.
        /// </summary>
        public INavigationService Navigation
        {
            get
            {
                if (_navigation == null)
                    MakeScreens();
                return _navigation;
            }
        }

        /// <summary>
        /// Where the game's code-made screens are registered (also after <see cref="ConfigureScreens"/>, from a module
        /// or a scene object); made on first use, with <see cref="Navigation"/>.
        /// </summary>
        public CodeViewFactory Screens
        {
            get
            {
                if (_screens == null)
                    MakeScreens();
                return _screens;
            }
        }

        /// <summary>
        /// The player's saved progress (null when 'Save Progress' is off, the game did not start, or while joined).
        /// </summary>
        public ActionRpgSaves Saves => _saves;

        /// <summary>
        /// Where this game's progress is saved: a folder per Game Setup under the persistent data path.
        /// </summary>
        public string SaveDirectory => SaveDirectoryOf(_setup);

        /// <summary>
        /// Where the progress of a game with this setup is saved (also from a menu scene without a host).
        /// </summary>
        public static string SaveDirectoryOf(GameSetup setup) =>
            Path.Combine(Application.persistentDataPath, "Saves", setup != null ? setup.name : "Game");

        /// <summary>
        /// The save as a start menu shows it (level, name, hero, time played, when), read without starting the game;
        /// null when there is none.
        /// </summary>
        public SaveSummary SavedGame => ActionRpgSaves.ReadSummary(SaveDirectory, Application.version);

        /// <summary>
        /// <see cref="SavedGame"/> of a game with this setup, from a menu scene without a host.
        /// </summary>
        public static SaveSummary SavedGameOf(GameSetup setup) => ActionRpgSaves.ReadSummary(SaveDirectoryOf(setup), Application.version);

        /// <summary>
        /// The game was started (at once, or by <see cref="StartGame"/> with 'Start On Request').
        /// </summary>
        public bool IsStarted => _started;

        /// <summary>
        /// The game started, and how.
        /// </summary>
        public event Action<GameStart> GameStarted;

        /// <summary>
        /// Waits for <see cref="StartGame"/> instead of starting at once. Set before the host starts (an inactive
        /// GameObject), or tick 'Start On Request'.
        /// </summary>
        public void UseStartOnRequest(bool startOnRequest) => _startOnRequest = startOnRequest;

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
        /// Other codes that reach this game (the host's VPN addresses, a second network); empty when there is one.
        /// </summary>
        public IReadOnlyList<string> OtherJoinCodes { get; private set; } = Array.Empty<string>();

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
                Debug.LogError("ActionRpgHost: drag a Game Setup asset into the 'Setup' field", this); // framework-ok: the host as console context, a click selects it
                throw new InvalidOperationException("ActionRpgHost has no Game Setup");
            }

            var problems = _setup.Validate();
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                    Debug.LogError($"Game Setup '{_setup.name}': {problem}", _setup); // framework-ok: the asset as console context, so a click selects it
                throw new InvalidOperationException($"Game Setup '{_setup.name}' has {problems.Count} problem(s)");
            }

            _settings = _setup.BuildSettings();
            _content = _setup.BuildContent();
            ConfigureSettings(_settings, _content);
            // Pools, assets, the prefab spawner and scenes, for the views (also when joined)
            var services = new UnityServicesOptions();
            ConfigureUnityServices(services);
            builder.UseModule(new UnityServicesModule(services));
            if (_audio != null)
                builder.UseModule(new AudioModule(_audio));
            if (DiagnosticsEnabled)
                builder.UseModule(new DiagnosticsModule());
            if (_joinCode != null)
            {
                // Joined: the friend's machine runs the rules, this one shows them
                _module = null;
                return;
            }

            _module = ActionRpgComposition.Configure(builder, _settings, _content);
            _module.Distance = AbilityDistance;
            _module.InteractionDistance = InteractionDistance;
            ConfigureGameModules(builder);
        }

        /// <summary>
        /// Distance between two characters for ability range (<c>Range</c> on the ability asset). By default the
        /// distance between where the scene shows them; a game whose characters do not live in free 3D (a runner's
        /// lanes and distance along the track, a grid) returns its own measure. 0 counts the target as in range.
        /// </summary>
        protected virtual float AbilityDistance(int casterId, int targetId)
        {
            if (_presenter == null || !_presenter.TryGetPose(casterId, out var caster) || !_presenter.TryGetPose(targetId, out var target))
                return 0f;
            float x = caster.X - target.X, y = caster.Y - target.Y, z = caster.Z - target.Z;
            return Mathf.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// Distance from a player to a place they may use (<see cref="InteractionPointView"/>, or one the game added
        /// to <c>World.Interactions</c>), against the place's radius. By default from where the scene shows the
        /// player to the place's position; a game off free 3D (a track, a grid) returns its own measure. NaN counts
        /// as out of reach. Used on the host for every player, and on a joined machine for the prompt.
        /// </summary>
        protected virtual float InteractionDistance(int playerId, InteractionPoint point)
        {
            if (_presenter == null || !_presenter.TryGetPose(playerId, out var pose))
                return float.NaN;
            float x = pose.X - point.X, y = pose.Y - point.Y, z = pose.Z - point.Z;
            return Mathf.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// Registers the game's screens, made in code with no prefab:
        /// <c>screens.Register&lt;StatListPanel&gt;("stats", panel => panel.Bind(...))</c>. Screens with prefabs go
        /// through a <see cref="PrefabViewFactory"/> set as <c>screens.Fallback</c>. Runs once, the first time
        /// <see cref="Navigation"/> is used; screens read the game through <see cref="CurrentWorld"/> when they open.
        /// </summary>
        protected virtual void ConfigureScreens(CodeViewFactory screens)
        {
        }

        /// <summary>
        /// Adjusts the rules and content built from the Game Setup before the game starts: a damage calculator of
        /// your own (<c>settings.DamageCalculator</c>), status effects and abilities (<c>content.StatusEffects</c>,
        /// <c>content.Abilities</c>), anything the asset does not show. Runs on every machine, also one that joins a
        /// friend's game, so host and friends agree on the rules (their fingerprints must match).
        /// </summary>
        protected virtual void ConfigureSettings(ActionRpgSettings settings, ActionRpgContent content)
        {
        }

        /// <summary>
        /// Add your own <c>GameFeatureModule</c>s here; they can resolve <see cref="ActorWorld"/> and subscribe to its events.
        /// They run where the rules run: alone or hosting, not on a machine that joined a friend's game.
        /// </summary>
        protected virtual void ConfigureGameModules(IGameCompositionBuilder builder)
        {
        }

        /// <summary>
        /// Adds asset sources to the shared <c>IAssetManager</c> (Resources is there already), e.g.
        /// <c>options.AddAssetProvider(() => new AddressablesAssetProvider())</c>. The services are in the container:
        /// <c>Host.Container.Resolve&lt;IPrefabSpawner&gt;()</c> (recipe 14).
        /// </summary>
        protected virtual void ConfigureUnityServices(UnityServicesOptions options)
        {
        }

        /// <summary>
        /// Add your game's own state to the saved progress: <c>saves.Register(new MyShopSave(shop))</c> with a
        /// <c>SaveParticipant&lt;TState&gt;</c>. Runs before the save is loaded, when 'Save Progress' is on and this
        /// machine runs the rules.
        /// </summary>
        protected virtual void ConfigureSaves(ActionRpgSaves saves)
        {
        }

        /// <summary>
        /// Adds your game's own co-op messages (ids from <see cref="CoopProtocol.FirstGameMessageId"/>):
        /// <c>messages.Add&lt;VoteMessage&gt;(1200, MessageAuthority.Client)</c>. Runs on the host and on every client.
        /// </summary>
        protected virtual void ConfigureCoopMessages(CoopGameMessages messages)
        {
        }

        /// <summary>
        /// The co-op host was just opened: set <see cref="Coop.CoopHost.DefenseAuthority"/>, respawn points, attack
        /// checks, and handle your messages with <c>host.On&lt;T&gt;</c>.
        /// </summary>
        protected virtual void OnCoopHostOpened(CoopHost host)
        {
        }

        /// <summary>
        /// This machine joined a friend's game: handle your messages with <c>client.On&lt;T&gt;</c>, answer
        /// <see cref="IncomingAttackEvent"/>s with <c>client.Defend</c>.
        /// </summary>
        protected virtual void OnCoopClientCreated(CoopClient client)
        {
        }

        /// <summary>
        /// The game is now in another scene (<see cref="ChangeScene"/>, or the host's game moved while joined).
        /// Alone or hosting, place the players for it here: <see cref="TeleportPlayer"/> to the new scene's spawn
        /// points (they keep their old positions otherwise). Enemies stay unless the game removes them.
        /// </summary>
        protected virtual void OnSceneChanged(string sceneName)
        {
        }

        private CoopGameMessages GameMessages()
        {
            var messages = new CoopGameMessages();
            ConfigureCoopMessages(messages);
            return messages;
        }

        protected override void Awake()
        {
            // Part of a scene the running game is moving into: the game that is already running goes on
            if (_carried != null && _carried != this)
            {
                enabled = false;
                Destroy(gameObject); // framework-ok: a duplicate host in a scene being loaded, never spawned
                return;
            }
            base.Awake();
        }

        /// <summary>
        /// Moves the game to another scene (a hub, a run, the next floor) without ending it: the party, progress,
        /// characters and the co-op link stay; friends who joined load the scene too, and so does anyone who joins
        /// later. The scene holds the level only; an <see cref="ActionRpgHost"/> in it steps aside for this one, so
        /// each scene can still be played on its own in the Editor. Loads through <c>ISceneDispatcher</c> (the scene
        /// must be in the build settings). Then <see cref="OnSceneChanged"/> runs.
        /// </summary>
        /// <returns>False when it cannot change now (joined to a friend's game, switching, another change running),
        /// or the scene failed to load (logged).</returns>
        public async UniTask<bool> ChangeScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || Role == CoopRole.Joined || World == null || IsSwitching || _changingScene)
                return false;
            _coopHost?.ChangeScene(sceneName);
            return await LoadScene(sceneName);
        }

        private async UniTask<bool> LoadScene(string sceneName)
        {
            if (Host?.Container == null)
                return false;
            _changingScene = true;
            _carried = this;
            DontDestroyOnLoad(gameObject);
            try
            {
                await Host.Container.Resolve<ISceneDispatcher>().LoadAsync(sceneName);
            }
            catch (Exception exception)
            {
                Host.Logger.LogError($"Scene '{sceneName}' could not be loaded: {exception.Message}", Category);
                return false;
            }
            finally
            {
                _carried = null;
                _changingScene = false;
            }
            if (this == null)
                return false;
            // The old scene's camera is gone: follow the player with the new one
            if (_followed != null)
                FollowWithCamera(_followed);
            if (_saves != null)
                _saves.Location = sceneName;
            OnSceneChanged(sceneName);
            return true;
        }

        // Joined: play in the scene the host's game is in
        private void FollowHostScene(string sceneName)
        {
            if (Host?.Container == null || _changingScene)
                return;
            if (Host.Container.Resolve<ISceneDispatcher>().ActiveScene == sceneName)
                return;
            LoadScene(sceneName).Forget();
        }

        /// <summary>
        /// Puts a player somewhere at once, on every machine (a friend's own machine moves them). Alone or hosting.
        /// </summary>
        /// <returns>False when this machine does not run the game or there is no such player.</returns>
        public bool TeleportPlayer(int playerId, ActorPose pose)
        {
            if (World?.Get(playerId) == null || _presenter == null || !pose.IsFinite)
                return false;
            if (_coopHost != null)
                _coopHost.Teleport(playerId, pose);
            else
                _presenter.Teleport(playerId, pose);
            return true;
        }

        protected virtual void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (_coop)
                UseCommandLineSettings(args);
            if (_launch.HasValue)
            {
                var launch = _launch.Value;
                _launch = null;
                StartGame(launch);
                return;
            }
            // -host / -join start a waiting game too
            var fromArgs = _coop ? CommandLineStart(args) : null;
            if (_startOnRequest)
            {
                if (fromArgs.HasValue)
                    StartGame(fromArgs.Value);
                return;
            }
            StartGame(fromArgs ?? GameStart.Continue);
        }

        /// <summary>
        /// Starts the game as the player chose in a start menu: on from the save, a new game (the save is deleted),
        /// hosting, or joining a friend. Once per host; without 'Start On Request' the host starts by itself.
        /// </summary>
        /// <returns>False when it already started, failed to start, or co-op is off for Host / Join (or no code).</returns>
        public bool StartGame(GameStart start)
        {
            if (_started || Host?.Container == null || !enabled)
                return false;
            bool together = start.Kind == GameStartKind.Host || start.Kind == GameStartKind.Join;
            if (together && !_coop)
                return false;
            if (start.Kind == GameStartKind.Join && string.IsNullOrEmpty(start.JoinCode))
                return false;
            _started = true;
            if (start.Kind == GameStartKind.NewGame && World != null)
                new ActionRpgSaves(World, SaveDirectory, Application.version).Delete();
            if (start.Kind == GameStartKind.Join)
            {
                // The friend's machine runs the rules: this host starts again without a world of its own
                _joinCode = start.JoinCode;
                _reconnectAttempt = 0;
                CoopStatus = $"Joining {_joinCode}...";
                Restart();
            }
            ShowGame();
            if (start.Kind == GameStartKind.Host)
                HostGame();
            GameStarted?.Invoke(start);
            return true;
        }

        /// <summary>
        /// From a menu scene: loads the game scene and has its <see cref="ActionRpgHost"/> start as the player chose
        /// (it starts so even without 'Start On Request'). Call it where no game is running.
        /// </summary>
        /// <returns>False when the scene could not be loaded (not in the build settings).</returns>
        public static async UniTask<bool> Launch(string sceneName, GameStart start)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("A scene name is required", nameof(sceneName));
            _launch = start;
            try
            {
                await new UnitySceneBackend().LoadAsync(sceneName, false, null, default);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                _launch = null;
                Debug.LogError(exception.Message); // framework-ok: no host is running yet to log it
                return false;
            }
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
            _coopHost = new CoopHost(World, Host.Container.Resolve<IEventBus>(), CoopProtocol.CreateNetwork(_transport, Host.Logger, ConnectionTimeout, GameMessages()),
                _presenter, CoopProtocol.Fingerprint(_settings, _content), Host.Logger)
            {
                // A friend's strike is checked against where this machine sees them, with slack for lag
                MaxAttackDistance = _setup.Player.AttackRange + 1.5f,
                MaxPlayers = MaxPlayers
            };
            World.SetPlayerName(World.PlayerId, PlayerName);
            // Friends who join load the scene the game is in
            _coopHost.ChangeScene(Host.Container.Resolve<ISceneDispatcher>().ActiveScene);
            _coopHost.Open();
            KeepRunningInBackground();
            OnCoopHostOpened(_coopHost);
            JoinCode = session.JoinCode;
            OtherJoinCodes = session.OtherJoinCodes;
            CoopStatus = $"Hosting: friends join with {JoinCode}";
            Host.Logger.LogInfo(CoopStatus, Category);
        }

        /// <summary>
        /// A co-op machine keeps running when its window loses focus: a paused host freezes every friend, a paused
        /// guest times out. Set at runtime so games whose Player Settings still say otherwise keep working.
        /// </summary>
        private static void KeepRunningInBackground() => Application.runInBackground = true;

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
            // Waiting for the start menu: the world does not run yet
            if (!_started)
                return;
            if (_restartPending)
            {
                _restartPending = false;
                HideGame();
                Restart();
                ShowGame();
                return;
            }
            base.Update();
            ShowPlaces();
            if (_navigation != null && _back.WasPressedThisFrame())
                CloseTopScreen();
            _coopHost?.Update(Time.unscaledDeltaTime);
            _coopClient?.Update(Time.unscaledDeltaTime);
            Autosave(Time.unscaledDeltaTime);
        }

        private void MakeScreens()
        {
            var canvasObject = new GameObject("Screens", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _screens = new CodeViewFactory(canvasObject.transform);
            _navigation = new NavigationService(_screens, events: Host?.Container?.Resolve<IEventBus>(), logger: Host?.Logger);
            _screens.ViewCreated = (id, view) =>
            {
                if (view is RPGFramework.UI.UIViewBase panel)
                    panel.Navigation = _navigation;
                // Buttons on screens need an event system; a scene without one gets it here
                if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
                {
                    var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    events.transform.SetParent(transform, false);
                }
            };
            _back = new InputAction("Back", InputActionType.Button);
            _back.AddBinding("<Keyboard>/escape");
            _back.AddBinding("<Gamepad>/buttonEast");
            _back.Enable();
            ConfigureScreens(_screens);
        }

        // A modal or a screen above another goes back; the last screen closes, back to the game
        private void CloseTopScreen()
        {
            if (_navigation.IsBusy)
                return;
            if (_navigation.CanGoBack)
                _navigation.GoBackAsync().Forget();
            else if (_navigation.History.Count > 0)
                _navigation.ClearAsync().Forget();
        }

        private void OnPlaceChanged(InteractionPointView view, bool active)
        {
            if (active)
                AddPlace(view);
            else
                RemovePlace(view);
        }

        private void AddPlace(InteractionPointView view)
        {
            if (World == null)
                return;
            view.Point = view.CreatePoint();
            World.Interactions.Add(view.Point);
        }

        private void RemovePlace(InteractionPointView view)
        {
            if (World != null && view.Point != null && World.Interactions.Get(view.Id) == view.Point)
                World.Interactions.Remove(view.Id);
            view.Point = null;
        }

        // Places that moved follow their object; every machine shows them open or closed as the world has them
        private void ShowPlaces()
        {
            var places = CurrentWorld?.Interactions;
            if (places == null)
                return;
            var views = InteractionPointView.Active;
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                if (view.Point != null && view.transform.hasChanged)
                {
                    var position = view.transform.position;
                    view.Point.At(position.x, position.y, position.z);
                    view.transform.hasChanged = false;
                }
                var point = places.Get(view.Id);
                view.ShowOpen(point == null || point.Enabled);
            }
        }

        private void Autosave(float deltaTime)
        {
            if (_saves == null)
                return;
            _saves.PlaySeconds += deltaTime;
            if (_autosaveSeconds <= 0f)
                return;
            _sinceSave += deltaTime;
            if (_sinceSave < _autosaveSeconds)
                return;
            _sinceSave = 0f;
            SaveProgress();
        }

        protected override void FixedUpdate()
        {
            if (_started)
                base.FixedUpdate();
        }

        protected override void LateUpdate()
        {
            if (_started)
                base.LateUpdate();
        }

        private void UseCommandLineSettings(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-port" && int.TryParse(args[i + 1], out int port))
                    UseCoopPort(port);
                else if (args[i] == "-provider" && !UseSessionProvider(args[i + 1]))
                    CoopStatus = $"'{args[i + 1]}' is not an installed way to play together; using {SessionProvider.DisplayName}.";
            }
        }

        private static GameStart? CommandLineStart(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host")
                    return GameStart.Host;
                if (args[i] == "-join" && i + 1 < args.Length)
                    return GameStart.Join(args[i + 1]);
            }
            return null;
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
            // With progress of their own, a guest brings their saved progress and saves what the host reports back
            if (_saveProgress && _settings.ProgressMode == ProgressMode.PerPlayer)
            {
                _saves = ActionRpgSaves.ForProgressOnly(SaveDirectory, Application.version,
                    () => _coopClient?.World.CaptureProgress(), progress => _profile = progress);
                string loadError = _saves.Load();
                if (loadError != null)
                    Host.Logger.LogError($"Saved progress could not be loaded: {loadError}", Category);
            }
            _coopClient = new CoopClient(_settings, _content, Host.Container.Resolve<IEventBus>(),
                CoopProtocol.CreateNetwork(_transport, Host.Logger, ConnectionTimeout, GameMessages()), null, Host.Logger)
            {
                PlayerName = PlayerName,
                RejoinToken = _rejoinCode == _joinCode ? _rejoinToken : "",
                Profile = _profile
            };
            KeepRunningInBackground();
            _coopClient.World.Interactions.Distance = InteractionDistance;
            OnCoopClientCreated(_coopClient);
            _coopClient.Joined += OnJoined;
            _coopClient.Ended += OnClientEnded;
            _coopClient.SceneChanged += FollowHostScene;
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

            _spawner = Host.Container.Resolve<IPrefabSpawner>();
            _presenter = new ActorPresenter(CurrentWorld, _setup, Host.Container.Resolve<IEventBus>(), _spawner,
                new EntityViewRegistry(), _charactersRoot, Host.Logger, enemiesDrivenElsewhere: _coopClient != null);
            _presenter.PartyColors = _partyColors;
            _presenter.ViewShown += OnViewShown;
            _presenter.Start();
            if (_audio != null && Host.Container.IsRegistered<IAudioService>())
            {
                _sounds = ActionRpgSounds.Wire(Host.Container.Resolve<IAudioService>(), entityId =>
                    _presenter != null && _presenter.TryGetPose(entityId, out var pose)
                        ? new AudioPosition(pose.X, pose.Y, pose.Z)
                        : (AudioPosition?)null);
            }

            // This machine runs the match: it puts players at their side's spawn point
            if (_coopClient == null && World?.Settings.Pvp != null)
            {
                var bus = Host.Container.Resolve<IEventBus>();
                bus.Subscribe<PvpRoundStartedEvent>(_ => PlacePlayers()).AddTo(_pvpTriggers);
                bus.Subscribe<PvpRespawnedEvent>(respawned => PlacePlayer(respawned.PlayerId)).AddTo(_pvpTriggers);
            }

            if (DiagnosticsEnabled)
                StartDiagnostics();

            // This machine runs the rules: the scene's places go into its world (friends get them from here)
            if (_coopClient == null && World != null)
            {
                foreach (var view in InteractionPointView.Active)
                    AddPlace(view);
                InteractionPointView.ActiveChanged += OnPlaceChanged;
            }

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
            _pvpTriggers.Dispose();
            _sounds?.Dispose();
            _sounds = null;
            InteractionPointView.ActiveChanged -= OnPlaceChanged;
            foreach (var view in InteractionPointView.Active)
                RemovePlace(view);
            _pvpTriggers = new SubscriptionBag();
            _saves = null;
            _generation++;
            _opening = false;
            StopNetwork();
            _presenter?.Dispose();
            _presenter = null;
            // The spawner belongs to the host's UnityServicesModule, which releases it when the host stops
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
                _coopClient.SceneChanged -= FollowHostScene;
                _coopClient.Dispose();
                _coopClient = null;
            }
            (_transport as IDisposable)?.Dispose();
            _transport = null;
            JoinCode = "";
            OtherJoinCodes = Array.Empty<string>();
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
            ConfigureSaves(_saves);
            string error = _saves.Load();
            _saves.Location = Host.Container.Resolve<ISceneDispatcher>().ActiveScene ?? "";
            if (error != null)
                Host.Logger.LogError($"Saved progress could not be loaded: {error}", Category);
            else if (_saves.Notice.Length > 0)
                Host.Logger.LogWarning(_saves.Notice, Category);
            // The player reads it in the game's log
            if (_saves.Notice.Length > 0)
                World.Announce(_saves.Notice);
            else if (!_saves.CanSave)
                World.Announce("Progress is not saved: the save was made by a newer version of the game.");

            var bus = Host.Container.Resolve<IEventBus>();
            bus.Subscribe<LevelUpEvent>(_ => SaveProgress()).AddTo(_saveTriggers);
            bus.Subscribe<QuestCompletedEvent>(_ => SaveProgress()).AddTo(_saveTriggers);
        }

        /// <summary>
        /// Saves now (does nothing when 'Save Progress' is off or while joined). Returns false and logs when the save failed.
        /// </summary>
        public bool SaveProgress()
        {
            // A guest saves only with progress of their own; their World is the host's
            if (_saves == null || (World == null && _coopClient == null) || (World != null && World.IsRestoring))
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
            Debug.Log($"Saved progress deleted: {SaveDirectory}", this); // framework-ok: context menu, works without a running game
        }

        private void PlacePlayers()
        {
            foreach (int id in World.Players)
                PlacePlayer(id);
        }

        private void PlacePlayer(int playerId)
        {
            var world = World;
            var player = world?.Get(playerId);
            var pvp = world?.Settings.Pvp;
            if (player == null || pvp == null || _presenter == null)
                return;
            int member = 0;
            foreach (int id in world.Players)
            {
                var other = world.Get(id);
                if (other != null && other.Team == player.Team && other.Seat < player.Seat)
                    member++;
            }
            int sides = pvp.Mode == PvpMode.FreeForAll ? world.Players.Count : pvp.Teams.Count;
            var spot = _setup.Pvp.SpawnPoint(pvp.SideIndexOf(player.Team), member, sides);
            TeleportPlayer(playerId, new ActorPose(spot.position.x, spot.position.y, spot.position.z, spot.rotation.eulerAngles.y));
        }

        private void OnViewShown(int entityId, GameObject view)
        {
            if (entityId == CurrentWorld?.PlayerId)
                FollowWithCamera(view.transform);
            ViewShown?.Invoke(entityId, view);
        }

        private void FollowWithCamera(Transform player)
        {
            _followed = player;
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
            if (_carried == this)
                _carried = null;
            // Save, leave and drop views first: they read the world, which the host shuts down
            HideGame();
            _back?.Dispose();
            _screens?.Dispose();
            base.OnDestroy();
        }
    }
}
