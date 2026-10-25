using System;
using RPGFramework.Achievements.Runtime;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Implementation of IAchievementReward.
    /// Represents a reward given upon achievement unlock.
    /// </summary>
    public class AchievementReward : IAchievementReward
    {
        public RewardType Type { get; private set; }
        public string RewardId { get; private set; }
        public int Amount { get; private set; }
        public string Description { get; private set; }

        /// <summary>
        /// Create an achievement reward.
        /// </summary>
        public AchievementReward(
            RewardType type,
            string rewardId,
            int amount,
            string description = "")
        {
            if (string.IsNullOrEmpty(rewardId))
                throw new ArgumentException("Reward ID cannot be null or empty", nameof(rewardId));
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));

            Type = type;
            RewardId = rewardId;
            Amount = amount;
            Description = description ?? string.Empty;
        }

        /// <summary>
        /// Factory method to create a currency reward.
        /// </summary>
        public static AchievementReward CreateGoldReward(int amount)
        {
            return new AchievementReward(RewardType.Gold, "gold", amount, $"{amount} gold");
        }

        /// <summary>
        /// Factory method to create an experience reward.
        /// </summary>
        public static AchievementReward CreateExperienceReward(int amount)
        {
            return new AchievementReward(RewardType.Experience, "xp", amount, $"{amount} experience");
        }

        /// <summary>
        /// Factory method to create an item reward.
        /// </summary>
        public static AchievementReward CreateItemReward(string itemId, int quantity = 1)
        {
            return new AchievementReward(RewardType.Item, itemId, quantity, $"{quantity}x {itemId}");
        }

        public override string ToString()
        {
            return $"Reward[{Type}]: {Amount}x {RewardId}";
        }
    }
}
