using RPGFramework.Events;

namespace RPGFramework.Quests
{
    /// <summary>
    /// A quest entered a stage. Published when it is taken (into its first stage, <see cref="FromStageId"/> null)
    /// and on every move after; before <see cref="QuestCompletedEvent"/> or <see cref="QuestFailedEvent"/> when the
    /// stage ends the quest.
    /// </summary>
    public struct QuestStageChangedEvent : IEvent
    {
        public int EntityId;
        public string QuestId;
        public string FromStageId;
        public string ToStageId;

        /// <summary>The signalled condition that moved it; null when its objectives did, or the game moved it.</summary>
        public string Condition;

        public QuestOutcome Outcome;
    }
}
