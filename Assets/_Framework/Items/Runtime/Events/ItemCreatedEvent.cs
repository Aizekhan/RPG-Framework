namespace RPGFramework.Items
{
    /// <summary>
    /// Event raised when a new item instance is created.
    /// Triggered during loot generation, crafting, quest rewards, etc.
    /// </summary>
    public struct ItemCreatedEvent
    {
        /// <summary>
        /// Unique instance ID of the created item.
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// Item definition ID.
        /// </summary>
        public string ItemDefinitionId;

        /// <summary>
        /// Initial stack quantity.
        /// </summary>
        public int Quantity;

        /// <summary>
        /// Timestamp when item was created.
        /// </summary>
        public float Timestamp;
    }
}
