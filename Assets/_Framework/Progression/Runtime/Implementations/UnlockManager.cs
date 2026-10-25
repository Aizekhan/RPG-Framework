using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Default implementation of IUnlockManager.
    /// Manages unlock registration and tracks unlocked content per entity.
    /// </summary>
    public class UnlockManager : IUnlockManager
    {
        private readonly Dictionary<string, UnlockData> _unlocks = new Dictionary<string, UnlockData>();
        private readonly Dictionary<int, HashSet<string>> _entityUnlocks = new Dictionary<int, HashSet<string>>();

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

            _entityUnlocks[entityId].Add(unlockId);
        }

        public List<string> GetUnlockedContent(int entityId)
        {
            if (!_entityUnlocks.TryGetValue(entityId, out var unlocked))
                return new List<string>();

            return unlocked.ToList();
        }
    }
}
