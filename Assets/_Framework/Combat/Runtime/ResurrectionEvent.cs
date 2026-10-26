using RPGFramework.Events;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Event raised when an entity is resurrected.
    /// </summary>
    /// <remarks>
    /// This event provides information about the resurrection, including who performed it and the type.
    /// Game layer can listen to this event to trigger resurrection effects (animations, buffs, etc.).
    /// </remarks>
    public struct ResurrectionEvent : IEvent
    {
        /// <summary>
        /// ID of the entity that was resurrected.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// ID of the entity that performed the resurrection.
        /// </summary>
        /// <remarks>
        /// Use -1 for self-resurrection (auto-respawn, checkpoint respawn, etc.).
        /// </remarks>
        public int ResurrectorEntityId;

        /// <summary>
        /// Type of resurrection.
        /// </summary>
        /// <remarks>
        /// Examples: "respawn", "player_revive", "auto_respawn", "checkpoint", "consumable".
        /// Game layer can use this to determine resurrection effects.
        /// </remarks>
        public string ResurrectionType;

        /// <summary>
        /// Timestamp when the resurrection occurred.
        /// </summary>
        /// <remarks>
        /// Typically Unity Time.time or game-specific time value.
        /// </remarks>
        public float TimeOfResurrection;
    }
}
