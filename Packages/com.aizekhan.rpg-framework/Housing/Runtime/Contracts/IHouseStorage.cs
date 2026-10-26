using System;
using System.Collections.Generic;

namespace RPGFramework.Housing.Runtime.Contracts
{
    /// <summary>
    /// Manages storage containers within houses (linked to furniture items).
    /// </summary>
    public interface IHouseStorage
    {
        /// <summary>
        /// ID of the house this storage belongs to.
        /// </summary>
        Guid HouseId { get; }

        /// <summary>
        /// ID of the furniture providing storage.
        /// </summary>
        Guid FurnitureId { get; }

        /// <summary>
        /// Maximum number of items this storage can hold.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Current number of items stored.
        /// </summary>
        int CurrentItemCount { get; }

        /// <summary>
        /// All item IDs currently stored.
        /// </summary>
        IReadOnlyList<Guid> StoredItemIds { get; }

        /// <summary>
        /// Whether the storage is full.
        /// </summary>
        bool IsFull { get; }

        /// <summary>
        /// Add an item to storage.
        /// </summary>
        bool TryAddItem(Guid itemId);

        /// <summary>
        /// Remove an item from storage.
        /// </summary>
        bool TryRemoveItem(Guid itemId);

        /// <summary>
        /// Check if an item is stored here.
        /// </summary>
        bool ContainsItem(Guid itemId);

        /// <summary>
        /// Clear all items from storage.
        /// </summary>
        void ClearStorage();
    }
}
