using System;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Represents a single objective within a dynamic event.
    /// Supports shared progress tracking across all participants.
    /// </summary>
    public interface IEventObjective
    {
        /// <summary>
        /// Unique identifier for this objective.
        /// </summary>
        string ObjectiveId { get; }

        /// <summary>
        /// Event ID this objective belongs to.
        /// </summary>
        string EventId { get; }

        /// <summary>
        /// Display description of the objective.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Type of objective (similar to quest objectives).
        /// </summary>
        string ObjectiveType { get; }

        /// <summary>
        /// Target identifier (enemy type, item ID, location ID).
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// Current progress count (shared across all participants).
        /// </summary>
        int CurrentCount { get; }

        /// <summary>
        /// Required count to complete this objective.
        /// </summary>
        int RequiredCount { get; }

        /// <summary>
        /// Whether this objective is complete.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Whether this objective is optional for event success.
        /// </summary>
        bool IsOptional { get; }

        /// <summary>
        /// Whether this objective tracks individual contributions.
        /// </summary>
        bool TracksContribution { get; }

        /// <summary>
        /// Updates the progress of this objective.
        /// </summary>
        /// <param name="amount">Amount to add to progress.</param>
        /// <param name="contributorId">Optional ID of the contributor.</param>
        void UpdateProgress(int amount, string? contributorId = null);

        /// <summary>
        /// Gets the contribution of a specific participant to this objective.
        /// </summary>
        /// <param name="participantId">Participant entity ID.</param>
        /// <returns>Contribution amount, or 0 if not tracked.</returns>
        int GetParticipantContribution(string participantId);

        /// <summary>
        /// Gets the progress as a value between 0.0 and 1.0.
        /// </summary>
        float GetProgressPercentage();
    }
}
