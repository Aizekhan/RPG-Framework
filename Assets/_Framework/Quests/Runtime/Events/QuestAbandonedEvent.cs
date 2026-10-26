using RPGFramework.Events;

namespace RPGFramework.Quests
{
    public struct QuestAbandonedEvent : IEvent
    {
        public int EntityId;
        public string QuestId;
    }
}
