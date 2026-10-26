using RPGFramework.Events;

namespace RPGFramework.Quests
{
    public struct ObjectiveProgressedEvent : IEvent
    {
        public int EntityId;
        public string QuestId;
        public string ObjectiveId;
        public int CurrentProgress;
        public int RequiredProgress;
        public bool IsCompleted;
    }
}
