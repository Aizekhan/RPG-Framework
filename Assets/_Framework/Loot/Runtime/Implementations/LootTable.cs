using System.Collections.Generic;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Default implementation of ILootTable.
    /// Immutable loot table definition.
    /// </summary>
    public class LootTable : ILootTable
    {
        public string TableId { get; }
        public string Name { get; }
        public IReadOnlyList<ILootEntry> Entries { get; }
        public int MinDrops { get; }
        public int MaxDrops { get; }
        public bool AllowDuplicates { get; }
        public IReadOnlyList<string> GuaranteedDrops { get; }

        /// <summary>
        /// Create a new loot table.
        /// </summary>
        /// <param name="tableId">Unique table identifier</param>
        /// <param name="name">Display name</param>
        /// <param name="entries">Loot entries (can be null or empty)</param>
        /// <param name="minDrops">Minimum drops (default: 1)</param>
        /// <param name="maxDrops">Maximum drops (default: 3)</param>
        /// <param name="allowDuplicates">Allow duplicate items (default: false)</param>
        /// <param name="guaranteedDrops">Items that always drop (can be null)</param>
        public LootTable(
            string tableId,
            string name,
            IReadOnlyList<ILootEntry> entries,
            int minDrops = 1,
            int maxDrops = 3,
            bool allowDuplicates = false,
            IReadOnlyList<string> guaranteedDrops = null)
        {
            TableId = tableId;
            Name = name;
            Entries = entries ?? new List<ILootEntry>();
            MinDrops = minDrops;
            MaxDrops = maxDrops;
            AllowDuplicates = allowDuplicates;
            GuaranteedDrops = guaranteedDrops ?? new List<string>();
        }
    }
}
