namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Represents a participant in a dynamic event.
    /// Tracks contribution and determines reward eligibility.
    /// </summary>
    public interface IEventParticipant
    {
        /// <summary>
        /// Entity ID of the participant.
        /// </summary>
        string ParticipantId { get; }

        /// <summary>
        /// Event ID this participant is participating in.
        /// </summary>
        string EventId { get; }

        /// <summary>
        /// Total contribution score accumulated.
        /// </summary>
        float ContributionScore { get; }

        /// <summary>
        /// Participation level based on contribution.
        /// </summary>
        ParticipationLevel ParticipationLevel { get; }

        /// <summary>
        /// Time when the participant joined the event.
        /// </summary>
        float JoinTime { get; }

        /// <summary>
        /// Time when the participant left the event (0 if still active).
        /// </summary>
        float LeaveTime { get; }

        /// <summary>
        /// Whether the participant is still active in the event.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Whether the participant is eligible for rewards.
        /// </summary>
        bool IsEligibleForRewards { get; }

        /// <summary>
        /// Adds contribution to the participant's score.
        /// </summary>
        /// <param name="amount">Amount of contribution to add.</param>
        void AddContribution(float amount);

        /// <summary>
        /// Marks the participant as having left the event.
        /// </summary>
        /// <param name="currentTime">Current time when participant left.</param>
        void Leave(float currentTime);

        /// <summary>
        /// Updates the participation level based on current contribution.
        /// </summary>
        void UpdateParticipationLevel();
    }
}
