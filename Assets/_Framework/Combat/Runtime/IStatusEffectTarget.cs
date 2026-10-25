namespace RPGFramework.Combat
{
    /// <summary>
    /// Interface for entities that can receive and be affected by status effects.
    /// Implement this on your game entities (characters, monsters, etc.).
    /// </summary>
    public interface IStatusEffectTarget
    {
        /// <summary>
        /// Unique identifier for this target entity.
        /// Used by StatusEffectManager to track effects per target.
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// Check if this target can receive a specific effect.
        /// Use this for immunity checks, effect type restrictions, etc.
        /// </summary>
        /// <param name="effect">The effect to check</param>
        /// <returns>True if effect can be applied to this target</returns>
        bool CanReceiveEffect(IStatusEffect effect);

        /// <summary>
        /// Get current value of a stat on this target.
        /// Used by effects to read current stats for calculations.
        /// </summary>
        /// <param name="statName">Name of the stat (e.g., "Health", "MaxHealth")</param>
        /// <returns>Current stat value</returns>
        float GetStat(string statName);

        /// <summary>
        /// Apply a damage instance to this target.
        /// Used by DOT effects to deal periodic damage.
        /// </summary>
        /// <param name="damage">Damage amount</param>
        /// <param name="damageType">Type of damage (integrates with Phase 3)</param>
        void ApplyDamage(float damage, DamageType damageType);

        /// <summary>
        /// Apply healing to this target.
        /// Used by HOT effects to restore health periodically.
        /// </summary>
        /// <param name="healing">Healing amount</param>
        void ApplyHealing(float healing);
    }
}
