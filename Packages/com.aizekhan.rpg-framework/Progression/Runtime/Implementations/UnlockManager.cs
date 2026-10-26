using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Default implementation of IUnlockManager.
    /// Manages unlock registration and tracks unlocked content per entity.
    /// With an event publisher, raises UnlockAchieved the first time an entity unlocks something.
    /// </summary>
    public class UnlockManager : IUnlockManager
    {
        private readonly Dictionary<string, UnlockData> _unlocks = new Dictionary<string, UnlockData>();
        private readonly Dictionary<int, HashSet<string>> _entityUnlocks = new Dictionary<int, HashSet<string>>();
        private readonly IEventPublisher _events;
        private readonly Func<float> _clock;

        public UnlockManager(IEventPublisher events = null, Func<float> clock = null)
        {
            _events = events;
            _clock = clock;
        }

        public void RegisterUnlock(UnlockData unlock)
        {
            _unlocks[unlock.UnlockId] = unlock;
        }

        public bool IsUnlockAvailable(string unlockId, int level)
        {
            if (!_unlocks.TryGetValue(unlockId, out var unlock))
                return false;

            return level >= unlock.RequiredLevel;
        }

        public List<UnlockData> GetUnlocksForLevel(int level)
        {
            return _unlocks.Values
                .Where(u => u.RequiredLevel == level)
                .ToList();
        }

        public List<UnlockData> GetAllUnlocksUpToLevel(int level)
        {
            return _unlocks.Values
                .Where(u => u.RequiredLevel <= level)
                .OrderBy(u => u.RequiredLevel)
                .ToList();
        }

        public bool HasUnlocked(int entityId, string unlockId)
        {
            if (!_entityUnlocks.TryGetValue(entityId, out var unlocked))
                return false;

            return unlocked.Contains(unlockId);
        }

        public void AchieveUnlock(int entityId, string unlockId)
        {
            if (!_entityUnlocks.ContainsKey(entityId))
            {
                _entityUnlocks[entityId] = new HashSet<string>();
            }

            if (!_entityUnlocks[entityId].Add(unlockId) || _events == null)
                return;

            _unlocks.TryGetValue(unlockId, out var unlock);
            _events.Publish(new UnlockAchievedEvent
            {
                EntityId = entityId,
                UnlockId = unlockId,
                UnlockName = unlock.Name,
                UnlockType = unlock.UnlockType,
                Level = unlock.RequiredLevel,
                Timestamp = _clock?.Invoke() ?? 0f
            });
        }

        public List<string> GetUnlockedContent(int entityId)
        {
            if (!_entityUnlocks.TryGetValue(entityId, out var unlocked))
                return new List<string>();

            return unlocked.ToList();
        }
    }
}
