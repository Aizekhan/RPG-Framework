using System;
using System.Collections.Generic;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Serializable quest progress data.
    /// </summary>
    [Serializable]
    public struct QuestProgressData
    {
        public string QuestId;
        public QuestState State;
        public Dictionary<string, int> ObjectiveProgress;
        public long StartedAtTicks;
        public long? CompletedAtTicks;

        public DateTime? GetStartedAt()
        {
            return StartedAtTicks > 0 ? new DateTime(StartedAtTicks) : null;
        }

        public DateTime? GetCompletedAt()
        {
            return CompletedAtTicks.HasValue ? new DateTime(CompletedAtTicks.Value) : null;
        }
    }
}
