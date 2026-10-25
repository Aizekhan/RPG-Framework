namespace RPGFramework.Achievements.Runtime.Contracts
{
    /// <summary>
    /// Represents a single criterion that must be met to unlock an achievement.
    /// Criteria track progress towards completion (e.g., "Kill 100 enemies").
    /// </summary>
    public interface IAchievementCriterion
    {
        /// <summary>
        /// Unique identifier for this criterion
        /// </summary>
        string CriterionId { get; }

        /// <summary>
        /// Type of criterion (e.g., KillCount, ItemCollected, QuestCompleted)
        /// </summary>
        CriterionType Type { get; }

        /// <summary>
        /// Target identifier (e.g., enemy ID, item ID, quest ID)
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// Required count to complete this criterion
        /// </summary>
        int RequiredCount { get; }

        /// <summary>
        /// Description of this criterion
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Whether this criterion must be completed in a single session
        /// </summary>
        bool RequiresSingleSession { get; }
    }
}
