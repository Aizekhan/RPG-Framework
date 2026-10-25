namespace RPGFramework.Quests
{
    public struct ObjectiveProgressedEvent
    {
        public int EntityId;
        public string QuestId;
        public string ObjectiveId;
        public int CurrentProgress;
        public int RequiredProgress;
        public bool IsCompleted;
    }
}
