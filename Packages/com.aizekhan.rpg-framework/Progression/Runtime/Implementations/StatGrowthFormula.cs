using System.Collections.Generic;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Default implementation of IStatGrowthFormula.
    /// Manages stat growth calculations for multiple stats, shared or per profile.
    /// </summary>
    public class StatGrowthFormula : IStatGrowthFormula
    {
        private readonly Dictionary<string, StatGrowthData> _statGrowth = new Dictionary<string, StatGrowthData>();
        private readonly Dictionary<string, Dictionary<string, StatGrowthData>> _profiles =
            new Dictionary<string, Dictionary<string, StatGrowthData>>();
        private readonly Dictionary<int, string> _entityProfiles = new Dictionary<int, string>();

        public void RegisterStatGrowth(StatGrowthData growthData)
        {
            _statGrowth[growthData.StatId] = growthData;
        }

        public void RegisterStatGrowth(string profileId, StatGrowthData growthData)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                RegisterStatGrowth(growthData);
                return;
            }

            if (!_profiles.TryGetValue(profileId, out var profile))
                _profiles[profileId] = profile = new Dictionary<string, StatGrowthData>();
            profile[growthData.StatId] = growthData;
        }

        public bool HasStatGrowth(string statId)
        {
            return _statGrowth.ContainsKey(statId);
        }

        public bool HasStatGrowth(int entityId, string statId)
        {
            return TryGetGrowth(entityId, statId, out _);
        }

        public void AssignProfile(int entityId, string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                _entityProfiles.Remove(entityId);
            else
                _entityProfiles[entityId] = profileId;
        }

        public string GetProfile(int entityId)
        {
            return _entityProfiles.TryGetValue(entityId, out var profileId) ? profileId : null;
        }

        public float CalculateStatAtLevel(string statId, int level)
        {
            if (!_statGrowth.TryGetValue(statId, out var growth))
                return 0f;

            return growth.CalculateAtLevel(level);
        }

        public float CalculateStatAtLevel(int entityId, string statId, int level)
        {
            return TryGetGrowth(entityId, statId, out var growth) ? growth.CalculateAtLevel(level) : 0f;
        }

        public Dictionary<string, float> GetStatChangesForLevelUp(int fromLevel, int toLevel)
        {
            var changes = new Dictionary<string, float>();

            foreach (var kvp in _statGrowth)
                changes[kvp.Key] = kvp.Value.CalculateAtLevel(toLevel);

            return changes;
        }

        public Dictionary<string, float> GetStatChangesForLevelUp(int entityId, int fromLevel, int toLevel)
        {
            var changes = GetStatChangesForLevelUp(fromLevel, toLevel);
            if (TryGetProfile(entityId, out var profile))
            {
                foreach (var kvp in profile)
                    changes[kvp.Key] = kvp.Value.CalculateAtLevel(toLevel);
            }

            return changes;
        }

        private bool TryGetProfile(int entityId, out Dictionary<string, StatGrowthData> profile)
        {
            profile = null;
            return _entityProfiles.TryGetValue(entityId, out var profileId) && _profiles.TryGetValue(profileId, out profile);
        }

        private bool TryGetGrowth(int entityId, string statId, out StatGrowthData growth)
        {
            if (TryGetProfile(entityId, out var profile) && profile.TryGetValue(statId, out growth))
                return true;
            return _statGrowth.TryGetValue(statId, out growth);
        }
    }
}
