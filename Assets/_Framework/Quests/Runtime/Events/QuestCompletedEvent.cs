using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Quests
{
    public struct QuestCompletedEvent : IEvent
    {
        public int EntityId;
        public string QuestId;
        public DateTime CompletedAt;
        public List<IQuestReward> Rewards;
    }
}
