using System.Collections.Generic;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Manager for applying, removing, and updating status effects on entities.
    /// </summary>
    public interface IStatusEffectManager
    {
        /// <summary>
        /// Register an effect definition.
        /// </summary>
        /// <returns>False if an effect with this id is already registered; the first one is kept.</returns>
        bool RegisterEffect(IStatusEffect effect);

        /// <summary>
        /// Register an effect from data.
        /// </summary>
        /// <returns>False if an effect with this id is already registered; the first one is kept.</returns>
        bool RegisterEffect(StatusEffectData data);

        /// <summary>
        /// Apply a status effect to an entity.
        /// </summary>
        /// <param name="entityId">Target entity</param>
        /// <param name="effectId">Effect to apply</param>
        /// <param name="sourceId">Source of the effect</param>
        /// <param name="currentTime">Current game time</param>
        /// <returns>True if effect was applied successfully</returns>
        bool ApplyEffect(int entityId, string effectId, string sourceId, float currentTime);

        /// <summary>
        /// Remove a specific effect from an entity.
        /// </summary>
        /// <param name="entityId">Target entity</param>
        /// <param name="effectId">Effect to remove</param>
        /// <returns>True if effect was removed</returns>
        bool RemoveEffect(int entityId, string effectId);

        /// <summary>
        /// Remove all effects from an entity.
        /// </summary>
        /// <param name="entityId">Target entity</param>
        void RemoveAllEffects(int entityId);

        /// <summary>
        /// Update all effects on an entity (duration, ticks, expiration).
        /// </summary>
        /// <param name="entityId">Target entity</param>
        /// <param name="deltaTime">Time elapsed since last update</param>
        void UpdateEffects(int entityId, float deltaTime);

        /// <summary>
        /// Get all active effects on an entity.
        /// </summary>
        /// <param name="entityId">Target entity</param>
        /// <returns>List of active effect instances</returns>
        IReadOnlyList<IStatusEffectInstance> GetActiveEffects(int entityId);

        /// <summary>
        /// Check if an entity has a specific effect.
        /// </summary>
        /// <param name="entityId">Target entity</param>
        /// <param name="effectId">Effect to check</param>
        /// <returns>True if entity has the effect</returns>
        bool HasEffect(int entityId, string effectId);
    }
}
