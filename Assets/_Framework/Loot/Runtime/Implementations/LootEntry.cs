namespace RPGFramework.Loot
{
    /// <summary>
    /// Default implementation of ILootEntry.
    /// Immutable loot entry definition.
    /// </summary>
    public class LootEntry : ILootEntry
    {
        public string ItemId { get; }
        public int Weight { get; }
        public int MinQuantity { get; }
        public int MaxQuantity { get; }
        public float DropChance { get; }
        public int MinLevel { get; }
        public int MaxLevel { get; }

        /// <summary>
        /// Create a new loot entry.
        /// </summary>
        /// <param name="itemId">Item definition ID</param>
        /// <param name="weight">Drop weight (default: 1)</param>
        /// <param name="minQuantity">Minimum quantity (default: 1)</param>
        /// <param name="maxQuantity">Maximum quantity (default: 1)</param>
        /// <param name="dropChance">Drop chance 0.0-1.0 (default: 1.0 = 100%)</param>
        /// <param name="minLevel">Minimum level required (default: 0 = no requirement)</param>
        /// <param name="maxLevel">Maximum level (default: -1 = no maximum)</param>
        public LootEntry(
            string itemId,
            int weight = 1,
            int minQuantity = 1,
            int maxQuantity = 1,
            float dropChance = 1.0f,
            int minLevel = 0,
            int maxLevel = -1)
        {
            ItemId = itemId;
            Weight = weight;
            MinQuantity = minQuantity;
            MaxQuantity = maxQuantity;
            DropChance = dropChance;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
        }
    }
}
