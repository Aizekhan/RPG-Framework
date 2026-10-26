// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Events/ItemAddedEvent.cs

using RPGFramework.Events;

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Event raised when an item is added to a container.
    /// </summary>
    /// <typeparam name="TItem">Type of item added</typeparam>
    public struct ItemAddedEvent<TItem> : IEvent
    {
        /// <summary>
        /// ID of the entity that owns the container.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// Name of the container ("primary", "bank", "quest", etc.)
        /// </summary>
        public string ContainerName;

        /// <summary>
        /// Item that was added.
        /// </summary>
        public TItem Item;

        /// <summary>
        /// Quantity added (for stackable items).
        /// </summary>
        public int Quantity;

        /// <summary>
        /// Timestamp when the item was added.
        /// </summary>
        public float Timestamp;
    }
}
