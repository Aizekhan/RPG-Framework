namespace RPGFramework.Achievements.Runtime.Contracts
{
    /// <summary>
    /// Represents an achievement definition with unlock criteria and rewards.
    /// Achievements track player accomplishments across multiple categories.
    /// </summary>
    public interface IAchievement
    {
        /// <summary>
        /// Unique identifier for this achievement
        /// </summary>
        string AchievementId { get; }

        /// <summary>
        /// Display name of the achievement
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of how to unlock this achievement
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Achievement category for organization
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Points awarded when unlocked (for meta-progression)
        /// </summary>
        int Points { get; }

        /// <summary>
        /// Whether this is a hidden achievement (not shown until unlocked)
        /// </summary>
        bool IsHidden { get; }

        /// <summary>
        /// Whether this is a secret achievement (criteria not shown)
        /// </summary>
        bool IsSecret { get; }

        /// <summary>
        /// Achievement rarity/difficulty tier
        /// </summary>
        AchievementTier Tier { get; }

        /// <summary>
        /// List of criteria that must be met to unlock
        /// </summary>
        System.Collections.Generic.IReadOnlyList<IAchievementCriterion> Criteria { get; }

        /// <summary>
        /// Optional rewards granted upon unlock
        /// </summary>
        System.Collections.Generic.IReadOnlyList<IAchievementReward> Rewards { get; }

        /// <summary>
        /// Optional prerequisite achievements (must be unlocked first)
        /// </summary>
        System.Collections.Generic.IReadOnlyList<string> Prerequisites { get; }
    }
}
