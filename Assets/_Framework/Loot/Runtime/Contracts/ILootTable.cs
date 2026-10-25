using System.Collections.Generic;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Loot table containing multiple loot entries.
    /// Used for specific enemies, chests, bosses, etc.
    /// </summary>
    public interface ILootTable
    {
        /// <summary>
        /// Unique identifier for this loot table.
        /// Used for lookups in loot table registry.
        /// </summary>
        string TableId { get; }

        /// <summary>
        /// Display name for debugging/UI.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// All loot entries in this table.
        /// Each entry defines a possible item drop.
        /// </summary>
        IReadOnlyList<ILootEntry> Entries { get; }

        /// <summary>
        /// Minimum number of items to drop.
        /// Actual drop count is randomly selected between min and max.
        /// </summary>
        int MinDrops { get; }

        /// <summary>
        /// Maximum number of items to drop.
        /// Actual drop count is randomly selected between min and max.
        /// </summary>
        int MaxDrops { get; }

        /// <summary>
        /// Whether items can be duplicated in a single roll.
        /// True = same item can drop multiple times in one generation.
        /// False = each item can only drop once per generation.
        /// </summary>
        bool AllowDuplicates { get; }

        /// <summary>
        /// Guaranteed item IDs that always drop.
        /// Used for quest items, boss-specific drops, etc.
        /// These bypass weight/chance calculations.
        /// </summary>
        IReadOnlyList<string> GuaranteedDrops { get; }
    }
}
