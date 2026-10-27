using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Default implementation of ILootGenerator.
    /// Uses weighted random selection with optional luck modifiers.
    /// </summary>
    public class LootGenerator : ILootGenerator
    {
        private readonly ILootTableRegistry _registry;

        /// <summary>
        /// Create a new loot generator.
        /// </summary>
        /// <param name="registry">Loot table registry for lookup by ID</param>
        public LootGenerator(ILootTableRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <inheritdoc/>
        public LootResult GenerateLoot(
            ILootTable lootTable,
            int level = 1,
            float luckModifier = 1.0f,
            int? seed = null)
        {
            if (lootTable == null)
                throw new ArgumentNullException(nameof(lootTable));

            int actualSeed = seed ?? Environment.TickCount;
            var random = new Random(actualSeed);

            var result = new LootResult
            {
                Drops = new List<LootDrop>(),
                TableId = lootTable.TableId,
                Level = level,
                LuckModifier = luckModifier,
                Seed = actualSeed,
                Timestamp = 0f // Game layer sets timestamp
            };

            // Add guaranteed drops
            foreach (var guaranteedId in lootTable.GuaranteedDrops)
            {
                result.Drops.Add(new LootDrop
                {
                    ItemId = guaranteedId,
                    Quantity = 1,
                    SourceEntry = null
                });
            }

            // Filter entries by level
            var validEntries = lootTable.Entries
                .Where(e => level >= e.MinLevel && (e.MaxLevel == -1 || level <= e.MaxLevel))
                .ToList();

            if (validEntries.Count == 0)
                return result;

            // Determine how many items to drop
            int dropCount = random.Next(lootTable.MinDrops, lootTable.MaxDrops + 1);

            // Track dropped items (for duplicate prevention)
            var droppedItems = new HashSet<string>();

            for (int i = 0; i < dropCount; i++)
            {
                // Weighted random selection among the entries still allowed: without duplicates, items already
                // dropped leave the pool, so their weight no longer counts (otherwise a roll could land on nothing)
                float totalWeight = 0f;
                foreach (var entry in validEntries)
                {
                    if (lootTable.AllowDuplicates || !droppedItems.Contains(entry.ItemId))
                        totalWeight += entry.Weight * luckModifier;
                }
                if (totalWeight <= 0f)
                    break;

                float roll = (float)random.NextDouble() * totalWeight;
                float cumulative = 0f;

                foreach (var entry in validEntries)
                {
                    // Skip if duplicates not allowed and item already dropped
                    if (!lootTable.AllowDuplicates && droppedItems.Contains(entry.ItemId))
                        continue;

                    cumulative += entry.Weight * luckModifier;

                    if (roll <= cumulative)
                    {
                        // Drop chance roll
                        float chanceRoll = (float)random.NextDouble();
                        float adjustedChance = Math.Min(1.0f, entry.DropChance * luckModifier);

                        if (chanceRoll <= adjustedChance)
                        {
                            // Determine quantity
                            int quantity = random.Next(entry.MinQuantity, entry.MaxQuantity + 1);

                            result.Drops.Add(new LootDrop
                            {
                                ItemId = entry.ItemId,
                                Quantity = quantity,
                                SourceEntry = entry
                            });

                            droppedItems.Add(entry.ItemId);
                        }

                        break;
                    }
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public LootResult GenerateLootById(
            string tableId,
            int level = 1,
            float luckModifier = 1.0f,
            int? seed = null)
        {
            var table = _registry.GetTable(tableId);
            if (table == null)
                return default(LootResult);

            return GenerateLoot(table, level, luckModifier, seed);
        }
    }
}
