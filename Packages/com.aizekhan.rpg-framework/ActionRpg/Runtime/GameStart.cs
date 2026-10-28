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
    /// How a player chose to start from a start menu: continue, a new game, host, or join a friend with a code.
    /// Give it to <c>ActionRpgHost.StartGame</c>, or to <c>ActionRpgHost.Launch</c> from a menu scene.
    /// </summary>
    public readonly struct GameStart
    {
        private GameStart(GameStartKind kind, string joinCode)
        {
            Kind = kind;
            JoinCode = joinCode ?? "";
        }

        public GameStartKind Kind { get; }

        /// <summary>
        /// What the friend's game shows to join it (an address on the local network, a relay code); empty otherwise.
        /// </summary>
        public string JoinCode { get; }

        public static GameStart Continue => new GameStart(GameStartKind.Continue, "");
        public static GameStart NewGame => new GameStart(GameStartKind.NewGame, "");
        public static GameStart Host => new GameStart(GameStartKind.Host, "");
        public static GameStart Join(string joinCode) => new GameStart(GameStartKind.Join, joinCode?.Trim());

        public override string ToString() => Kind == GameStartKind.Join ? $"Join {JoinCode}" : Kind.ToString();
    }
}
