namespace RPGFramework.Achievements.Runtime
{
    /// <summary>
    /// Represents a reward given upon achievement unlock.
    /// </summary>
    public interface IAchievementReward
    {
        /// <summary>
        /// Type of reward (Gold, Experience, Item, Title, Cosmetic, Stat Bonus).
        /// </summary>
        RewardType Type { get; }

        /// <summary>
        /// Identifier for the reward (item ID, title ID, cosmetic ID, etc.).
        /// </summary>
        string RewardId { get; }

        /// <summary>
        /// Amount of the reward (gold amount, XP amount, stat bonus value).
        /// </summary>
        int Amount { get; }

        /// <summary>
        /// Optional description of the reward.
        /// </summary>
        string Description { get; }
    }
}
