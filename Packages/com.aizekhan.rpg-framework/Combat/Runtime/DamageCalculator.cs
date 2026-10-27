// Packages/com.aizekhan.rpg-framework/Combat/Runtime/DamageCalculator.cs

using System;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Default implementation of damage calculator using standard RPG formulas
    /// Formula: finalDamage = (baseDamage * totalMultiplier) * (100 / (100 + effectiveDefense))
    /// With an <see cref="AttackContext"/>, damage from an effect ignores defense; tags change nothing (a game's
    /// own calculator reads them).
    /// </summary>
    public class DamageCalculator : IDamageCalculator, IContextualDamageCalculator
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Calculate final damage amount with all modifiers
        /// </summary>
        public float CalculateDamage(
            float baseDamage,
            float defenseValue,
            bool isCritical = false,
            float criticalMultiplier = 1.0f,
            float[] damageMultipliers = null,
            float armorPenetration = 0f)
        {
            // Validate inputs
            if (baseDamage < 0f)
                throw new ArgumentException("Base damage cannot be negative", nameof(baseDamage));
            if (defenseValue < 0f)
                throw new ArgumentException("Defense value cannot be negative", nameof(defenseValue));
            if (criticalMultiplier <= 0f)
                throw new ArgumentException("Critical multiplier must be positive", nameof(criticalMultiplier));
            if (armorPenetration < 0f)
                throw new ArgumentException("Armor penetration cannot be negative", nameof(armorPenetration));

            // Calculate effective defense after armor penetration
            float effectiveDefense = CalculateEffectiveDefense(defenseValue, armorPenetration);

            // Calculate total multiplier
            float totalMultiplier = isCritical ? criticalMultiplier : 1.0f;

            // Apply additional damage multipliers (buffs, debuffs, block penalty, etc.)
            if (damageMultipliers != null && damageMultipliers.Length > 0)
            {
                foreach (float multiplier in damageMultipliers)
                {
                    if (multiplier < 0f)
                        throw new ArgumentException("Damage multipliers cannot be negative", nameof(damageMultipliers));
                    totalMultiplier *= multiplier;
                }
            }

            // Apply damage formula: (baseDamage * totalMultiplier) * (100 / (100 + effectiveDefense))
            float finalDamage = (baseDamage * totalMultiplier) * (100f / (100f + effectiveDefense));

            // Ensure minimum damage of 1 (damage cannot be 0 or negative)
            return Math.Max(1f, finalDamage);
        }

        public virtual float CalculateDamage(float baseDamage, float defenseValue, AttackContext context) =>
            context.FromEffect ? Math.Max(0f, baseDamage) : CalculateDamage(baseDamage, defenseValue);

        /// <summary>
        /// Calculate damage with full result breakdown and random critical hit roll
        /// </summary>
        public DamageResult CalculateDamageWithResult(
            float baseDamage,
            float defenseValue,
            float criticalChance,
            float criticalMultiplier,
            float[] damageMultipliers = null,
            float armorPenetration = 0f)
        {
            // Validate inputs
            if (baseDamage < 0f)
                throw new ArgumentException("Base damage cannot be negative", nameof(baseDamage));
            if (defenseValue < 0f)
                throw new ArgumentException("Defense value cannot be negative", nameof(defenseValue));
            if (criticalChance < 0f || criticalChance > 1f)
                throw new ArgumentException("Critical chance must be between 0 and 1", nameof(criticalChance));
            if (criticalMultiplier <= 0f)
                throw new ArgumentException("Critical multiplier must be positive", nameof(criticalMultiplier));
            if (armorPenetration < 0f)
                throw new ArgumentException("Armor penetration cannot be negative", nameof(armorPenetration));

            // Roll for critical hit
            bool isCritical = _random.NextDouble() <= criticalChance;

            // Calculate effective defense
            float effectiveDefense = CalculateEffectiveDefense(defenseValue, armorPenetration);

            // Calculate total multiplier
            float totalMultiplier = isCritical ? criticalMultiplier : 1.0f;

            if (damageMultipliers != null && damageMultipliers.Length > 0)
            {
                foreach (float multiplier in damageMultipliers)
                {
                    if (multiplier < 0f)
                        throw new ArgumentException("Damage multipliers cannot be negative", nameof(damageMultipliers));
                    totalMultiplier *= multiplier;
                }
            }

            // Calculate damage reduction percentage
            float damageReduction = CalculateDamageReduction(effectiveDefense);

            // Apply damage formula
            float finalDamage = (baseDamage * totalMultiplier) * (100f / (100f + effectiveDefense));
            finalDamage = Math.Max(1f, finalDamage);

            return new DamageResult(
                finalDamage: finalDamage,
                baseDamage: baseDamage,
                effectiveDefense: effectiveDefense,
                isCritical: isCritical,
                criticalMultiplier: isCritical ? criticalMultiplier : 1.0f,
                totalMultiplier: totalMultiplier,
                damageReduction: damageReduction
            );
        }

        /// <summary>
        /// Calculate effective defense after armor penetration
        /// Formula: max(0, defense - armorPenetration)
        /// </summary>
        public float CalculateEffectiveDefense(float defenseValue, float armorPenetration)
        {
            if (defenseValue < 0f)
                throw new ArgumentException("Defense value cannot be negative", nameof(defenseValue));
            if (armorPenetration < 0f)
                throw new ArgumentException("Armor penetration cannot be negative", nameof(armorPenetration));

            return Math.Max(0f, defenseValue - armorPenetration);
        }

        /// <summary>
        /// Calculate damage reduction percentage from defense
        /// Formula: defense / (100 + defense)
        /// Examples:
        ///   0 defense = 0% reduction (1.0 damage multiplier)
        ///   100 defense = 50% reduction (0.5 damage multiplier)
        ///   200 defense = 66.7% reduction (0.333 damage multiplier)
        /// </summary>
        public float CalculateDamageReduction(float defenseValue)
        {
            if (defenseValue < 0f)
                throw new ArgumentException("Defense value cannot be negative", nameof(defenseValue));

            return defenseValue / (100f + defenseValue);
        }
    }
}
