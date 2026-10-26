// Assets/_Framework/Inventory/Runtime/Events/ItemUnequippedEvent.cs

using RPGFramework.Events;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Event raised when an item is unequipped from a slot.
    /// </summary>
    /// <typeparam name="TSlotType">Enum type for equipment slots</typeparam>
    /// <typeparam name="TItem">Type of item unequipped</typeparam>
    public struct ItemUnequippedEvent<TSlotType, TItem> : IEvent
        where TSlotType : System.Enum
    {
        /// <summary>
        /// ID of the entity unequipping the item.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// Type of equipment slot.
        /// </summary>
        public TSlotType SlotType;

        /// <summary>
        /// Item that was unequipped.
        /// </summary>
        public TItem UnequippedItem;

        /// <summary>
        /// Timestamp when the item was unequipped.
        /// </summary>
        public float Timestamp;
    }
}
