using System;
using System.Collections.Generic;
using RPGFramework.Stats;

namespace RPGFramework.Items
{
    /// <summary>
    /// The affixes items can roll, and how many a rarity gets: <see cref="Roll"/> picks them by weight among those that
    /// fit the item's level and kind, one per group, then rolls their values. The same seed rolls the same item.
    /// Rolled affixes live on the item instance (<see cref="ItemAffixes"/>) and turn into stat modifiers when worn
    /// (<see cref="ModifiersFor{TStatType}"/>).
    /// </summary>
    public sealed class AffixPool
    {
        private readonly Dictionary<string, AffixDefinition> _affixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
        private readonly List<AffixDefinition> _ordered = new List<AffixDefinition>();
        private readonly Dictionary<ItemRarity, (int Total, int Prefixes, int Suffixes)> _counts =
            new Dictionary<ItemRarity, (int Total, int Prefixes, int Suffixes)>
            {
                [ItemRarity.Common] = (0, 0, 0),
                [ItemRarity.Uncommon] = (1, 1, 1),
                [ItemRarity.Rare] = (2, 1, 1),
                [ItemRarity.Epic] = (3, 2, 2),
                [ItemRarity.Legendary] = (4, 2, 2),
                [ItemRarity.Mythic] = (6, 3, 3)
            };

        public IReadOnlyList<AffixDefinition> All => _ordered;

        public AffixPool Add(AffixDefinition affix)
        {
            if (affix == null)
                throw new ArgumentNullException(nameof(affix));
            if (_affixes.ContainsKey(affix.Id))
                throw new ArgumentException("Affix '" + affix.Id + "' is already in the pool.", nameof(affix));
            _affixes[affix.Id] = affix;
            _ordered.Add(affix);
            return this;
        }

        public AffixDefinition Find(string id) =>
            id != null && _affixes.TryGetValue(id, out var affix) ? affix : null;

        /// <summary>
        /// How many affixes a rarity rolls, and at most how many of them are prefixes and suffixes.
        /// Defaults: Common 0, Uncommon 1, Rare 2, Epic 3, Legendary 4, Mythic 6.
        /// </summary>
        public AffixPool SetCount(ItemRarity rarity, int total, int maxPrefixes, int maxSuffixes)
        {
            if (total < 0 || maxPrefixes < 0 || maxSuffixes < 0)
                throw new ArgumentOutOfRangeException(nameof(total), "Counts cannot be negative.");
            _counts[rarity] = (total, maxPrefixes, maxSuffixes);
            return this;
        }

        public int CountFor(ItemRarity rarity) => _counts.TryGetValue(rarity, out var count) ? count.Total : 0;

        /// <summary>
        /// Rolls the affixes of a new item. Fewer come out when too few fit (a low level, a narrow kind).
        /// </summary>
        /// <param name="itemTags">The item's kinds ("weapon", "sword"); null or none: any affix fits.</param>
        public List<RolledAffix> Roll(Random random, int itemLevel, ItemRarity rarity, IReadOnlyCollection<string> itemTags = null)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            var rolled = new List<RolledAffix>();
            if (!_counts.TryGetValue(rarity, out var count))
                return rolled;

            var taken = new HashSet<string>(StringComparer.Ordinal);
            int prefixes = 0, suffixes = 0;
            var candidates = new List<AffixDefinition>();
            for (int i = 0; i < count.Total; i++)
            {
                candidates.Clear();
                int totalWeight = 0;
                foreach (var affix in _ordered)
                {
                    if (taken.Contains(affix.ExclusionKey) || !affix.Fits(itemLevel, itemTags))
                        continue;
                    if (affix.Slot == AffixSlot.Prefix ? prefixes >= count.Prefixes : suffixes >= count.Suffixes)
                        continue;
                    candidates.Add(affix);
                    totalWeight += affix.Weight;
                }
                if (candidates.Count == 0)
                    break;

                int pick = random.Next(totalWeight);
                var chosen = candidates[candidates.Count - 1];
                foreach (var candidate in candidates)
                {
                    if (pick < candidate.Weight)
                    {
                        chosen = candidate;
                        break;
                    }
                    pick -= candidate.Weight;
                }

                taken.Add(chosen.ExclusionKey);
                if (chosen.Slot == AffixSlot.Prefix)
                    prefixes++;
                else
                    suffixes++;
                rolled.Add(new RolledAffix(chosen.Id, chosen.RollValue(random)));
            }
            return rolled;
        }

        /// <summary>
        /// The same affixes with new values (a reforge); affixes no longer in the pool keep theirs.
        /// </summary>
        public List<RolledAffix> RerollValues(Random random, IEnumerable<RolledAffix> affixes)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            var rerolled = new List<RolledAffix>();
            foreach (var affix in affixes ?? Array.Empty<RolledAffix>())
            {
                var definition = Find(affix.AffixId);
                rerolled.Add(definition == null ? affix : new RolledAffix(affix.AffixId, definition.RollValue(random)));
            }
            return rerolled;
        }

        /// <summary>
        /// "Prefix Base Suffix": the first prefix and the first suffix rolled name the item.
        /// </summary>
        /// <param name="translate">Turns an affix's name into the player's language (its name is a key); null: as written.</param>
        public string NameFor(string baseName, IEnumerable<RolledAffix> affixes, Func<string, string> translate = null)
        {
            string prefix = null, suffix = null;
            foreach (var affix in affixes ?? Array.Empty<RolledAffix>())
            {
                var definition = Find(affix.AffixId);
                if (definition == null || definition.Name.Length == 0)
                    continue;
                if (definition.Slot == AffixSlot.Prefix && prefix == null)
                    prefix = translate?.Invoke(definition.Name) ?? definition.Name;
                else if (definition.Slot == AffixSlot.Suffix && suffix == null)
                    suffix = translate?.Invoke(definition.Name) ?? definition.Name;
            }
            string name = baseName ?? "";
            if (!string.IsNullOrEmpty(prefix))
                name = prefix + " " + name;
            if (!string.IsNullOrEmpty(suffix))
                name = name + " " + suffix;
            return name;
        }

        /// <summary>
        /// The stat modifiers the affixes give, with ids "sourceId:affixId" (the source: the item instance), so the
        /// game takes them off with the item. Affixes not in the pool, or for a stat the enum does not have, give none.
        /// </summary>
        public List<StatModifier<TStatType, float>> ModifiersFor<TStatType>(string sourceId, IEnumerable<RolledAffix> affixes)
            where TStatType : struct, Enum, IConvertible
        {
            if (string.IsNullOrEmpty(sourceId))
                throw new ArgumentException("Modifiers need a source id (the item instance).", nameof(sourceId));
            var modifiers = new List<StatModifier<TStatType, float>>();
            foreach (var affix in affixes ?? Array.Empty<RolledAffix>())
            {
                var definition = Find(affix.AffixId);
                if (definition == null || !Enum.TryParse(definition.Stat, out TStatType stat))
                    continue;
                modifiers.Add(new StatModifier<TStatType, float>(sourceId + ":" + affix.AffixId, stat, affix.Value, definition.ModifierType));
            }
            return modifiers;
        }
    }
}
