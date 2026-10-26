using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Default implementation of IProgressionTracker.
    /// Tracks experience and levels for multiple entities.
    /// With an event publisher, raises ExperienceGained and LevelUp; <c>clock</c> stamps them (game time, default 0).
    /// </summary>
    public class ProgressionTracker : IProgressionTracker
    {
        private readonly ILevelTable _levelTable;
        private readonly IEventPublisher _events;
        private readonly Func<float> _clock;
        private readonly Dictionary<int, ExperienceData> _entityExperience = new Dictionary<int, ExperienceData>();

        public ProgressionTracker(ILevelTable levelTable, IEventPublisher events = null, Func<float> clock = null)
        {
            _levelTable = levelTable;
            _events = events;
            _clock = clock;
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

            var result = ProgressionResult.Create(oldLevel, newLevel, xpAmount, newExpData);
            if (_events != null)
            {
                float now = _clock?.Invoke() ?? 0f;
                if (xpAmount != 0)
                {
                    _events.Publish(new ExperienceGainedEvent
                    {
                        EntityId = entityId,
                        XPAmount = xpAmount,
                        TotalXP = newTotalXP,
                        CurrentLevel = newLevel,
                        Timestamp = now
                    });
                }
                if (newLevel > oldLevel)
                {
                    _events.Publish(new LevelUpEvent
                    {
                        EntityId = entityId,
                        OldLevel = oldLevel,
                        NewLevel = newLevel,
                        LevelsGained = newLevel - oldLevel,
                        Timestamp = now
                    });
                }
            }
            return result;
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
