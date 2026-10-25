using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of ICooldownTracker.
    /// Tracks cooldown state for abilities across multiple entities.
    /// </summary>
    public class CooldownTracker : ICooldownTracker
    {
        // entityId -> (abilityId -> CooldownState)
        private readonly Dictionary<int, Dictionary<string, CooldownState>> _cooldowns =
            new Dictionary<int, Dictionary<string, CooldownState>>();

        public void StartCooldown(int entityId, string abilityId, float duration, float currentTime)
        {
            if (!_cooldowns.ContainsKey(entityId))
            {
                _cooldowns[entityId] = new Dictionary<string, CooldownState>();
            }

            _cooldowns[entityId][abilityId] = new CooldownState(abilityId, duration, currentTime);
        }

        public bool IsOnCooldown(int entityId, string abilityId, float currentTime)
        {
            if (!_cooldowns.TryGetValue(entityId, out var entityCooldowns))
                return false;

            if (!entityCooldowns.TryGetValue(abilityId, out var cooldownState))
                return false;

            return cooldownState.IsOnCooldown(currentTime);
        }

        public float GetRemainingCooldown(int entityId, string abilityId, float currentTime)
        {
            if (!_cooldowns.TryGetValue(entityId, out var entityCooldowns))
                return 0f;

            if (!entityCooldowns.TryGetValue(abilityId, out var cooldownState))
                return 0f;

            return cooldownState.GetRemainingTime(currentTime);
        }

        public void ClearCooldown(int entityId, string abilityId)
        {
            if (_cooldowns.TryGetValue(entityId, out var entityCooldowns))
            {
                entityCooldowns.Remove(abilityId);
            }
        }

        public void ClearAllCooldowns(int entityId)
        {
            _cooldowns.Remove(entityId);
        }

        public IReadOnlyList<CooldownState> GetActiveCooldowns(int entityId, float currentTime)
        {
            if (!_cooldowns.TryGetValue(entityId, out var entityCooldowns))
                return new List<CooldownState>();

            return entityCooldowns.Values
                .Where(cd => cd.IsOnCooldown(currentTime))
                .ToList();
        }
    }
}
