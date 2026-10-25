namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Types of status effects.
    /// </summary>
    public enum EffectType
    {
        /// <summary>
        /// Positive temporary effect.
        /// </summary>
        Buff,

        /// <summary>
        /// Negative temporary effect.
        /// </summary>
        Debuff,

        /// <summary>
        /// Damage over Time (Poison, Bleed, Burn).
        /// </summary>
        DamageOverTime,

        /// <summary>
        /// Heal over Time (Regeneration, Healing Aura).
        /// </summary>
        HealOverTime,

        /// <summary>
        /// Stat modification (Speed, Damage, Defense).
        /// </summary>
        StatModifier,

        /// <summary>
        /// Special status condition (Stun, Freeze, Silence).
        /// </summary>
        StatusCondition
    }
}
