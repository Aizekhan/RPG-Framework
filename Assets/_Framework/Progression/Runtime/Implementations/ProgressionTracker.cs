using System.Collections.Generic;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Default implementation of IProgressionTracker.
    /// Tracks experience and levels for multiple entities.
    /// </summary>
    public class ProgressionTracker : IProgressionTracker
    {
        private readonly ILevelTable _levelTable;
        private readonly Dictionary<int, ExperienceData> _entityExperience = new Dictionary<int, ExperienceData>();

        public ProgressionTracker(ILevelTable levelTable)
        {
            _levelTable = levelTable;
        }

        public ProgressionResult AddExperience(int entityId, long xpAmount)
        {
            if (!_entityExperience.TryGetValue(entityId, out var expData))
            {
                // Initialize with level 1
                expData = new ExperienceData(1, 0, 0, _levelTable.GetXPForNextLevel(1));
            }

            int oldLevel = expData.Level;
            long newTotalXP = expData.TotalXP + xpAmount;

            // Calculate new level based on total XP
            int newLevel = _levelTable.CalculateLevelFromXP(newTotalXP);

            // Cap at max level
            if (newLevel > _levelTable.MaxLevel)
                newLevel = _levelTable.MaxLevel;

            // Calculate XP in current level
            long currentLevelTotalXP = _levelTable.GetTotalXPForLevel(newLevel);
            long nextLevelTotalXP = newLevel < _levelTable.MaxLevel
                ? _levelTable.GetTotalXPForLevel(newLevel + 1)
                : currentLevelTotalXP;

            long xpInCurrentLevel = newTotalXP - currentLevelTotalXP;
            long xpToNextLevel = nextLevelTotalXP - currentLevelTotalXP;

            // Create new experience data
            var newExpData = new ExperienceData(newLevel, xpInCurrentLevel, newTotalXP, xpToNextLevel);
            _entityExperience[entityId] = newExpData;

            return ProgressionResult.Create(oldLevel, newLevel, xpAmount, newExpData);
        }

        public void SetLevel(int entityId, int level)
        {
            if (level < 1)
                level = 1;
            if (level > _levelTable.MaxLevel)
                level = _levelTable.MaxLevel;

            long totalXP = _levelTable.GetTotalXPForLevel(level);
            long xpToNext = level < _levelTable.MaxLevel
                ? _levelTable.GetXPForNextLevel(level)
                : 0;

            _entityExperience[entityId] = new ExperienceData(level, 0, totalXP, xpToNext);
        }

        public ExperienceData GetExperienceData(int entityId)
        {
            if (_entityExperience.TryGetValue(entityId, out var data))
                return data;

            // Return default level 1 data
            return new ExperienceData(1, 0, 0, _levelTable.GetXPForNextLevel(1));
        }

        public int GetLevel(int entityId)
        {
            return GetExperienceData(entityId).Level;
        }

        public long GetTotalXP(int entityId)
        {
            return GetExperienceData(entityId).TotalXP;
        }

        public long GetXPRequiredForLevel(int level)
        {
            return _levelTable.GetTotalXPForLevel(level);
        }
    }
}
