namespace RPGFramework.Quests
{
    public struct QuestFailedEvent
    {
        public int EntityId;
        public string QuestId;
        public string FailureReason;
    }
}
