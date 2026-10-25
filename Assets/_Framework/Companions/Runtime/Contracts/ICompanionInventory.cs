using System.Collections.Generic;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Manages a companion's equipped items and carried inventory.
    /// Some companions can wear armor/accessories, carry items, or have special equipment slots.
    /// </summary>
    public interface ICompanionInventory
    {
        /// <summary>
        /// Maximum number of items the companion can carry.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Current number of items in inventory.
        /// </summary>
        int ItemCount { get; }

        /// <summary>
        /// Gets all items currently in the companion's inventory.
        /// </summary>
        IReadOnlyList<string> GetAllItems();

        /// <summary>
        /// Adds an item to the companion's inventory.
        /// </summary>
        /// <returns>True if item was added successfully.</returns>
        bool AddItem(string itemId, int quantity = 1);

        /// <summary>
        /// Removes an item from the companion's inventory.
        /// </summary>
        /// <returns>True if item was removed successfully.</returns>
        bool RemoveItem(string itemId, int quantity = 1);

        /// <summary>
        /// Checks if the companion has a specific item.
        /// </summary>
        bool HasItem(string itemId);

        /// <summary>
        /// Gets the quantity of a specific item.
        /// </summary>
        int GetItemQuantity(string itemId);

        /// <summary>
        /// Equips an item to a specific slot (if companion supports equipment).
        /// </summary>
        /// <returns>True if item was equipped successfully.</returns>
        bool EquipItem(string itemId, string slotId);

        /// <summary>
        /// Unequips an item from a specific slot.
        /// </summary>
        /// <returns>The unequipped item ID, or null if slot was empty.</returns>
        string UnequipItem(string slotId);

        /// <summary>
        /// Gets the item equipped in a specific slot.
        /// </summary>
        string GetEquippedItem(string slotId);

        /// <summary>
        /// Clears all items from the companion's inventory.
        /// </summary>
        void Clear();
    }
}
