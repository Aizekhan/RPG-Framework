// Assets/_Framework/Combat/Runtime/IDamageCalculator.cs

namespace RPGFramework.Combat
{
    /// <summary>
    /// Generic damage calculator interface for RPG damage formulas
    /// </summary>
    public interface IDamageCalculator
    {
        /// <summary>
        /// Calculate final damage amount with all modifiers
        /// </summary>
        /// <param name="baseDamage">Base damage before any modifiers</param>
        /// <param name="defenseValue">Defender's defense stat</param>
        /// <param name="isCritical">Whether this is a critical hit</param>
        /// <param name="criticalMultiplier">Critical damage multiplier (e.g., 2.0 for 200% damage)</param>
        /// <param name="damageMultipliers">Additional damage multipliers (buffs, debuffs, block penalty)</param>
        /// <param name="armorPenetration">Armor penetration value (reduces effective defense)</param>
        /// <returns>Final damage amount</returns>
        float CalculateDamage(
            float baseDamage,
            float defenseValue,
            bool isCritical = false,
            float criticalMultiplier = 1.0f,
            float[] damageMultipliers = null,
            float armorPenetration = 0f);

        /// <summary>
        /// Calculate damage with full result breakdown (includes all calculation details)
        /// </summary>
        /// <param name="baseDamage">Base damage before any modifiers</param>
        /// <param name="defenseValue">Defender's defense stat</param>
        /// <param name="criticalChance">Probability of critical hit (0.0 to 1.0)</param>
        /// <param name="criticalMultiplier">Critical damage multiplier</param>
        /// <param name="damageMultipliers">Additional damage multipliers</param>
        /// <param name="armorPenetration">Armor penetration value</param>
        /// <returns>DamageResult with full calculation breakdown</returns>
        DamageResult CalculateDamageWithResult(
            float baseDamage,
            float defenseValue,
            float criticalChance,
            float criticalMultiplier,
            float[] damageMultipliers = null,
            float armorPenetration = 0f);

        /// <summary>
        /// Calculate effective defense after armor penetration
        /// </summary>
        /// <param name="defenseValue">Original defense value</param>
        /// <param name="armorPenetration">Armor penetration amount</param>
        /// <returns>Effective defense value (minimum 0)</returns>
        float CalculateEffectiveDefense(float defenseValue, float armorPenetration);

        /// <summary>
        /// Calculate damage reduction percentage from defense
        /// </summary>
        /// <param name="defenseValue">Defense value</param>
        /// <returns>Damage reduction (0.0 to 1.0)</returns>
        float CalculateDamageReduction(float defenseValue);
    }
}
