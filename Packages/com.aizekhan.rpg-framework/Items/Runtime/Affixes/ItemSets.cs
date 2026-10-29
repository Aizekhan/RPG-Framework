using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Stats;

namespace RPGFramework.Items
{
    /// <summary>
    /// A bonus an item set gives once enough of its pieces are worn ("2 pieces: +10 Defense").
    /// </summary>
    public sealed class ItemSetBonus
    {
        public ItemSetBonus(int pieces, string stat, float value, StatModifierType modifierType = StatModifierType.Additive)
        {
            if (pieces < 1)
                throw new ArgumentOutOfRangeException(nameof(pieces), pieces, "A set bonus needs at least one piece.");
            Pieces = pieces;
            Stat = stat ?? "";
            Value = value;
            ModifierType = modifierType;
        }

        public int Pieces { get; }
        public string Stat { get; }
        public float Value { get; }
        public StatModifierType ModifierType { get; }
    }

    /// <summary>
    /// Items that belong together ("Wolf Hunter's armour"): wearing several of them gives the set's bonuses for that
    /// many pieces (2, 4...). Rolled items count as their base item (<see cref="RolledItems.BaseOf"/>).
    /// </summary>
    public sealed class ItemSet
    {
        private readonly HashSet<string> _items = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ItemSetBonus> _bonuses = new List<ItemSetBonus>();

        public ItemSet(string id, string name, params string[] itemIds)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("An item set needs an id.", nameof(id));
            Id = id;
            Name = name ?? id;
            foreach (var itemId in itemIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(itemId))
                    _items.Add(itemId);
            }
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyCollection<string> Items => _items;
        public IReadOnlyList<ItemSetBonus> Bonuses => _bonuses;

        public ItemSet WithBonus(int pieces, string stat, float value, StatModifierType modifierType = StatModifierType.Additive)
        {
            _bonuses.Add(new ItemSetBonus(pieces, stat, value, modifierType));
            return this;
        }

        public bool Contains(string itemId) => itemId != null && _items.Contains(RolledItems.BaseOf(itemId));

        /// <summary>
        /// How many different pieces of the set are among the worn items (two of the same ring count once).
        /// </summary>
        public int PiecesIn(IEnumerable<string> wornItemIds) =>
            (wornItemIds ?? Enumerable.Empty<string>()).Where(Contains).Select(RolledItems.BaseOf).Distinct(StringComparer.Ordinal).Count();
    }

    /// <summary>
    /// The game's item sets, and the bonuses a set of worn items earns from them.
    /// </summary>
    public sealed class ItemSets
    {
        private readonly List<ItemSet> _sets = new List<ItemSet>();

        public IReadOnlyList<ItemSet> All => _sets;

        public ItemSets Add(ItemSet set)
        {
            if (set == null)
                throw new ArgumentNullException(nameof(set));
            if (_sets.Any(s => s.Id == set.Id))
                throw new ArgumentException("Item set '" + set.Id + "' is already there.", nameof(set));
            _sets.Add(set);
            return this;
        }

        public ItemSet Find(string setId) => _sets.FirstOrDefault(s => s.Id == setId);

        /// <summary>
        /// The sets an item belongs to.
        /// </summary>
        public IReadOnlyList<ItemSet> SetsOf(string itemId) => _sets.Where(s => s.Contains(itemId)).ToList();

        /// <summary>
        /// Every bonus the worn items earn: for each set, the bonuses of as many pieces as are worn or fewer.
        /// </summary>
        public List<(ItemSet Set, ItemSetBonus Bonus)> BonusesFor(IEnumerable<string> wornItemIds)
        {
            var worn = (wornItemIds ?? Enumerable.Empty<string>()).ToList();
            var earned = new List<(ItemSet, ItemSetBonus)>();
            foreach (var set in _sets)
            {
                int pieces = set.PiecesIn(worn);
                if (pieces == 0)
                    continue;
                foreach (var bonus in set.Bonuses)
                {
                    if (bonus.Pieces <= pieces)
                        earned.Add((set, bonus));
                }
            }
            return earned;
        }
    }
}
