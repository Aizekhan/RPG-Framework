using System.Collections.Generic;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Result of loot generation.
    /// Contains all items dropped and metadata about the roll.
    /// </summary>
    public struct LootResult
    {
        /// <summary>
        /// All items dropped.
        /// Includes both guaranteed and randomly generated drops.
        /// </summary>
        public List<LootDrop> Drops;

        /// <summary>
        /// Loot table ID that was rolled.
        /// </summary>
        public string TableId;

        /// <summary>
        /// Level used for generation.
        /// Determines which loot entries are valid.
        /// </summary>
        public int Level;

        /// <summary>
        /// Luck modifier applied.
        /// Values greater than 1.0 increase drop rates.
        /// </summary>
        public float LuckModifier;

        /// <summary>
        /// Random seed used for generation.
        /// Allows deterministic loot generation for testing/replays.
        /// </summary>
        public int Seed;

        /// <summary>
        /// Timestamp when loot was generated.
        /// Set by game layer.
        /// </summary>
        public float Timestamp;

        /// <summary>
        /// Total number of items dropped.
        /// </summary>
        public int TotalCount => Drops?.Count ?? 0;

        /// <summary>
        /// Whether any items were dropped.
        /// </summary>
        public bool HasLoot => TotalCount > 0;
    }
}
