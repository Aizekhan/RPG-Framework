using UnityEngine;
using RPGFramework.Loot;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Tools.LootTableDesigner
{
    /// <summary>
    /// Central registry for all loot table assets.
    /// Provides validation, statistics, and bulk operations.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTableDatabase", menuName = "RPG Framework/Loot Table Database", order = 102)]
    public class LootTableDatabase : ScriptableObject
    {
        [Tooltip("All loot table assets in the project")]
        public List<LootTableAsset> lootTables = new List<LootTableAsset>();

        [Header("Statistics (Read-Only)")]
        [SerializeField, Tooltip("Total number of loot tables")]
        private int totalTables;

        [SerializeField, Tooltip("Total number of loot entries across all tables")]
        private int totalEntries;

        [SerializeField, Tooltip("Total number of guaranteed drops across all tables")]
        private int totalGuaranteedDrops;

        [SerializeField, Tooltip("Total number of unique items referenced")]
        private int uniqueItems;

        /// <summary>
        /// Gets loot table by ID.
        /// </summary>
        public LootTableAsset GetLootTable(string tableId)
        {
            return lootTables?.FirstOrDefault(t => t != null && t.tableId == tableId);
        }

        /// <summary>
        /// Converts all loot tables to Framework ILootTable array.
        /// </summary>
        public ILootTable[] ToLootTableArray()
        {
            return lootTables?
                .Where(t => t != null)
                .Select(t => t.ToLootTable())
                .ToArray() ?? System.Array.Empty<ILootTable>();
        }

        /// <summary>
        /// Creates a LootTableRegistry from this database.
        /// </summary>
        public ILootTableRegistry ToLootTableRegistry()
        {
            var registry = new LootTableRegistry();
            var tables = ToLootTableArray();

            foreach (var table in tables)
            {
                registry.RegisterTable(table);
            }

            return registry;
        }

        /// <summary>
        /// Validates all loot tables in the database.
        /// </summary>
        public List<string> ValidateDatabase()
        {
            var errors = new List<string>();

            if (lootTables == null || lootTables.Count == 0)
            {
                errors.Add("Database is empty - no loot tables registered");
                return errors;
            }

            // Check for null tables
            var nullTables = lootTables.Count(t => t == null);
            if (nullTables > 0)
            {
                errors.Add($"{nullTables} null loot table(s) in database");
            }

            // Check for duplicate table IDs
            var duplicateIds = lootTables
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.tableId))
                .GroupBy(t => t.tableId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                errors.Add($"Duplicate table IDs: {string.Join(", ", duplicateIds)}");
            }

            // Validate individual tables
            foreach (var table in lootTables.Where(t => t != null))
            {
                var tableErrors = table.Validate();
                if (tableErrors.Any())
                {
                    errors.Add($"Table '{table.tableName}' ({table.tableId}):");
                    errors.AddRange(tableErrors.Select(e => $"  - {e}"));
                }
            }

            return errors;
        }

        /// <summary>
        /// Updates database statistics.
        /// </summary>
        public void UpdateStatistics()
        {
            if (lootTables == null)
            {
                totalTables = 0;
                totalEntries = 0;
                totalGuaranteedDrops = 0;
                uniqueItems = 0;
                return;
            }

            var validTables = lootTables.Where(t => t != null).ToList();

            totalTables = validTables.Count;
            totalEntries = validTables.Sum(t => t.entries?.Count ?? 0);
            totalGuaranteedDrops = validTables.Sum(t => t.guaranteedDrops?.Count ?? 0);

            // Count unique item IDs across all tables
            var allItemIds = new HashSet<string>();
            foreach (var table in validTables)
            {
                if (table.entries != null)
                {
                    foreach (var entry in table.entries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.itemId)))
                    {
                        allItemIds.Add(entry.itemId);
                    }
                }

                if (table.guaranteedDrops != null)
                {
                    foreach (var itemId in table.guaranteedDrops.Where(id => !string.IsNullOrWhiteSpace(id)))
                    {
                        allItemIds.Add(itemId);
                    }
                }
            }

            uniqueItems = allItemIds.Count;
        }

        /// <summary>
        /// Sorts loot tables alphabetically by table ID.
        /// </summary>
        public void SortByTableId()
        {
            if (lootTables == null) return;

            lootTables = lootTables
                .OrderBy(t => t == null ? "" : t.tableId)
                .ToList();
        }

        /// <summary>
        /// Sorts loot tables alphabetically by table name.
        /// </summary>
        public void SortByTableName()
        {
            if (lootTables == null) return;

            lootTables = lootTables
                .OrderBy(t => t == null ? "" : t.tableName)
                .ToList();
        }

        /// <summary>
        /// Removes null entries from loot tables list.
        /// </summary>
        public void RemoveNullEntries()
        {
            if (lootTables == null) return;

            lootTables.RemoveAll(t => t == null);
        }

        /// <summary>
        /// Gets statistics summary text.
        /// </summary>
        public string GetStatisticsSummary()
        {
            return $"Tables: {totalTables} | Entries: {totalEntries} | Guaranteed: {totalGuaranteedDrops} | Unique Items: {uniqueItems}";
        }

        private void OnValidate()
        {
            UpdateStatistics();
        }
    }
}
