using System;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Tracks cooldown state for an ability.
    /// Uses time-based cooldowns with start time and duration.
    /// </summary>
    public struct CooldownState
    {
        /// <summary>Ability ID</summary>
        public string AbilityId;

        /// <summary>Total cooldown duration (in seconds)</summary>
        public float CooldownDuration;

        /// <summary>Timestamp when cooldown started (game time)</summary>
        public float StartTime;

        public CooldownState(string abilityId, float cooldownDuration, float startTime)
        {
            AbilityId = abilityId;
            CooldownDuration = cooldownDuration;
            StartTime = startTime;
        }

        /// <summary>Calculate remaining time on cooldown</summary>
        public float GetRemainingTime(float currentTime)
        {
            return Math.Max(0f, (StartTime + CooldownDuration) - currentTime);
        }

        /// <summary>Check if cooldown is active</summary>
        public bool IsOnCooldown(float currentTime)
        {
            return GetRemainingTime(currentTime) > 0f;
        }
    }
}
