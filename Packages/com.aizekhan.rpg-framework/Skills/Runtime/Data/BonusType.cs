// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Data/BonusType.cs

namespace RPGFramework.Skills
{
    /// <summary>
    /// Defines how a skill bonus is applied to a stat.
    /// </summary>
    public enum BonusType
    {
        /// <summary>
        /// Additive bonus: base + bonus (e.g., HP = 100 + 50 = 150).
        /// </summary>
        Additive = 0,

        /// <summary>
        /// Multiplicative bonus: base * (1 + bonus) (e.g., HP = 100 * 1.5 = 150).
        /// </summary>
        Multiplicative = 1,

        /// <summary>
        /// Override: replaces the base value entirely.
        /// </summary>
        Override = 2
    }
}
