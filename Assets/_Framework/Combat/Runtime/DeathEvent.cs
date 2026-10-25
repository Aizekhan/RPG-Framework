namespace RPGFramework.Combat
{
    /// <summary>
    /// Event raised when an entity dies.
    /// </summary>
    /// <remarks>
    /// This event provides information about the death, including the killer and reason.
    /// Game layer can listen to this event to trigger death consequences (loot, XP, etc.).
    /// </remarks>
    public struct DeathEvent
    {
        /// <summary>
        /// ID of the entity that died.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// ID of the entity that caused the death.
        /// </summary>
        /// <remarks>
        /// Use -1 for environmental deaths (fall damage, poison, etc.) or self-inflicted death.
        /// </remarks>
        public int KillerEntityId;

        /// <summary>
        /// Reason for the death.
        /// </summary>
        /// <remarks>
        /// Examples: "resource_depletion", "instant_death", "environmental", "timeout".
        /// Game layer can use this to determine death consequences.
        /// </remarks>
        public string DeathReason;

        /// <summary>
        /// Timestamp when the death occurred.
        /// </summary>
        /// <remarks>
        /// Typically Unity Time.time or game-specific time value.
        /// </remarks>
        public float TimeOfDeath;
    }
}
