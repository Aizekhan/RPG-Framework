namespace RPGFramework.Progression
{
    /// <summary>
    /// Event raised when a stat increases due to level up.
    /// </summary>
    public struct StatIncreasedEvent
    {
        /// <summary>Entity whose stat increased</summary>
        public int EntityId;

        /// <summary>Stat identifier (e.g., "Strength", "HP")</summary>
        public string StatId;

        /// <summary>Previous stat value</summary>
        public float OldValue;

        /// <summary>New stat value</summary>
        public float NewValue;

        /// <summary>Level at which increase occurred</summary>
        public int Level;

        /// <summary>Timestamp of stat increase</summary>
        public float Timestamp;
    }
}
