using System.Collections.Generic;

namespace RPGFramework.Achievements.Runtime
{
    /// <summary>
    /// Tracks progress towards completing an achievement.
    /// </summary>
    public interface IAchievementProgress
    {
        /// <summary>
        /// The achievement ID being tracked.
        /// </summary>
        string AchievementId { get; }

        /// <summary>
        /// Current state of the achievement.
        /// </summary>
        AchievementState State { get; }

        /// <summary>
        /// Progress data for each criterion.
        /// Key is criterion ID.
        /// </summary>
        IReadOnlyDictionary<string, CriterionProgressData> CriterionProgress { get; }

        /// <summary>
        /// Check if all criteria are completed.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Overall completion percentage (0.0 to 1.0).
        /// </summary>
        float CompletionPercentage { get; }

        /// <summary>
        /// Update progress for a specific criterion.
        /// </summary>
        /// <param name="criterionId">Criterion to update.</param>
        /// <param name="currentValue">New current value.</param>
        /// <returns>True if progress changed.</returns>
        bool UpdateCriterion(string criterionId, int currentValue);

        /// <summary>
        /// Get progress for a specific criterion.
        /// </summary>
        /// <param name="criterionId">Criterion ID to check.</param>
        /// <returns>Progress data or null if not found.</returns>
        CriterionProgressData? GetCriterionProgress(string criterionId);

        /// <summary>
        /// Mark the achievement as unlocked.
        /// </summary>
        void Unlock();
    }
}
