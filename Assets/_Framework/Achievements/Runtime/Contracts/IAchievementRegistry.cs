using System.Collections.Generic;
using RPGFramework.Achievements.Runtime.Contracts;

namespace RPGFramework.Achievements.Runtime
{
    /// <summary>
    /// Registry for all available achievements in the game.
    /// Acts as a central repository for achievement definitions.
    /// </summary>
    public interface IAchievementRegistry
    {
        /// <summary>
        /// Register a new achievement.
        /// </summary>
        /// <param name="achievement">Achievement to register.</param>
        void RegisterAchievement(IAchievement achievement);

        /// <summary>
        /// Unregister an achievement.
        /// </summary>
        /// <param name="achievementId">Achievement ID to unregister.</param>
        /// <returns>True if achievement was unregistered.</returns>
        bool UnregisterAchievement(string achievementId);

        /// <summary>
        /// Get an achievement by ID.
        /// </summary>
        /// <param name="achievementId">Achievement ID.</param>
        /// <returns>Achievement or null if not found.</returns>
        IAchievement? GetAchievement(string achievementId);

        /// <summary>
        /// Check if an achievement is registered.
        /// </summary>
        /// <param name="achievementId">Achievement ID to check.</param>
        /// <returns>True if achievement exists.</returns>
        bool HasAchievement(string achievementId);

        /// <summary>
        /// Get all registered achievements.
        /// </summary>
        /// <returns>Collection of all achievements.</returns>
        IReadOnlyCollection<IAchievement> GetAllAchievements();

        /// <summary>
        /// Get achievements by tier.
        /// </summary>
        /// <param name="tier">Achievement tier to filter by.</param>
        /// <returns>Collection of achievements of the specified tier.</returns>
        IReadOnlyCollection<IAchievement> GetAchievementsByTier(AchievementTier tier);

        /// <summary>
        /// Get achievements that use a specific criterion type.
        /// </summary>
        /// <param name="criterionType">Criterion type to filter by.</param>
        /// <returns>Collection of achievements with the specified criterion type.</returns>
        IReadOnlyCollection<IAchievement> GetAchievementsByCriterionType(CriterionType criterionType);

        /// <summary>
        /// Clear all registered achievements.
        /// </summary>
        void Clear();
    }
}
