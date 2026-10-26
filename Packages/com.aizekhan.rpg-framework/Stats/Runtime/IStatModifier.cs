// Packages/com.aizekhan.rpg-framework/Stats/Runtime/IStatModifier.cs

using System;

namespace RPGFramework.Stats
{
    /// <summary>
    /// Generic stat modifier interface for buffs/debuffs
    /// </summary>
    /// <typeparam name="TStatType">Stat identifier type (enum or struct)</typeparam>
    /// <typeparam name="TValue">Stat value type (float, int, etc.)</typeparam>
    public interface IStatModifier<TStatType, TValue>
        where TStatType : struct, IConvertible
        where TValue : struct, IComparable<TValue>
    {
        /// <summary>
        /// Unique modifier ID (for removal)
        /// </summary>
        string ModifierId { get; }

        /// <summary>
        /// Stat type being modified
        /// </summary>
        TStatType StatType { get; }

        /// <summary>
        /// Modifier value
        /// </summary>
        TValue Value { get; }

        /// <summary>
        /// Modifier type (additive, multiplicative, override)
        /// </summary>
        StatModifierType ModifierType { get; }

        /// <summary>
        /// Priority for ordering (higher = applied later)
        /// Lower priority modifiers applied first, higher priority last
        /// </summary>
        int Priority { get; }
    }
}
