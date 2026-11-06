namespace RPGFramework.ActionRpg
{
    public enum GameStartKind
    {
        /// <summary>
        /// Play on from the save (a fresh start when there is none).
        /// </summary>
        Continue,

        /// <summary>
        /// Delete the save and start over.
        /// </summary>
        NewGame,

        /// <summary>
        /// Play on from the save, with the game open for friends to join.
        /// </summary>
        Host,

        /// <summary>
        /// Play in a friend's game (<see cref="GameStart.JoinCode"/>).
        /// </summary>
        Join
    }

    /// <summary>
    /// How a player chose to start from a start menu: continue, a new game, host, or join a friend with a code, and for
    /// hosting and joining how to play together (<see cref="Provider"/>: the local network, Unity Relay).
    /// Give it to <c>ActionRpgHost.StartGame</c>, or to <c>ActionRpgHost.Launch</c> from a menu scene.
    /// </summary>
    /// <example><code>
    /// ActionRpgHost.Launch("Game", GameStart.Host.Via(NetworkSessionProviders.UnityRelay));
    /// ActionRpgHost.Launch("Game", GameStart.Join(code, NetworkSessionProviders.UnityRelay));
    /// </code></example>
    public readonly struct GameStart
    {
        private GameStart(GameStartKind kind, string joinCode, string provider)
        {
            Kind = kind;
            _joinCode = joinCode;
            _provider = provider;
        }

        // default(GameStart) skips the constructor: the getters keep both texts non-null
        private readonly string _joinCode;
        private readonly string _provider;

        public GameStartKind Kind { get; }

        /// <summary>
        /// What the friend's game shows to join it (an address on the local network, a relay code); empty otherwise.
        /// </summary>
        public string JoinCode => _joinCode ?? "";

        /// <summary>
        /// How to host or join: a session provider's id ("lan", "unity-relay", see <c>NetworkSessionProviders</c>).
        /// Empty: the host's own choice (the command line's <c>-provider</c>, the F2 panel, the local network).
        /// Only <see cref="GameStartKind.Host"/> and <see cref="GameStartKind.Join"/> use it.
        /// </summary>
        public string Provider => _provider ?? "";

        public static GameStart Continue => new GameStart(GameStartKind.Continue, "", "");
        public static GameStart NewGame => new GameStart(GameStartKind.NewGame, "", "");
        public static GameStart Host => new GameStart(GameStartKind.Host, "", "");
        public static GameStart Join(string joinCode) => new GameStart(GameStartKind.Join, joinCode?.Trim(), "");

        /// <summary>
        /// Joins a friend's game through a session provider (a relay code needs the relay's provider).
        /// </summary>
        public static GameStart Join(string joinCode, string providerId) =>
            new GameStart(GameStartKind.Join, joinCode?.Trim(), providerId?.Trim());

        /// <summary>
        /// The same start through a session provider, e.g. <c>GameStart.Host.Via("unity-relay")</c>.
        /// </summary>
        public GameStart Via(string providerId) => new GameStart(Kind, JoinCode, providerId?.Trim());

        public override string ToString()
        {
            string text = Kind == GameStartKind.Join ? $"Join {JoinCode}" : Kind.ToString();
            return Provider.Length > 0 ? $"{text} via {Provider}" : text;
        }
    }
}
