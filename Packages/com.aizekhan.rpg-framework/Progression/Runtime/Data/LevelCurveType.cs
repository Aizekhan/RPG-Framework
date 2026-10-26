namespace RPGFramework.Progression
{
    /// <summary>
    /// Types of XP curves for level progression.
    /// Defines how XP requirements scale with level.
    /// </summary>
    public enum LevelCurveType
    {
        /// <summary>Linear: XP = base * level</summary>
        Linear = 0,

        /// <summary>Exponential: XP = base * (multiplier ^ level)</summary>
        Exponential = 1,

        /// <summary>Polynomial: XP = base * (level ^ power)</summary>
        Polynomial = 2,

        /// <summary>Custom table with predefined XP values</summary>
        Custom = 3
    }
}
