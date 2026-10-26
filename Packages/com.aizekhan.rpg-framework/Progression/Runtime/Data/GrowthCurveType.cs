namespace RPGFramework.Progression
{
    /// <summary>
    /// Types of stat growth curves.
    /// Defines how stats increase on level up.
    /// </summary>
    public enum GrowthCurveType
    {
        /// <summary>Linear: value = base + (growth * level)</summary>
        Linear = 0,

        /// <summary>Exponential: value increases exponentially with percentage</summary>
        Exponential = 1,

        /// <summary>Polynomial: value = base + (growth * level^2)</summary>
        Polynomial = 2,

        /// <summary>Custom formula provided by game layer</summary>
        Custom = 3
    }
}
