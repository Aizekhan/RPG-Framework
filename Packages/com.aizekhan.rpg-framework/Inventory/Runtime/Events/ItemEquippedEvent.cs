// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Events/ItemEquippedEvent.cs

using RPGFramework.Events;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Event raised when an item is equipped to a slot.
    /// </summary>
    /// <typeparam name="TSlotType">Enum type for equipment slots</typeparam>
    /// <typeparam name="TItem">Type of item equipped</typeparam>
    public struct ItemEquippedEvent<TSlotType, TItem> : IEvent
        where TSlotType : System.Enum
    {
        /// <summary>
        /// ID of the entity equipping the item.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// Type of equipment slot.
        /// </summary>
        public TSlotType SlotType;

        /// <summary>
        /// Item that was equipped.
        /// </summary>
        public TItem EquippedItem;

        /// <summary>
        /// Item that was previously in the slot (default if empty).
        /// </summary>
        public TItem PreviousItem;

        /// <summary>
        /// Timestamp when the item was equipped.
        /// </summary>
        public float Timestamp;
    }
}
