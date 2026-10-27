using RPGFramework.Events;

namespace RPGFramework.Items
{
    /// <summary>
    /// Event raised when a new item instance is created.
    /// Published by the <see cref="ItemInstance"/> constructor when it is given an event publisher, so pass one
    /// wherever your game creates items (loot generation, crafting, quest rewards, etc.).
    /// </summary>
    public struct ItemCreatedEvent : IEvent
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
        /// Timestamp when item was created (the instance's <c>CreatedAt</c>, 0 unless the creator passes a time).
        /// </summary>
        public float Timestamp;
    }
}
