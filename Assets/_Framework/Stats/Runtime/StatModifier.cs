// Assets/_Framework/Stats/Runtime/StatModifier.cs

using System;

namespace RPGFramework.Stats
{
    /// <summary>
    /// Default implementation of stat modifier
    /// </summary>
    /// <typeparam name="TStatType">Stat identifier type</typeparam>
    /// <typeparam name="TValue">Stat value type</typeparam>
    public class StatModifier<TStatType, TValue> : IStatModifier<TStatType, TValue>
        where TStatType : struct, IConvertible
        where TValue : struct, IComparable<TValue>
    {
        /// <summary>
        /// Unique modifier ID (for removal)
        /// </summary>
        public string ModifierId { get; }

        /// <summary>
        /// Stat type being modified
        /// </summary>
        public TStatType StatType { get; }

        /// <summary>
        /// Modifier value
        /// </summary>
        public TValue Value { get; }

        /// <summary>
        /// Modifier type (additive, multiplicative, override)
        /// </summary>
        public StatModifierType ModifierType { get; }

        /// <summary>
        /// Priority for ordering (higher = applied later)
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Create a new stat modifier
        /// </summary>
        /// <param name="modifierId">Unique identifier for this modifier</param>
        /// <param name="statType">Stat type to modify</param>
        /// <param name="value">Modifier value</param>
        /// <param name="modifierType">Type of modification (additive/multiplicative/override)</param>
        /// <param name="priority">Priority for ordering (default 0)</param>
        public StatModifier(
            string modifierId,
            TStatType statType,
            TValue value,
            StatModifierType modifierType,
            int priority = 0)
        {
            if (string.IsNullOrEmpty(modifierId))
                throw new ArgumentException("ModifierId cannot be null or empty", nameof(modifierId));

            ModifierId = modifierId;
            StatType = statType;
            Value = value;
            ModifierType = modifierType;
            Priority = priority;
        }

        public override string ToString()
        {
            return $"StatModifier[{ModifierId}]: {StatType} {ModifierType} {Value} (Priority={Priority})";
        }
    }
}
