using System;
using System.Collections.Generic;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Fluent, validating builder for <see cref="LootTable"/>s defined in code.
    /// <code>
    /// var table = LootTableBuilder.Create("goblin_common", "Common Goblin Loot")
    ///     .WithDrops(1, 3)
    ///     .AddEntry("gold_coins", weight: 100, minQuantity: 5, maxQuantity: 15, dropChance: 0.8f)
    ///     .AddEntry("health_potion", weight: 50, dropChance: 0.3f)
    ///     .AddGuaranteedDrop("exp_orb_small")
    ///     .Build();
    /// </code>
    /// Each <see cref="Build"/> returns an independent table, so a builder can be reused as a template.
    /// </summary>
    public sealed class LootTableBuilder
    {
        private readonly string _tableId;
        private readonly string _name;
        private readonly List<ILootEntry> _entries = new List<ILootEntry>();
        private readonly List<string> _guaranteedDrops = new List<string>();
        private int _minDrops = 1;
        private int _maxDrops = 3;
        private bool _allowDuplicates;

        private LootTableBuilder(string tableId, string name)
        {
            if (string.IsNullOrEmpty(tableId))
                throw new ArgumentException("Loot table id is required", nameof(tableId));
            _tableId = tableId;
            _name = name ?? tableId;
        }

        public static LootTableBuilder Create(string tableId, string name = null) => new LootTableBuilder(tableId, name);

        /// <summary>
        /// Number of rolled drops (guaranteed drops come on top). Default 1–3.
        /// </summary>
        public LootTableBuilder WithDrops(int minDrops, int maxDrops)
        {
            if (minDrops < 0)
                throw new ArgumentOutOfRangeException(nameof(minDrops));
            if (maxDrops < minDrops)
                throw new ArgumentOutOfRangeException(nameof(maxDrops), "maxDrops must be >= minDrops");
            _minDrops = minDrops;
            _maxDrops = maxDrops;
            return this;
        }

        public LootTableBuilder AllowDuplicates(bool allow = true)
        {
            _allowDuplicates = allow;
            return this;
        }

        /// <param name="weight">Relative selection weight (higher = more likely).</param>
        /// <param name="dropChance">Chance (0–1) that a selected entry actually drops.</param>
        /// <param name="maxLevel">-1 = no upper level limit.</param>
        public LootTableBuilder AddEntry(string itemId, int weight = 1, int minQuantity = 1, int maxQuantity = 1,
            float dropChance = 1f, int minLevel = 0, int maxLevel = -1)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item id is required", nameof(itemId));
            if (weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be positive");
            if (minQuantity < 1 || maxQuantity < minQuantity)
                throw new ArgumentOutOfRangeException(nameof(maxQuantity), "Quantities must satisfy 1 <= min <= max");
            if (dropChance < 0f || dropChance > 1f)
                throw new ArgumentOutOfRangeException(nameof(dropChance), "Drop chance must be within 0..1");
            if (minLevel < 0 || (maxLevel >= 0 && maxLevel < minLevel))
                throw new ArgumentOutOfRangeException(nameof(maxLevel), "Levels must satisfy 0 <= min <= max (or max = -1)");

            _entries.Add(new LootEntry(itemId, weight, minQuantity, maxQuantity, dropChance, minLevel, maxLevel));
            return this;
        }

        /// <summary>
        /// Adds an item that drops on every roll, bypassing weights and chances.
        /// </summary>
        public LootTableBuilder AddGuaranteedDrop(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item id is required", nameof(itemId));
            _guaranteedDrops.Add(itemId);
            return this;
        }

        public LootTable Build()
        {
            return new LootTable(_tableId, _name, _entries.ToArray(), _minDrops, _maxDrops, _allowDuplicates, _guaranteedDrops.ToArray());
        }
    }
}
