using System.Collections.Generic;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Tracks cooldowns for abilities across entities.
    /// Manages time-based cooldown state.
    /// </summary>
    public interface ICooldownTracker
    {
        /// <summary>Start cooldown for ability</summary>
        void StartCooldown(int entityId, string abilityId, float duration, float currentTime);

        /// <summary>Check if ability is on cooldown</summary>
        bool IsOnCooldown(int entityId, string abilityId, float currentTime);

        /// <summary>Get remaining cooldown time</summary>
        float GetRemainingCooldown(int entityId, string abilityId, float currentTime);

        /// <summary>Clear cooldown for specific ability</summary>
        void ClearCooldown(int entityId, string abilityId);

        /// <summary>Clear all cooldowns for entity</summary>
        void ClearAllCooldowns(int entityId);

        /// <summary>Get all active cooldowns for entity</summary>
        IReadOnlyList<CooldownState> GetActiveCooldowns(int entityId, float currentTime);
    }
}
