// Assets/_Framework/Inventory/Runtime/Events/ItemRemovedEvent.cs

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Event raised when an item is removed from a container.
    /// </summary>
    /// <typeparam name="TItem">Type of item removed</typeparam>
    public struct ItemRemovedEvent<TItem>
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
        /// Item that was removed.
        /// </summary>
        public TItem Item;

        /// <summary>
        /// Quantity removed (for stackable items).
        /// </summary>
        public int Quantity;

        /// <summary>
        /// Timestamp when the item was removed.
        /// </summary>
        public float Timestamp;
    }
}
