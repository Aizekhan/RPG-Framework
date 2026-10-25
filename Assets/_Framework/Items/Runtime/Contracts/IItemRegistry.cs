using System.Collections.Generic;

namespace RPGFramework.Items
{
    /// <summary>
    /// Item definition registry for centralized lookups.
    /// Game layer populates with definitions from data sources (ScriptableObjects, JSON, database).
    /// Acts as single source of truth for all item definitions in the game.
    /// </summary>
    public interface IItemRegistry
    {
        /// <summary>
        /// Get item definition by ID.
        /// </summary>
        /// <param name="itemId">Unique item identifier</param>
        /// <returns>Item definition, or null if not found</returns>
        IItem GetItem(string itemId);

        /// <summary>
        /// Check if item exists in registry.
        /// </summary>
        /// <param name="itemId">Unique item identifier</param>
        /// <returns>True if item registered, false otherwise</returns>
        bool HasItem(string itemId);

        /// <summary>
        /// Get all items of a specific type.
        /// </summary>
        /// <param name="itemType">Item type to filter by</param>
        /// <returns>Read-only list of items matching the type</returns>
        IReadOnlyList<IItem> GetItemsByType(ItemType itemType);

        /// <summary>
        /// Get all items of a specific rarity.
        /// </summary>
        /// <param name="rarity">Rarity tier to filter by</param>
        /// <returns>Read-only list of items matching the rarity</returns>
        IReadOnlyList<IItem> GetItemsByRarity(ItemRarity rarity);

        /// <summary>
        /// Get all items of a specific quality.
        /// </summary>
        /// <param name="quality">Quality tier to filter by</param>
        /// <returns>Read-only list of items matching the quality</returns>
        IReadOnlyList<IItem> GetItemsByQuality(ItemQuality quality);

        /// <summary>
        /// Get all registered items.
        /// </summary>
        /// <returns>Read-only list of all items in registry</returns>
        IReadOnlyList<IItem> GetAllItems();

        /// <summary>
        /// Register a new item definition.
        /// Replaces existing item if same ItemId already registered.
        /// </summary>
        /// <param name="item">Item definition to register</param>
        void RegisterItem(IItem item);

        /// <summary>
        /// Unregister an item definition.
        /// </summary>
        /// <param name="itemId">Item ID to remove</param>
        /// <returns>True if item was removed, false if not found</returns>
        bool UnregisterItem(string itemId);

        /// <summary>
        /// Clear all registered items.
        /// Use with caution - typically only needed for testing or full reloads.
        /// </summary>
        void Clear();
    }
}
