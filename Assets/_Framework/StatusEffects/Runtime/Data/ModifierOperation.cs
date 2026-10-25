namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// How a modifier is applied to a value.
    /// </summary>
    public enum ModifierOperation
    {
        /// <summary>
        /// Additive: value + modifier
        /// Example: 100 + 20 = 120
        /// </summary>
        Add,

        /// <summary>
        /// Multiplicative: value * modifier
        /// Example: 100 * 1.5 = 150 (50% increase)
        /// </summary>
        Multiply,

        /// <summary>
        /// Overrides the value entirely.
        /// Example: value = modifier
        /// </summary>
        Override
    }
}
