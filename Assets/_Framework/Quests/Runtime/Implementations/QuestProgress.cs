using System;
using System.Collections.Generic;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuestProgress.
    /// Represents a player's progress on a specific quest.
    /// </summary>
    public class QuestProgress : IQuestProgress
    {
        public string QuestId { get; }
        public QuestState State { get; private set; }
        public IReadOnlyDictionary<string, int> ObjectiveProgress { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        /// <summary>
        /// Create a QuestProgress from QuestProgressData.
        /// </summary>
        public QuestProgress(QuestProgressData data)
        {
            QuestId = data.QuestId;
            State = data.State;
            ObjectiveProgress = data.ObjectiveProgress ?? new Dictionary<string, int>();
            StartedAt = data.GetStartedAt();
            CompletedAt = data.GetCompletedAt();
        }

        /// <summary>
        /// Create a new QuestProgress for a quest start.
        /// </summary>
        public QuestProgress(string questId)
        {
            QuestId = questId;
            State = QuestState.Active;
            ObjectiveProgress = new Dictionary<string, int>();
            StartedAt = DateTime.UtcNow;
            CompletedAt = null;
        }

        /// <summary>
        /// Update objective progress.
        /// </summary>
        public void UpdateObjectiveProgress(string objectiveId, int newProgress)
        {
            if (ObjectiveProgress is Dictionary<string, int> dict)
            {
                dict[objectiveId] = newProgress;
            }
        }

        /// <summary>
        /// Mark the quest as completed.
        /// </summary>
        public void Complete()
        {
            State = QuestState.Completed;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Mark the quest as abandoned.
        /// </summary>
        public void Abandon()
        {
            State = QuestState.Abandoned;
        }

        /// <summary>
        /// Mark the quest as failed.
        /// </summary>
        public void Fail()
        {
            State = QuestState.Failed;
        }

        /// <summary>
        /// Convert to QuestProgressData for storage.
        /// </summary>
        public QuestProgressData ToData()
        {
            return new QuestProgressData
            {
                QuestId = QuestId,
                State = State,
                ObjectiveProgress = new Dictionary<string, int>(ObjectiveProgress),
                StartedAtTicks = StartedAt?.Ticks ?? 0,
                CompletedAtTicks = CompletedAt?.Ticks
            };
        }
    }
}
