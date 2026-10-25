using System;
using System.Collections.Generic;

namespace RPGFramework.Items
{
    /// <summary>
    /// Default implementation of IEquipment.
    /// Represents equippable items that provide stat bonuses.
    /// </summary>
    /// <typeparam name="TSlotType">Equipment slot type enum</typeparam>
    /// <typeparam name="TStatType">Stat type for modifiers</typeparam>
    /// <typeparam name="TValue">Stat value type</typeparam>
    public class Equipment<TSlotType, TStatType, TValue> : Item, IEquipment<TSlotType, TStatType, TValue>
        where TSlotType : Enum
        where TStatType : struct, IConvertible
        where TValue : struct, IComparable<TValue>
    {
        public TSlotType SlotType { get; }
        public IReadOnlyList<RPGFramework.Stats.IStatModifier<TStatType, TValue>> StatModifiers { get; }
        public int MinimumLevel { get; }
        public int MaxDurability { get; }
        public bool IsRepairable { get; }

        /// <summary>
        /// Create a new equipment definition.
        /// </summary>
        /// <param name="itemId">Unique item identifier</param>
        /// <param name="name">Display name</param>
        /// <param name="description">Tooltip description</param>
        /// <param name="slotType">Equipment slot this can be equipped in</param>
        /// <param name="statModifiers">Stat bonuses (can be null or empty)</param>
        /// <param name="rarity">Rarity tier (default: Common)</param>
        /// <param name="quality">Quality tier (default: Normal)</param>
        /// <param name="itemLevel">Item level (default: 1)</param>
        /// <param name="minimumLevel">Minimum level to equip (default: 0 = no requirement)</param>
        /// <param name="maxDurability">Max durability (default: -1 = indestructible)</param>
        /// <param name="isRepairable">Can be repaired (default: true)</param>
        /// <param name="iconId">Icon identifier (default: empty)</param>
        public Equipment(
            string itemId,
            string name,
            string description,
            TSlotType slotType,
            IReadOnlyList<RPGFramework.Stats.IStatModifier<TStatType, TValue>> statModifiers,
            ItemRarity rarity = ItemRarity.Common,
            ItemQuality quality = ItemQuality.Normal,
            int itemLevel = 1,
            int minimumLevel = 0,
            int maxDurability = -1,
            bool isRepairable = true,
            string iconId = "")
            : base(itemId, name, description, ItemType.Equipment, rarity, quality, 1, itemLevel, iconId)
        {
            SlotType = slotType;
            StatModifiers = statModifiers ?? new List<RPGFramework.Stats.IStatModifier<TStatType, TValue>>();
            MinimumLevel = minimumLevel;
            MaxDurability = maxDurability;
            IsRepairable = isRepairable;
        }
    }
}
