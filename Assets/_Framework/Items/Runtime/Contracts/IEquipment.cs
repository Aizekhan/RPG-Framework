using System;
using System.Collections.Generic;

namespace RPGFramework.Items
{
    /// <summary>
    /// Equipment item that can be equipped in slots and provides stat bonuses.
    /// Extends IItem with equipment-specific properties.
    /// </summary>
    /// <typeparam name="TSlotType">Equipment slot type enum (Helmet, Chest, Weapon, etc.)</typeparam>
    /// <typeparam name="TStatType">Stat type for modifiers (Strength, Defense, etc.)</typeparam>
    /// <typeparam name="TValue">Stat value type (float, int, etc.)</typeparam>
    public interface IEquipment<TSlotType, TStatType, TValue> : IItem
        where TSlotType : Enum
        where TStatType : struct, IConvertible
        where TValue : struct, IComparable<TValue>
    {
        /// <summary>
        /// Equipment slot this item can be equipped in.
        /// Determines where in the equipment UI this item goes.
        /// </summary>
        TSlotType SlotType { get; }

        /// <summary>
        /// Stat modifiers provided when equipped.
        /// Applied to entity's stat container when equipped, removed when unequipped.
        /// </summary>
        IReadOnlyList<RPGFramework.Stats.IStatModifier<TStatType, TValue>> StatModifiers { get; }

        /// <summary>
        /// Minimum level required to equip this item.
        /// 0 = no requirement, player can equip at any level.
        /// </summary>
        int MinimumLevel { get; }

        /// <summary>
        /// Maximum durability points.
        /// -1 = indestructible (no durability system).
        /// 0 = already broken/destroyed.
        /// Greater than 0 = has durability that decreases with use.
        /// </summary>
        int MaxDurability { get; }

        /// <summary>
        /// Whether this item can be repaired when durability drops.
        /// Only relevant if MaxDurability greater than 0.
        /// </summary>
        bool IsRepairable { get; }
    }
}
