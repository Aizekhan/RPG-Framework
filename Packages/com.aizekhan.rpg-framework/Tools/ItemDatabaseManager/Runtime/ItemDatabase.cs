using UnityEngine;
using RPGFramework.Items;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Tools.ItemDatabaseManager
{
    /// <summary>
    /// Central registry for all item assets.
    /// Provides validation, statistics, and filtering.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "RPG Framework/Item Database", order = 104)]
    public class ItemDatabase : ScriptableObject
    {
        [Tooltip("All item assets in the project")]
        public List<ItemAsset> items = new List<ItemAsset>();

        [Header("Statistics (Read-Only)")]
        [SerializeField, Tooltip("Total number of items")]
        private int totalItems;

        [SerializeField, Tooltip("Items by type")]
        private string itemsByType;

        [SerializeField, Tooltip("Items by rarity")]
        private string itemsByRarity;

        [SerializeField, Tooltip("Total unique stat modifiers")]
        private int uniqueStats;

        /// <summary>
        /// Gets item by ID.
        /// </summary>
        public ItemAsset GetItem(string itemId)
        {
            return items?.FirstOrDefault(i => i != null && i.itemId == itemId);
        }

        /// <summary>
        /// Gets items by type.
        /// </summary>
        public List<ItemAsset> GetItemsByType(ItemType itemType)
        {
            return items?.Where(i => i != null && i.itemType == itemType).ToList()
                ?? new List<ItemAsset>();
        }

        /// <summary>
        /// Gets items by rarity.
        /// </summary>
        public List<ItemAsset> GetItemsByRarity(ItemRarity rarity)
        {
            return items?.Where(i => i != null && i.rarity == rarity).ToList()
                ?? new List<ItemAsset>();
        }

        /// <summary>
        /// Converts all items to Framework IItem array.
        /// </summary>
        public IItem[] ToItemArray()
        {
            return items?
                .Where(i => i != null)
                .Select(i => i.ToItem())
                .ToArray() ?? System.Array.Empty<IItem>();
        }

        /// <summary>
        /// Creates an IItemRegistry from this database.
        /// </summary>
        public IItemRegistry ToItemRegistry()
        {
            var registry = new ItemRegistry();
            var itemDefinitions = ToItemArray();

            foreach (var item in itemDefinitions)
            {
                registry.RegisterItem(item);
            }

            return registry;
        }

        /// <summary>
        /// Validates all items in the database.
        /// </summary>
        public List<string> ValidateDatabase()
        {
            var errors = new List<string>();

            if (items == null || items.Count == 0)
            {
                errors.Add("Database is empty - no items registered");
                return errors;
            }

            // Check for null items
            var nullItems = items.Count(i => i == null);
            if (nullItems > 0)
            {
                errors.Add($"{nullItems} null item(s) in database");
            }

            // Check for duplicate item IDs
            var duplicateIds = items
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.itemId))
                .GroupBy(i => i.itemId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                errors.Add($"Duplicate item IDs: {string.Join(", ", duplicateIds)}");
            }

            // Validate individual items
            foreach (var item in items.Where(i => i != null))
            {
                var itemErrors = item.Validate();
                if (itemErrors.Any())
                {
                    errors.Add($"Item '{item.itemName}' ({item.itemId}):");
                    errors.AddRange(itemErrors.Select(e => $"  - {e}"));
                }
            }

            return errors;
        }

        /// <summary>
        /// Updates database statistics.
        /// </summary>
        public void UpdateStatistics()
        {
            if (items == null)
            {
                totalItems = 0;
                itemsByType = "None";
                itemsByRarity = "None";
                uniqueStats = 0;
                return;
            }

            var validItems = items.Where(i => i != null).ToList();
            totalItems = validItems.Count;

            // Count by type
            var typeGroups = validItems
                .GroupBy(i => i.itemType)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Count()}");
            itemsByType = string.Join(" | ", typeGroups);

            // Count by rarity
            var rarityGroups = validItems
                .GroupBy(i => i.rarity)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Count()}");
            itemsByRarity = string.Join(" | ", rarityGroups);

            // Count unique stat names
            var allStats = new HashSet<string>();
            foreach (var item in validItems.Where(i => i.statModifiers != null))
            {
                foreach (var stat in item.statModifiers.Where(s => !string.IsNullOrWhiteSpace(s.statName)))
                {
                    allStats.Add(stat.statName);
                }
            }
            uniqueStats = allStats.Count;
        }

        /// <summary>
        /// Sorts items alphabetically by item ID.
        /// </summary>
        public void SortByItemId()
        {
            if (items == null) return;
            items = items.OrderBy(i => i == null ? "" : i.itemId).ToList();
        }

        /// <summary>
        /// Sorts items alphabetically by item name.
        /// </summary>
        public void SortByItemName()
        {
            if (items == null) return;
            items = items.OrderBy(i => i == null ? "" : i.itemName).ToList();
        }

        /// <summary>
        /// Sorts items by type, then rarity.
        /// </summary>
        public void SortByTypeAndRarity()
        {
            if (items == null) return;
            items = items
                .OrderBy(i => i == null ? ItemType.Miscellaneous : i.itemType)
                .ThenBy(i => i == null ? ItemRarity.Common : i.rarity)
                .ToList();
        }

        /// <summary>
        /// Removes null entries from items list.
        /// </summary>
        public void RemoveNullEntries()
        {
            if (items == null) return;
            items.RemoveAll(i => i == null);
        }

        /// <summary>
        /// Gets statistics summary text.
        /// </summary>
        public string GetStatisticsSummary()
        {
            return $"Total: {totalItems} | By Type: {itemsByType} | By Rarity: {itemsByRarity} | Unique Stats: {uniqueStats}";
        }

        private void OnValidate()
        {
            UpdateStatistics();
        }
    }
}
