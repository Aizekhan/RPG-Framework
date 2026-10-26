using RPGFramework.Events;

namespace RPGFramework.Quests
{
    public struct QuestFailedEvent : IEvent
    {
        public int EntityId;
        public string QuestId;
        public string FailureReason;
    }
}
