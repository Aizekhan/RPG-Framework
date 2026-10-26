using System;
using System.Collections.Generic;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Represents a player's progress on a specific quest.
    /// </summary>
    public interface IQuestProgress
    {
        /// <summary>
        /// ID of the quest this progress tracks.
        /// </summary>
        string QuestId { get; }

        /// <summary>
        /// Current state of the quest.
        /// </summary>
        QuestState State { get; }

        /// <summary>
        /// Progress for each objective (objectiveId -> current count).
        /// </summary>
        IReadOnlyDictionary<string, int> ObjectiveProgress { get; }

        /// <summary>
        /// Timestamp when the quest was started (null if not started).
        /// </summary>
        DateTime? StartedAt { get; }

        /// <summary>
        /// Timestamp when the quest was completed (null if not completed).
        /// </summary>
        DateTime? CompletedAt { get; }
    }
}
