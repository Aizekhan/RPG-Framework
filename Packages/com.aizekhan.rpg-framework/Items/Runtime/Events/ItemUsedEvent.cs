namespace RPGFramework.Items
{
    /// <summary>
    /// Event raised when a consumable item is used.
    /// Triggered when players activate consumable items.
    /// </summary>
    public struct ItemUsedEvent
    {
        /// <summary>
        /// Entity ID of who used the item.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// Instance ID of the used item.
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// Item definition ID.
        /// </summary>
        public string ItemDefinitionId;

        /// <summary>
        /// Effect ID applied by this consumable.
        /// </summary>
        public string EffectId;

        /// <summary>
        /// Effect magnitude (heal amount, buff strength, etc.).
        /// </summary>
        public float EffectMagnitude;

        /// <summary>
        /// Timestamp when item was used.
        /// </summary>
        public float Timestamp;
    }
}
