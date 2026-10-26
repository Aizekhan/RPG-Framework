// Packages/com.aizekhan.rpg-framework/Combat/Runtime/DamageResult.cs

namespace RPGFramework.Combat
{
    /// <summary>
    /// Result of damage calculation with detailed breakdown
    /// </summary>
    public struct DamageResult
    {
        /// <summary>
        /// Final damage amount after all calculations
        /// </summary>
        public float FinalDamage;

        /// <summary>
        /// Base damage before any modifiers
        /// </summary>
        public float BaseDamage;

        /// <summary>
        /// Effective defense value after armor penetration
        /// </summary>
        public float EffectiveDefense;

        /// <summary>
        /// Whether this attack was a critical hit
        /// </summary>
        public bool IsCritical;

        /// <summary>
        /// Critical damage multiplier applied (e.g., 2.0 for 200% damage)
        /// </summary>
        public float CriticalMultiplier;

        /// <summary>
        /// Total damage multiplier (includes critical, buffs, debuffs)
        /// </summary>
        public float TotalMultiplier;

        /// <summary>
        /// Damage reduction percentage from defense (0.0 to 1.0)
        /// </summary>
        public float DamageReduction;

        /// <summary>
        /// Create a damage result with all fields
        /// </summary>
        public DamageResult(
            float finalDamage,
            float baseDamage,
            float effectiveDefense,
            bool isCritical,
            float criticalMultiplier,
            float totalMultiplier,
            float damageReduction)
        {
            FinalDamage = finalDamage;
            BaseDamage = baseDamage;
            EffectiveDefense = effectiveDefense;
            IsCritical = isCritical;
            CriticalMultiplier = criticalMultiplier;
            TotalMultiplier = totalMultiplier;
            DamageReduction = damageReduction;
        }
    }
}
