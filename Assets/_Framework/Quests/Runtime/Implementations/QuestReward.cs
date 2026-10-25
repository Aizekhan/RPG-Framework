namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuestReward.
    /// Represents a reward given upon quest completion.
    /// </summary>
    public class QuestReward : IQuestReward
    {
        public RewardType Type { get; }
        public string RewardId { get; }
        public int Amount { get; }

        /// <summary>
        /// Create a QuestReward from QuestRewardData.
        /// </summary>
        public QuestReward(QuestRewardData data)
        {
            Type = data.Type;
            RewardId = data.RewardId;
            Amount = data.Amount;
        }

        /// <summary>
        /// Create a QuestReward directly with parameters.
        /// </summary>
        public QuestReward(
            RewardType type,
            string rewardId,
            int amount)
        {
            Type = type;
            RewardId = rewardId;
            Amount = amount;
        }
    }
}
