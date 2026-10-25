using System;
using System.Collections.Generic;

namespace RPGFramework.Quests
{
    public struct QuestCompletedEvent
    {
        public int EntityId;
        public string QuestId;
        public DateTime CompletedAt;
        public List<IQuestReward> Rewards;
    }
}
