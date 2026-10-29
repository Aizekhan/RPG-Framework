using System;
using System.Collections.Generic;

namespace RPGFramework.Items
{
    /// <summary>
    /// How often a dropped item comes out of each rarity, then <see cref="AffixPool.Roll"/> gives it that rarity's
    /// affixes. Luck (magic find) makes the rarer tiers likelier: a rarity's weight grows by luck times its tier
    /// (Uncommon 1 .. Mythic 5), Common's stays.
    /// </summary>
    public sealed class RarityTable
    {
        private readonly Dictionary<ItemRarity, int> _weights = new Dictionary<ItemRarity, int>();

        /// <summary>
        /// A table with weights Common 600, Uncommon 250, Rare 100, Epic 40, Legendary 9, Mythic 1.
        /// </summary>
        public static RarityTable Default() => new RarityTable()
            .Set(ItemRarity.Common, 600)
            .Set(ItemRarity.Uncommon, 250)
            .Set(ItemRarity.Rare, 100)
            .Set(ItemRarity.Epic, 40)
            .Set(ItemRarity.Legendary, 9)
            .Set(ItemRarity.Mythic, 1);

        /// <param name="weight">0 takes the rarity out.</param>
        public RarityTable Set(ItemRarity rarity, int weight)
        {
            if (weight < 0)
                throw new ArgumentOutOfRangeException(nameof(weight), "A weight cannot be negative.");
            if (weight == 0)
                _weights.Remove(rarity);
            else
                _weights[rarity] = weight;
            return this;
        }

        public int WeightOf(ItemRarity rarity) => _weights.TryGetValue(rarity, out int weight) ? weight : 0;

        /// <summary>
        /// The chance of a rarity with this luck, 0..1.
        /// </summary>
        public double ChanceOf(ItemRarity rarity, float luck = 0f)
        {
            double total = 0;
            foreach (var pair in _weights)
                total += Weighted(pair.Key, pair.Value, luck);
            return total <= 0 ? 0 : Weighted(rarity, WeightOf(rarity), luck) / total;
        }

        /// <param name="luck">0: the table as written; 1: Uncommon twice as likely, Mythic six times, against Common.</param>
        /// <returns>Common when the table is empty.</returns>
        public ItemRarity Roll(Random random, float luck = 0f)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            double total = 0;
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
                total += Weighted(rarity, WeightOf(rarity), luck);
            if (total <= 0)
                return ItemRarity.Common;
            double pick = random.NextDouble() * total;
            ItemRarity last = ItemRarity.Common;
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                double weight = Weighted(rarity, WeightOf(rarity), luck);
                if (weight <= 0)
                    continue;
                last = rarity;
                if (pick < weight)
                    return rarity;
                pick -= weight;
            }
            return last;
        }

        private static double Weighted(ItemRarity rarity, int weight, float luck) =>
            weight * (1.0 + Math.Max(0f, luck) * (int)rarity);
    }
}
