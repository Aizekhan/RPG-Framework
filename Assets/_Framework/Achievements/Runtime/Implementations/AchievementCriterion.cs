using System;
using RPGFramework.Achievements.Runtime.Contracts;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Implementation of IAchievementCriterion.
    /// Represents a single criterion that must be met for an achievement.
    /// </summary>
    public class AchievementCriterion : IAchievementCriterion
    {
        public string CriterionId { get; private set; }
        public CriterionType Type { get; private set; }
        public string TargetId { get; private set; }
        public int RequiredCount { get; private set; }
        public string Description { get; private set; }
        public bool RequiresSingleSession { get; private set; }

        /// <summary>
        /// Create an achievement criterion.
        /// </summary>
        public AchievementCriterion(
            string criterionId,
            CriterionType type,
            string targetId,
            int requiredCount,
            string description = "",
            bool requiresSingleSession = false)
        {
            if (string.IsNullOrEmpty(criterionId))
                throw new ArgumentException("Criterion ID cannot be null or empty", nameof(criterionId));
            if (requiredCount <= 0)
                throw new ArgumentException("Required count must be positive", nameof(requiredCount));

            CriterionId = criterionId;
            Type = type;
            TargetId = targetId ?? string.Empty;
            RequiredCount = requiredCount;
            Description = description ?? string.Empty;
            RequiresSingleSession = requiresSingleSession;
        }

        /// <summary>
        /// Factory method to create a simple criterion.
        /// </summary>
        public static AchievementCriterion Create(
            string criterionId,
            CriterionType type,
            int requiredCount,
            string targetId = "")
        {
            return new AchievementCriterion(criterionId, type, targetId, requiredCount);
        }

        public override string ToString()
        {
            return $"Criterion[{CriterionId}]: {Type} - {RequiredCount} required";
        }
    }
}
