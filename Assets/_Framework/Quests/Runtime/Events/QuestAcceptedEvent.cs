using System;
using RPGFramework.Events;

namespace RPGFramework.Quests
{
    public struct QuestAcceptedEvent : IEvent
    {
        public int EntityId;
        public string QuestId;
        public DateTime AcceptedAt;
    }
}
