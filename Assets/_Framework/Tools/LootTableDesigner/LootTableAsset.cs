using UnityEngine;
using RPGFramework.Loot;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Tools.LootTableDesigner
{
    /// <summary>
    /// ScriptableObject for visual loot table authoring.
    /// Enables creation and management of loot tables through Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "RPG Framework/Loot Table", order = 101)]
    public class LootTableAsset : ScriptableObject
    {
        [Header("Table Identity")]
        [Tooltip("Unique identifier for this loot table")]
        public string tableId = "loot_table_new";

        [Tooltip("Display name for debugging/UI")]
        public string tableName = "New Loot Table";

        [Header("Drop Configuration")]
        [Tooltip("Minimum number of items to drop")]
        [Min(0)]
        public int minDrops = 1;

        [Tooltip("Maximum number of items to drop")]
        [Min(1)]
        public int maxDrops = 3;

        [Tooltip("Whether items can be duplicated in a single roll")]
        public bool allowDuplicates = true;

        [Header("Loot Entries")]
        [Tooltip("Possible items that can drop from this table")]
        public List<LootEntryData> entries = new List<LootEntryData>
        {
            new LootEntryData()
        };

        [Header("Guaranteed Drops")]
        [Tooltip("Item IDs that always drop (bypasses weight/chance calculations)")]
        public List<string> guaranteedDrops = new List<string>();

        /// <summary>
        /// Converts ScriptableObject to Framework ILootTable implementation.
        /// </summary>
        public ILootTable ToLootTable()
        {
            var lootEntries = entries?
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.itemId))
                .Select(e => e.ToLootEntry())
                .ToList() ?? new List<ILootEntry>();

            return new LootTable(
                tableId: tableId,
                name: tableName,
                entries: lootEntries,
                minDrops: minDrops,
                maxDrops: maxDrops,
                allowDuplicates: allowDuplicates,
                guaranteedDrops: guaranteedDrops ?? new List<string>()
            );
        }

        /// <summary>
        /// Validates loot table configuration.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(tableId))
                errors.Add("Table ID cannot be empty");

            if (string.IsNullOrWhiteSpace(tableName))
                errors.Add("Table Name cannot be empty");

            if (maxDrops < minDrops)
                errors.Add($"Max Drops ({maxDrops}) cannot be less than Min Drops ({minDrops})");

            if (minDrops < 0)
                errors.Add("Min Drops cannot be negative");

            if (entries == null || entries.Count == 0)
                errors.Add("Loot table must have at least one entry");

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null) continue;

                    if (string.IsNullOrWhiteSpace(entry.itemId))
                        errors.Add($"Entry {i}: Item ID is required");

                    if (entry.weight < 0)
                        errors.Add($"Entry {i} ({entry.itemId}): Weight cannot be negative");

                    if (entry.dropChance < 0f || entry.dropChance > 1f)
                        errors.Add($"Entry {i} ({entry.itemId}): Drop chance must be between 0 and 1");

                    if (entry.maxQuantity < entry.minQuantity)
                        errors.Add($"Entry {i} ({entry.itemId}): Max quantity cannot be less than min quantity");

                    if (entry.minLevel < 0)
                        errors.Add($"Entry {i} ({entry.itemId}): Min level cannot be negative");
                }

                // Check for duplicate item IDs
                var duplicateItems = entries
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.itemId))
                    .GroupBy(e => e.itemId)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateItems.Any())
                {
                    errors.Add($"Duplicate item IDs found: {string.Join(", ", duplicateItems)}");
                }
            }

            // Check guaranteed drops for duplicates
            if (guaranteedDrops != null)
            {
                var duplicateGuaranteed = guaranteedDrops
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .GroupBy(id => id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateGuaranteed.Any())
                {
                    errors.Add($"Duplicate guaranteed drops: {string.Join(", ", duplicateGuaranteed)}");
                }
            }

            return errors;
        }

        /// <summary>
        /// Calculates total weight for drop rate percentages.
        /// </summary>
        public int GetTotalWeight()
        {
            return entries?.Sum(e => e?.weight ?? 0) ?? 0;
        }

        /// <summary>
        /// Gets drop rate percentage for a specific entry.
        /// </summary>
        public float GetDropRatePercentage(LootEntryData entry)
        {
            var totalWeight = GetTotalWeight();
            if (totalWeight == 0) return 0f;
            return (entry.weight / (float)totalWeight) * 100f;
        }

        private void OnValidate()
        {
            // Ensure IDs are sanitized
            if (!string.IsNullOrWhiteSpace(tableId))
            {
                tableId = tableId.ToLower().Replace(" ", "_");
            }

            // Ensure min/max drops are valid
            if (minDrops < 0) minDrops = 0;
            if (maxDrops < 1) maxDrops = 1;
            if (maxDrops < minDrops) maxDrops = minDrops;

            // Ensure at least one entry exists
            if (entries == null || entries.Count == 0)
            {
                entries = new List<LootEntryData> { new LootEntryData() };
            }
        }
    }

    /// <summary>
    /// Serializable loot entry data for Inspector editing.
    /// </summary>
    [System.Serializable]
    public class LootEntryData
    {
        [Header("Item")]
        [Tooltip("Item definition ID to drop")]
        public string itemId = "item_id";

        [Header("Drop Weight")]
        [Tooltip("Higher weight = more likely to drop relative to other entries")]
        [Min(0)]
        public int weight = 100;

        [Tooltip("Drop chance percentage after being selected (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float dropChance = 1f;

        [Header("Quantity")]
        [Tooltip("Minimum quantity to drop")]
        [Min(1)]
        public int minQuantity = 1;

        [Tooltip("Maximum quantity to drop")]
        [Min(1)]
        public int maxQuantity = 1;

        [Header("Level Requirements")]
        [Tooltip("Minimum player/enemy level required (0 = no requirement)")]
        [Min(0)]
        public int minLevel = 0;

        [Tooltip("Maximum player/enemy level (-1 = no maximum)")]
        public int maxLevel = -1;

        /// <summary>
        /// Converts to Framework ILootEntry implementation.
        /// </summary>
        public ILootEntry ToLootEntry()
        {
            return new LootEntry(
                itemId: itemId,
                weight: weight,
                minQuantity: minQuantity,
                maxQuantity: maxQuantity,
                dropChance: dropChance,
                minLevel: minLevel,
                maxLevel: maxLevel
            );
        }
    }
}
