using System.Collections.Generic;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Manages status effects on targets.
    /// Handles application, removal, stacking, and updating of effects.
    /// </summary>
    public interface IStatusEffectManager
    {
        /// <summary>
        /// Apply a status effect to a target.
        /// Handles stacking behavior automatically based on effect configuration.
        /// </summary>
        /// <param name="target">Target to receive the effect</param>
        /// <param name="effect">Effect to apply</param>
        /// <returns>True if effect was applied (or stacked/refreshed)</returns>
        bool ApplyEffect(IStatusEffectTarget target, IStatusEffect effect);

        /// <summary>
        /// Remove a specific effect from a target by effect ID.
        /// </summary>
        /// <param name="target">Target to remove effect from</param>
        /// <param name="effectId">ID of effect to remove</param>
        /// <returns>True if effect was found and removed</returns>
        bool RemoveEffect(IStatusEffectTarget target, string effectId);

        /// <summary>
        /// Remove all status effects from a target.
        /// </summary>
        /// <param name="target">Target to clear effects from</param>
        void RemoveAllEffects(IStatusEffectTarget target);

        /// <summary>
        /// Remove all effects of a specific type from a target.
        /// Useful for "cleanse debuffs" mechanics.
        /// </summary>
        /// <param name="target">Target to remove effects from</param>
        /// <param name="effectType">Type of effects to remove (e.g., all Debuffs)</param>
        void RemoveEffectsOfType(IStatusEffectTarget target, StatusEffectType effectType);

        /// <summary>
        /// Get all currently active effects on a target.
        /// </summary>
        /// <param name="target">Target to query</param>
        /// <returns>Read-only list of active effects</returns>
        IReadOnlyList<IStatusEffect> GetActiveEffects(IStatusEffectTarget target);

        /// <summary>
        /// Check if target has a specific effect active.
        /// </summary>
        /// <param name="target">Target to check</param>
        /// <param name="effectId">ID of effect to check for</param>
        /// <returns>True if effect is active on target</returns>
        bool HasEffect(IStatusEffectTarget target, string effectId);

        /// <summary>
        /// Update all active effects (call once per frame/tick).
        /// Updates durations and triggers recurring effects.
        /// </summary>
        /// <param name="target">Target to update effects for</param>
        /// <param name="deltaTime">Time delta in seconds</param>
        void UpdateEffects(IStatusEffectTarget target, float deltaTime);
    }
}
