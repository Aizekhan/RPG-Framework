namespace RPGFramework.Loot
{
    /// <summary>
    /// Loot generator that creates items from loot tables.
    /// Uses weighted random selection with optional modifiers.
    /// </summary>
    public interface ILootGenerator
    {
        /// <summary>
        /// Generate loot from a loot table.
        /// </summary>
        /// <param name="lootTable">Loot table to roll</param>
        /// <param name="level">Level for level-based filtering (default: 1)</param>
        /// <param name="luckModifier">Luck/magic find modifier (default: 1.0 = no bonus, 1.5 = 50% bonus)</param>
        /// <param name="seed">Random seed for deterministic generation (default: time-based)</param>
        /// <returns>Loot generation result with all dropped items</returns>
        LootResult GenerateLoot(
            ILootTable lootTable,
            int level = 1,
            float luckModifier = 1.0f,
            int? seed = null);

        /// <summary>
        /// Generate loot from a loot table by ID.
        /// </summary>
        /// <param name="tableId">Loot table ID from registry</param>
        /// <param name="level">Level for level-based filtering</param>
        /// <param name="luckModifier">Luck/magic find modifier</param>
        /// <param name="seed">Random seed for deterministic generation</param>
        /// <returns>Loot generation result, or default if table not found</returns>
        LootResult GenerateLootById(
            string tableId,
            int level = 1,
            float luckModifier = 1.0f,
            int? seed = null);
    }
}
