using System;
using RPGFramework.Events;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Event emitted when an achievement is unlocked.
    /// Published by <c>AchievementManager</c> (when given an event publisher) once per achievement and entity,
    /// whether it was unlocked by progress, retroactively, or manually through <c>UnlockAchievement</c>.
    /// </summary>
    public struct AchievementUnlockedEvent : IEvent
    {
        /// <summary>
        /// The entity that unlocked the achievement.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// The ID of the achievement that was unlocked.
        /// </summary>
        public string AchievementId;

        /// <summary>
        /// The tier of the unlocked achievement.
        /// </summary>
        public AchievementTier Tier;

        /// <summary>
        /// Points awarded for this achievement.
        /// </summary>
        public int Points;

        /// <summary>
        /// Timestamp when the achievement was unlocked (UTC, the same moment the manager stores in AchievementData).
        /// </summary>
        public DateTime UnlockedAt;

        /// <summary>
        /// Constructor for creating an achievement unlocked event.
        /// </summary>
        public AchievementUnlockedEvent(
            int entityId,
            string achievementId,
            AchievementTier tier,
            int points,
            DateTime unlockedAt)
        {
            EntityId = entityId;
            AchievementId = achievementId;
            Tier = tier;
            Points = points;
            UnlockedAt = unlockedAt;
        }
    }
}
