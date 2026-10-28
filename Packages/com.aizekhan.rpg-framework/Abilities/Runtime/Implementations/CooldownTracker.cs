using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of ICooldownTracker.
    /// Tracks cooldown state for abilities across multiple entities.
    /// With an event publisher, raises CooldownStarted, CooldownCleared when a cooldown is cleared before it ends,
    /// and CooldownExpired from <see cref="CollectExpired"/>.
    /// </summary>
    public class CooldownTracker : ICooldownTracker
    {
        // entityId -> (abilityId -> CooldownState)
        private readonly Dictionary<int, Dictionary<string, CooldownState>> _cooldowns =
            new Dictionary<int, Dictionary<string, CooldownState>>();
        private readonly IEventPublisher _events;
        private readonly List<string> _expired = new List<string>();

        public CooldownTracker(IEventPublisher events = null)
        {
            _events = events;
        }

        // Lets the executor skip its own CooldownStarted when this tracker already raises it on the same publisher.
        internal IEventPublisher Events => _events;

        /// <summary>
        /// Removes cooldowns that have ended and raises CooldownExpired for each. Call it once per frame (or tick)
        /// when something listens for expirations; queries work without it.
        /// </summary>
        /// <returns>Number of cooldowns that expired.</returns>
        public int CollectExpired(float currentTime)
        {
            int count = 0;
            foreach (var entity in _cooldowns)
            {
                _expired.Clear();
                foreach (var pair in entity.Value)
                {
                    if (!pair.Value.IsOnCooldown(currentTime))
                        _expired.Add(pair.Key);
                }
                foreach (var abilityId in _expired)
                {
                    entity.Value.Remove(abilityId);
                    count++;
                    _events?.Publish(new CooldownExpiredEvent { AbilityId = abilityId, EntityId = entity.Key, Timestamp = currentTime });
                }
            }
            return count;
        }

        public void StartCooldown(int entityId, string abilityId, float duration, float currentTime)
        {
            if (!_cooldowns.ContainsKey(entityId))
            {
                _cooldowns[entityId] = new Dictionary<string, CooldownState>();
            }

            _cooldowns[entityId][abilityId] = new CooldownState(abilityId, duration, currentTime);
            _events?.Publish(new CooldownStartedEvent
            {
                AbilityId = abilityId,
                EntityId = entityId,
                Duration = duration,
                Timestamp = currentTime
            });
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
            if (_cooldowns.TryGetValue(entityId, out var entityCooldowns) && entityCooldowns.Remove(abilityId))
                _events?.Publish(new CooldownClearedEvent { AbilityId = abilityId, EntityId = entityId });
        }

        public void ClearAllCooldowns(int entityId)
        {
            if (_cooldowns.TryGetValue(entityId, out var entityCooldowns) && _cooldowns.Remove(entityId) && entityCooldowns.Count > 0)
                _events?.Publish(new CooldownClearedEvent { AbilityId = null, EntityId = entityId });
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
