using System.Collections.Generic;

namespace RPGFramework.Achievements.Runtime
{
    /// <summary>
    /// ECS-based manager for achievement tracking and unlocking.
    /// Supports retroactive checking for achievements when criteria are already met.
    /// </summary>
    public interface IAchievementManager
    {
        /// <summary>
        /// Update progress for a specific achievement criterion.
        /// Automatically checks if achievement can be unlocked.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <param name="achievementId">Achievement ID.</param>
        /// <param name="criterionId">Criterion ID to update.</param>
        /// <param name="currentValue">New current value.</param>
        /// <returns>True if progress was updated.</returns>
        bool UpdateCriterionProgress(int entityId, string achievementId, string criterionId, int currentValue);

        /// <summary>
        /// Manually unlock an achievement for an entity.
        /// Used for special achievements or testing.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <param name="achievementId">Achievement ID to unlock.</param>
        /// <returns>True if achievement was successfully unlocked.</returns>
        bool UnlockAchievement(int entityId, string achievementId);

        /// <summary>
        /// Check and unlock achievements retroactively based on current entity state.
        /// Useful when achievements are added to the game after players have progressed.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <param name="criterionType">Type of criteria to check (e.g., KillCount, QuestCompleted).</param>
        /// <param name="criterionData">Dictionary of criterion ID → current value.</param>
        /// <returns>List of achievement IDs that were unlocked.</returns>
        IReadOnlyList<string> CheckRetroactiveUnlocks(int entityId, CriterionType criterionType, IReadOnlyDictionary<string, int> criterionData);

        /// <summary>
        /// Get progress for a specific achievement.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <param name="achievementId">Achievement ID.</param>
        /// <returns>Achievement progress or null if not found.</returns>
        IAchievementProgress? GetAchievementProgress(int entityId, string achievementId);

        /// <summary>
        /// Get all unlocked achievements for an entity.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <returns>List of unlocked achievement IDs.</returns>
        IReadOnlyList<string> GetUnlockedAchievements(int entityId);

        /// <summary>
        /// Get all achievements in progress for an entity.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <returns>List of achievement progress.</returns>
        IReadOnlyList<IAchievementProgress> GetInProgressAchievements(int entityId);

        /// <summary>
        /// Check if an entity has unlocked a specific achievement.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <param name="achievementId">Achievement ID to check.</param>
        /// <returns>True if achievement is unlocked.</returns>
        bool IsAchievementUnlocked(int entityId, string achievementId);

        /// <summary>
        /// Initialize achievement tracking for a new entity.
        /// Sets up tracking for all available achievements.
        /// </summary>
        /// <param name="entityId">Entity to initialize.</param>
        void InitializeEntityAchievements(int entityId);

        /// <summary>
        /// Get achievements by tier.
        /// </summary>
        /// <param name="tier">Achievement tier to filter by.</param>
        /// <returns>List of achievement IDs of the specified tier.</returns>
        IReadOnlyList<string> GetAchievementsByTier(AchievementTier tier);
    }
}
