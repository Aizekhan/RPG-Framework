using System.Collections.Generic;
using System.Linq;
using RPGFramework.Achievements.Runtime;
using RPGFramework.Achievements.Runtime.Contracts;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Implementation of IAchievementRegistry.
    /// Central repository for all achievement definitions.
    /// </summary>
    public class AchievementRegistry : IAchievementRegistry
    {
        private readonly Dictionary<string, IAchievement> _achievements = new Dictionary<string, IAchievement>();

        /// <summary>
        /// Register a new achievement.
        /// </summary>
        public void RegisterAchievement(IAchievement achievement)
        {
            if (achievement == null)
                throw new System.ArgumentNullException(nameof(achievement));

            _achievements[achievement.AchievementId] = achievement;
        }

        /// <summary>
        /// Unregister an achievement.
        /// </summary>
        public bool UnregisterAchievement(string achievementId)
        {
            return _achievements.Remove(achievementId);
        }

        /// <summary>
        /// Get an achievement by ID.
        /// </summary>
        public IAchievement? GetAchievement(string achievementId)
        {
            _achievements.TryGetValue(achievementId, out var achievement);
            return achievement;
        }

        /// <summary>
        /// Check if an achievement is registered.
        /// </summary>
        public bool HasAchievement(string achievementId)
        {
            return _achievements.ContainsKey(achievementId);
        }

        /// <summary>
        /// Get all registered achievements.
        /// </summary>
        public IReadOnlyCollection<IAchievement> GetAllAchievements()
        {
            return _achievements.Values.ToList();
        }

        /// <summary>
        /// Get achievements by tier.
        /// </summary>
        public IReadOnlyCollection<IAchievement> GetAchievementsByTier(AchievementTier tier)
        {
            return _achievements.Values
                .Where(a => a.Tier == tier)
                .ToList();
        }

        /// <summary>
        /// Get achievements that use a specific criterion type.
        /// </summary>
        public IReadOnlyCollection<IAchievement> GetAchievementsByCriterionType(CriterionType criterionType)
        {
            return _achievements.Values
                .Where(a => a.Criteria.Any(c => c.Type == criterionType))
                .ToList();
        }

        /// <summary>
        /// Clear all registered achievements.
        /// </summary>
        public void Clear()
        {
            _achievements.Clear();
        }
    }
}
