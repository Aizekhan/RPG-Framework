using System;

namespace RPGFramework.Quests
{
    public struct QuestAcceptedEvent
    {
        public int EntityId;
        public string QuestId;
        public DateTime AcceptedAt;
    }
}
