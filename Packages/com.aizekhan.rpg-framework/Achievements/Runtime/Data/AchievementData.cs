using System;
using RPGFramework.ECS;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// ECS component for tracking unlocked achievements for an entity.
    /// Stores which achievements have been unlocked and when.
    /// </summary>
    [Serializable]
    public struct AchievementData : IComponent
    {
        /// <summary>
        /// Array of unlocked achievement IDs.
        /// </summary>
        public string[] UnlockedAchievementIds;

        /// <summary>
        /// Array of timestamps when achievements were unlocked (parallel to UnlockedAchievementIds).
        /// Stored as UTC ticks for serialization compatibility.
        /// </summary>
        public long[] UnlockTimestamps;

        /// <summary>
        /// Total achievement points earned.
        /// </summary>
        public int TotalPoints;

        /// <summary>
        /// Constructor for creating achievement data.
        /// </summary>
        /// <param name="unlockedIds">Array of unlocked achievement IDs.</param>
        /// <param name="timestamps">Array of unlock timestamps as UTC ticks.</param>
        /// <param name="totalPoints">Total achievement points earned.</param>
        public AchievementData(string[] unlockedIds, long[] timestamps, int totalPoints)
        {
            UnlockedAchievementIds = unlockedIds ?? Array.Empty<string>();
            UnlockTimestamps = timestamps ?? Array.Empty<long>();
            TotalPoints = totalPoints;
        }

        /// <summary>
        /// Create empty achievement data (no unlocked achievements).
        /// </summary>
        public static AchievementData Empty()
        {
            return new AchievementData(Array.Empty<string>(), Array.Empty<long>(), 0);
        }
    }
}
