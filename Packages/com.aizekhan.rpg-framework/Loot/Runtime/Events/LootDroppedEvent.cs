namespace RPGFramework.Loot
{
    /// <summary>
    /// Event raised when a loot item is dropped in the world.
    /// Triggered when converting generated loot to world entities.
    /// </summary>
    public struct LootDroppedEvent
    {
        /// <summary>
        /// Item definition ID dropped.
        /// </summary>
        public string ItemId;

        /// <summary>
        /// Quantity dropped.
        /// </summary>
        public int Quantity;

        /// <summary>
        /// Entity ID representing the dropped item in world.
        /// References the ItemComponent entity created for this drop.
        /// </summary>
        public int ItemEntityId;

        /// <summary>
        /// Source entity ID (killed enemy, opened chest).
        /// -1 = no source entity.
        /// </summary>
        public int SourceEntityId;

        /// <summary>
        /// Timestamp when loot was dropped.
        /// </summary>
        public float Timestamp;
    }
}
