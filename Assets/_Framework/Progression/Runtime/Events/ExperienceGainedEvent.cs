namespace RPGFramework.Progression
{
    /// <summary>
    /// Event raised when an entity gains experience points.
    /// </summary>
    public struct ExperienceGainedEvent
    {
        /// <summary>Entity that gained XP</summary>
        public int EntityId;

        /// <summary>Amount of XP gained</summary>
        public long XPAmount;

        /// <summary>New total XP</summary>
        public long TotalXP;

        /// <summary>Current level (may be same if no level up)</summary>
        public int CurrentLevel;

        /// <summary>Timestamp of XP gain</summary>
        public float Timestamp;
    }
}
