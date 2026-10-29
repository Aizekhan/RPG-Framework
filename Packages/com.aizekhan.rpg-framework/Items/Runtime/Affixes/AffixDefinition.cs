using System;
using System.Collections.Generic;
using RPGFramework.Stats;

namespace RPGFramework.Items
{
    /// <summary>
    /// Where an affix's name goes: before the item's name ("Sturdy") or after it ("of the Bear").
    /// </summary>
    public enum AffixSlot
    {
        Prefix = 0,
        Suffix = 1
    }

    /// <summary>
    /// A random bonus an item can roll ("+5..12 Strength", named "of the Bear"): which stat, how much, how often, from
    /// which item level, on which kinds of item. Affixes sharing a <see cref="Group"/> exclude each other on one item.
    /// </summary>
    public sealed class AffixDefinition
    {
        private readonly HashSet<string> _tags = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="id">Saved with the item; letters, digits, '_', '-' and '.' only.</param>
        /// <param name="name">The word in the item's name ("Sturdy", "of the Bear"); a key for a game that translates.</param>
        /// <param name="stat">The stat's name, as the game's stat enum spells it.</param>
        public AffixDefinition(string id, AffixSlot slot, string name, string stat, float min, float max,
            StatModifierType modifierType = StatModifierType.Additive)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("An affix needs an id.", nameof(id));
            foreach (char c in id)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                    throw new ArgumentException("Affix id '" + id + "': letters, digits, '_', '-' and '.' only.", nameof(id));
            }
            if (max < min)
                throw new ArgumentException("Affix '" + id + "': max is below min.", nameof(max));
            Id = id;
            Slot = slot;
            Name = name ?? "";
            Stat = stat ?? "";
            Min = min;
            Max = max;
            ModifierType = modifierType;
        }

        public string Id { get; }
        public AffixSlot Slot { get; }
        public string Name { get; }
        public string Stat { get; }
        public float Min { get; }
        public float Max { get; }
        public StatModifierType ModifierType { get; }

        /// <summary>
        /// How often it comes up against the other affixes that fit (0: never rolled).
        /// </summary>
        public int Weight { get; set; } = 100;

        public int MinItemLevel { get; set; } = 1;
        public int MaxItemLevel { get; set; } = int.MaxValue;

        /// <summary>
        /// Affixes of one group exclude each other (two tiers of Strength); null: only the affix itself.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Rolled values land on multiples of it from <see cref="Min"/> (1: whole steps); 0 keeps any value.
        /// </summary>
        public float Step { get; set; } = 1f;

        /// <summary>
        /// The kinds of item it can roll on ("weapon", "ring"); none: any item.
        /// </summary>
        public IReadOnlyCollection<string> Tags => _tags;

        public AffixDefinition OnlyOn(params string[] tags)
        {
            foreach (var tag in tags)
            {
                if (!string.IsNullOrEmpty(tag))
                    _tags.Add(tag);
            }
            return this;
        }

        internal string ExclusionKey => Group ?? "#" + Id;

        /// <summary>
        /// Whether it can roll on an item of this level and these kinds (an item without kinds takes any affix).
        /// </summary>
        public bool Fits(int itemLevel, IReadOnlyCollection<string> itemTags)
        {
            if (Weight <= 0 || itemLevel < MinItemLevel || itemLevel > MaxItemLevel)
                return false;
            if (_tags.Count == 0 || itemTags == null || itemTags.Count == 0)
                return true;
            foreach (var tag in itemTags)
            {
                if (_tags.Contains(tag))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A value between <see cref="Min"/> and <see cref="Max"/>, on a <see cref="Step"/>.
        /// </summary>
        public float RollValue(Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (Step > 0f)
            {
                int steps = (int)Math.Floor((Max - Min) / Step + 1e-4);
                return Min + Step * random.Next(steps + 1);
            }
            return Min + (float)random.NextDouble() * (Max - Min);
        }
    }
}
