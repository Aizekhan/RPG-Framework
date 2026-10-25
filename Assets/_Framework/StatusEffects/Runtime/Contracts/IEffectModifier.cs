namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Modifier that applies changes to stats, damage, healing, or other attributes.
    /// </summary>
    public interface IEffectModifier
    {
        /// <summary>
        /// Type of modifier (Stat, Damage, Healing, etc.)
        /// </summary>
        ModifierType Type { get; }

        /// <summary>
        /// Target identifier (e.g., "AttackPower", "Health", "MoveSpeed")
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// Value of the modifier.
        /// </summary>
        float Value { get; }

        /// <summary>
        /// How this modifier is applied (Add, Multiply, Override)
        /// </summary>
        ModifierOperation Operation { get; }

        /// <summary>
        /// Apply this modifier to a current value.
        /// </summary>
        /// <param name="currentValue">The current value to modify</param>
        /// <returns>The modified value</returns>
        float Apply(float currentValue);
    }
}
