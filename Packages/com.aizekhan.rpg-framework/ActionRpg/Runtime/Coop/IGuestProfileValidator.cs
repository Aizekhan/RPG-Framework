namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// The host's check of the progress a joining player brings from home (<see cref="ProgressMode.PerPlayer"/>), set
    /// on <see cref="CoopHost.ProfileValidator"/>. It runs once per player per session, on the host only.
    /// </summary>
    public interface IGuestProfileValidator
    {
        /// <param name="playerName">The name they join with.</param>
        /// <param name="profile">What their machine sent; never null.</param>
        GuestProfileCheck Check(string playerName, PlayerProgress profile);
    }

    /// <summary>
    /// What the host does with a brought profile: take it (as it is, or a cleaned copy), start the player fresh, or
    /// turn them away with a reason they see.
    /// </summary>
    public readonly struct GuestProfileCheck
    {
        private GuestProfileCheck(PlayerProgress profile, string reason)
        {
            Profile = profile;
            Reason = reason;
        }

        /// <summary>
        /// The progress they start with; null starts them fresh.
        /// </summary>
        public PlayerProgress Profile { get; }

        /// <summary>
        /// Why they are turned away; null when they may play.
        /// </summary>
        public string Reason { get; }

        public bool Refused => Reason != null;

        public static GuestProfileCheck Accept(PlayerProgress profile) => new GuestProfileCheck(profile, null);

        public static GuestProfileCheck Fresh() => new GuestProfileCheck(null, null);

        public static GuestProfileCheck Refuse(string reason) =>
            new GuestProfileCheck(null, string.IsNullOrWhiteSpace(reason) ? "The host did not accept your saved progress." : reason);
    }
}
