using System;
using System.Collections.Generic;

namespace RPGFramework.Items
{
    /// <summary>
    /// An item with rolled affixes, kept as one id: "baseId~rarity~affixes" ("sword~2~sturdy=2;str_1=9"). For games
    /// whose backpacks, saves and network messages hold item ids: the rolled item travels and saves like any other id,
    /// and every machine with the same content builds the same item from it (<see cref="Create"/>). Base ids must not
    /// contain '~'.
    /// </summary>
    public static class RolledItems
    {
        public const char Separator = '~';

        public static string Compose(string baseId, ItemRarity rarity, IEnumerable<RolledAffix> affixes)
        {
            if (string.IsNullOrEmpty(baseId) || baseId.IndexOf(Separator) >= 0)
                throw new ArgumentException("A base item id, without '~'.", nameof(baseId));
            return baseId + Separator + ((int)rarity).ToString(System.Globalization.CultureInfo.InvariantCulture) + Separator +
                   RolledAffix.Encode(affixes);
        }

        public static bool IsRolled(string itemId) => itemId != null && itemId.IndexOf(Separator) > 0;

        /// <summary>
        /// The item's base id: the id itself for an item that was not rolled.
        /// </summary>
        public static string BaseOf(string itemId)
        {
            if (!IsRolled(itemId))
                return itemId;
            return itemId.Substring(0, itemId.IndexOf(Separator));
        }

        public static bool TryParse(string itemId, out string baseId, out ItemRarity rarity, out List<RolledAffix> affixes)
        {
            baseId = itemId;
            rarity = ItemRarity.Common;
            affixes = new List<RolledAffix>();
            if (!IsRolled(itemId))
                return false;
            var parts = itemId.Split(new[] { Separator }, 3);
            if (parts.Length != 3 || !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int tier) || !Enum.IsDefined(typeof(ItemRarity), tier))
                return false;
            baseId = parts[0];
            rarity = (ItemRarity)tier;
            affixes = RolledAffix.Decode(parts[2]);
            return true;
        }

        /// <summary>
        /// Rolls a rarity and its affixes for a base item and gives the rolled id; the base id when nothing was rolled
        /// (a Common, or no affix fits).
        /// </summary>
        /// <param name="itemTags">The item's kinds for <see cref="AffixDefinition.OnlyOn"/>.</param>
        public static string Roll(string baseId, AffixPool pool, RarityTable rarities, Random random, int itemLevel,
            float luck = 0f, IReadOnlyCollection<string> itemTags = null)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            var rarity = (rarities ?? RarityTable.Default()).Roll(random, luck);
            var affixes = pool.Roll(random, itemLevel, rarity, itemTags);
            return affixes.Count == 0 ? baseId : Compose(baseId, rarity, affixes);
        }

        /// <summary>
        /// The rolled item: the base item's kind, stack size, level and icon, the rolled rarity, and a name made of
        /// the affixes ("Sturdy Sword of the Bear").
        /// </summary>
        /// <returns>Null when the id is not a rolled one or is for another base item.</returns>
        public static IItem Create(string itemId, IItem baseItem, AffixPool pool, Func<string, string> translate = null)
        {
            if (baseItem == null || !TryParse(itemId, out string baseId, out var rarity, out var affixes) || baseId != baseItem.ItemId)
                return null;
            string name = pool != null ? pool.NameFor(baseItem.Name, affixes, translate) : baseItem.Name;
            return new Item(itemId, name, baseItem.Description, baseItem.ItemType, rarity, baseItem.Quality,
                baseItem.MaxStackSize, baseItem.ItemLevel, baseItem.IconId);
        }
    }
}
