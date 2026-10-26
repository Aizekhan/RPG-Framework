// Packages/com.aizekhan.rpg-framework/Stats/Runtime/StatModifierType.cs

namespace RPGFramework.Stats
{
    /// <summary>
    /// Type of stat modifier for calculation order
    /// </summary>
    public enum StatModifierType
    {
        /// <summary>
        /// Adds to base value: base + modifier
        /// Example: Base 100 + Additive 20 = 120
        /// </summary>
        Additive,

        /// <summary>
        /// Multiplies final value: (base + additives) * (1 + modifier)
        /// Example: (Base 100 + Add 20) * (1 + 0.1) = 132
        /// </summary>
        Multiplicative,

        /// <summary>
        /// Overrides final value (highest priority)
        /// Example: Override 500 → Final = 500 (ignores base and modifiers)
        /// </summary>
        Override
    }
}
