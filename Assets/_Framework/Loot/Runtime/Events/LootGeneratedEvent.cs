namespace RPGFramework.Loot
{
    /// <summary>
    /// Event raised when loot is generated from a loot table.
    /// Triggered during loot generation process.
    /// </summary>
    public struct LootGeneratedEvent
    {
        /// <summary>
        /// Loot table ID that was rolled.
        /// </summary>
        public string TableId;

        /// <summary>
        /// Entity ID that triggered loot generation (killer, chest opener, etc.).
        /// -1 = no specific entity (manual generation, scripted event).
        /// </summary>
        public int TriggerEntityId;

        /// <summary>
        /// Source entity ID (killed enemy, opened chest, etc.).
        /// -1 = no source entity.
        /// </summary>
        public int SourceEntityId;

        /// <summary>
        /// Number of items generated.
        /// </summary>
        public int ItemCount;

        /// <summary>
        /// Timestamp when loot was generated.
        /// </summary>
        public float Timestamp;
    }
}
