using System;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Serializable quest reward data.
    /// </summary>
    [Serializable]
    public struct QuestRewardData
    {
        public RewardType Type;
        public string RewardId;
        public int Amount;
    }
}
