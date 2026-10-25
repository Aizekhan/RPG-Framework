namespace RPGFramework.Items
{
    /// <summary>
    /// Event raised when an item instance is destroyed.
    /// Triggered when items are consumed, deleted, or removed from the game.
    /// </summary>
    public struct ItemDestroyedEvent
    {
        /// <summary>
        /// Unique instance ID of the destroyed item.
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// Item definition ID.
        /// </summary>
        public string ItemDefinitionId;

        /// <summary>
        /// Quantity that was destroyed.
        /// </summary>
        public int Quantity;

        /// <summary>
        /// Timestamp when item was destroyed.
        /// </summary>
        public float Timestamp;
    }
}
