using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Achievements.Runtime;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Implementation of IAchievementProgress.
    /// Tracks progress towards completing an achievement.
    /// </summary>
    public class AchievementProgress : IAchievementProgress
    {
        private readonly Dictionary<string, CriterionProgressData> _criterionProgress;

        public string AchievementId { get; private set; }
        public AchievementState State { get; private set; }
        public IReadOnlyDictionary<string, CriterionProgressData> CriterionProgress => _criterionProgress;

        public bool IsComplete => State == AchievementState.Unlocked;

        public float CompletionPercentage
        {
            get
            {
                if (_criterionProgress.Count == 0)
                    return 0f;

                float totalPercentage = _criterionProgress.Values
                    .Sum(p => p.GetCompletionPercentage());

                return totalPercentage / _criterionProgress.Count;
            }
        }

        /// <summary>
        /// Create new achievement progress.
        /// </summary>
        /// <param name="achievementId">Achievement ID to track.</param>
        /// <param name="criteria">List of criteria for this achievement.</param>
        public AchievementProgress(string achievementId, IEnumerable<(string CriterionId, int RequiredValue)> criteria)
        {
            if (string.IsNullOrEmpty(achievementId))
                throw new ArgumentException("Achievement ID cannot be null or empty", nameof(achievementId));

            AchievementId = achievementId;
            State = AchievementState.Locked;
            _criterionProgress = new Dictionary<string, CriterionProgressData>();

            foreach (var (criterionId, requiredValue) in criteria)
            {
                _criterionProgress[criterionId] = new CriterionProgressData(criterionId, 0, requiredValue);
            }
        }

        /// <summary>
        /// Update progress for a specific criterion.
        /// </summary>
        public bool UpdateCriterion(string criterionId, int currentValue)
        {
            if (!_criterionProgress.ContainsKey(criterionId))
                return false;

            var oldProgress = _criterionProgress[criterionId];
            if (oldProgress.CurrentValue == currentValue)
                return false;

            _criterionProgress[criterionId] = new CriterionProgressData(
                criterionId,
                currentValue,
                oldProgress.RequiredValue);

            // Update state based on progress
            if (State == AchievementState.Locked && currentValue > 0)
            {
                State = AchievementState.InProgress;
            }

            return true;
        }

        /// <summary>
        /// Get progress for a specific criterion.
        /// </summary>
        public CriterionProgressData? GetCriterionProgress(string criterionId)
        {
            if (_criterionProgress.TryGetValue(criterionId, out var progress))
                return progress;

            return null;
        }

        /// <summary>
        /// Mark the achievement as unlocked.
        /// </summary>
        public void Unlock()
        {
            State = AchievementState.Unlocked;
        }

        /// <summary>
        /// Check if all criteria are completed.
        /// </summary>
        public bool AreAllCriteriaCompleted()
        {
            return _criterionProgress.Values.All(p => p.IsCompleted);
        }
    }
}
