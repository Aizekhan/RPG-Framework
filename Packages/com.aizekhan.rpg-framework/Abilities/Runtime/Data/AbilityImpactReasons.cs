namespace RPGFramework.Abilities
{
    /// <summary>
    /// Why a delayed hit missed (<see cref="IAbilityImpacts.ImpactMissed"/>, <see cref="AbilityImpactEvent.Reason"/>);
    /// the game's own reasons for an interrupt come as given.
    /// </summary>
    public static class AbilityImpactReasons
    {
        /// <summary>
        /// The target was no longer valid when the hit came (<see cref="CastInfo.RecheckTargetOnImpact"/>).
        /// </summary>
        public const string InvalidTarget = "Invalid target";

        public const string Interrupted = "interrupted";
        public const string Canceled = "canceled";
        public const string CasterDied = "caster died";
    }
}
