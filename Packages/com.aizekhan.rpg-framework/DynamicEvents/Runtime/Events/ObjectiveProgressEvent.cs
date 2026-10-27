using System;
using RPGFramework.Events;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when an event objective progresses.
    /// Published by <see cref="EventManager.UpdateObjectiveProgress"/> when the objective's count actually changed.
    /// </summary>
    public class ObjectiveProgressEvent : IEvent
    {
        /// <summary>
        /// Event ID containing the objective.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Objective ID that progressed.
        /// </summary>
        public string ObjectiveId { get; }

        /// <summary>
        /// Current progress count.
        /// </summary>
        public int CurrentCount { get; }

        /// <summary>
        /// Required count for completion.
        /// </summary>
        public int RequiredCount { get; }

        /// <summary>
        /// Whether the objective is now complete.
        /// </summary>
        public bool IsComplete { get; }

        /// <summary>
        /// Optional contributor ID who made progress.
        /// </summary>
        public string? ContributorId { get; }

        public ObjectiveProgressEvent(
            string eventId,
            string objectiveId,
            int currentCount,
            int requiredCount,
            bool isComplete,
            string? contributorId = null)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            ObjectiveId = objectiveId ?? throw new ArgumentNullException(nameof(objectiveId));
            CurrentCount = currentCount;
            RequiredCount = requiredCount;
            IsComplete = isComplete;
            ContributorId = contributorId;
        }
    }
}
