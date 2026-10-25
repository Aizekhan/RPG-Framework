namespace RPGFramework.Quests
{
    /// <summary>
    /// Represents a reward given upon quest completion.
    /// </summary>
    public interface IQuestReward
    {
        /// <summary>
        /// Type of reward (Item, Experience, Currency, etc.).
        /// </summary>
        RewardType Type { get; }

        /// <summary>
        /// Identifier for the reward (item ID, unlock ID, etc.).
        /// </summary>
        string RewardId { get; }

        /// <summary>
        /// Amount of the reward (item count, XP amount, currency amount).
        /// </summary>
        int Amount { get; }
    }
}
