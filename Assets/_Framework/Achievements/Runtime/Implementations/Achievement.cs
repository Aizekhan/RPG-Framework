using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Achievements.Runtime;
using RPGFramework.Achievements.Runtime.Contracts;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Implementation of IAchievement.
    /// Represents an achievement definition with criteria and rewards.
    /// </summary>
    public class Achievement : IAchievement
    {
        public string AchievementId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Category { get; private set; }
        public int Points { get; private set; }
        public bool IsHidden { get; private set; }
        public bool IsSecret { get; private set; }
        public AchievementTier Tier { get; private set; }
        public IReadOnlyList<IAchievementCriterion> Criteria { get; private set; }
        public IReadOnlyList<IAchievementReward> Rewards { get; private set; }
        public IReadOnlyList<string> Prerequisites { get; private set; }

        /// <summary>
        /// Create an achievement with full parameters.
        /// </summary>
        public Achievement(
            string achievementId,
            string name,
            string description,
            string category,
            int points,
            AchievementTier tier,
            IReadOnlyList<IAchievementCriterion> criteria,
            IReadOnlyList<IAchievementReward> rewards,
            IReadOnlyList<string> prerequisites,
            bool isHidden = false,
            bool isSecret = false)
        {
            if (string.IsNullOrEmpty(achievementId))
                throw new ArgumentException("Achievement ID cannot be null or empty", nameof(achievementId));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Achievement name cannot be null or empty", nameof(name));

            AchievementId = achievementId;
            Name = name;
            Description = description ?? string.Empty;
            Category = category ?? string.Empty;
            Points = Math.Max(0, points);
            Tier = tier;
            Criteria = criteria ?? new List<IAchievementCriterion>();
            Rewards = rewards ?? new List<IAchievementReward>();
            Prerequisites = prerequisites ?? new List<string>();
            IsHidden = isHidden;
            IsSecret = isSecret;
        }

        /// <summary>
        /// Factory method to create a simple achievement without rewards or prerequisites.
        /// </summary>
        public static Achievement Create(
            string achievementId,
            string name,
            string description,
            AchievementTier tier,
            IReadOnlyList<IAchievementCriterion> criteria,
            int points = 10)
        {
            return new Achievement(
                achievementId,
                name,
                description,
                string.Empty,
                points,
                tier,
                criteria,
                new List<IAchievementReward>(),
                new List<string>());
        }

        public override string ToString()
        {
            return $"Achievement[{AchievementId}]: {Name} ({Tier}) - {Points} points";
        }
    }
}
