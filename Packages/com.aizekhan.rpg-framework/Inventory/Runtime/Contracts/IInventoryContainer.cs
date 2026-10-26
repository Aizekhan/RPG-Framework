// Packages/com.aizekhan.rpg-framework/Inventory/Runtime/Contracts/IInventoryContainer.cs

namespace RPGFramework.Inventory.Runtime
{
    /// <summary>
    /// Generic item container with capacity management.
    /// Supports any item type via generics (string IDs, item instances, etc.).
    /// </summary>
    /// <typeparam name="TItem">Type of items stored (use struct or int for IDs, objects for instances)</typeparam>
    public interface IInventoryContainer<TItem>
    {
        /// <summary>
        /// Maximum number of items this container can hold.
        /// -1 for unlimited capacity.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Current number of items in the container.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Whether the container is at maximum capacity.
        /// </summary>
        bool IsFull { get; }

        /// <summary>
        /// Whether the container is empty.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Adds an item to the container.
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <returns>True if item was added, false if container is full</returns>
        bool Add(TItem item);

        /// <summary>
        /// Removes the first occurrence of an item from the container.
        /// </summary>
        /// <param name="item">Item to remove</param>
        /// <returns>True if item was removed, false if item not found</returns>
        bool Remove(TItem item);

        /// <summary>
        /// Removes item at specific index.
        /// </summary>
        /// <param name="index">Zero-based index of item to remove</param>
        /// <returns>True if item was removed, false if index invalid</returns>
        bool RemoveAt(int index);

        /// <summary>
        /// Checks if container contains an item.
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if item exists in container</returns>
        bool Contains(TItem item);

        /// <summary>
        /// Gets item at specific index.
        /// </summary>
        /// <param name="index">Zero-based index</param>
        /// <returns>Item at index, or default(TItem) if invalid</returns>
        TItem GetAt(int index);

        /// <summary>
        /// Clears all items from the container.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets all items in the container.
        /// </summary>
        /// <returns>Read-only collection of items</returns>
        System.Collections.Generic.IReadOnlyList<TItem> GetAllItems();
    }
}
