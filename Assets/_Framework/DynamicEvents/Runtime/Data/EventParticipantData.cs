using RPGFramework.ECS;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// ECS component data for event participants.
    /// Tracks contribution and eligibility.
    /// </summary>
    public readonly struct EventParticipantData : IComponent
    {
        /// <summary>
        /// Participant entity ID.
        /// </summary>
        public string ParticipantId { get; }

        /// <summary>
        /// Event ID being participated in.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Total contribution score.
        /// </summary>
        public float ContributionScore { get; }

        /// <summary>
        /// Participation level based on contribution.
        /// </summary>
        public ParticipationLevel ParticipationLevel { get; }

        /// <summary>
        /// Time when joined (in game time).
        /// </summary>
        public float JoinTime { get; }

        /// <summary>
        /// Time when left (0 if still active).
        /// </summary>
        public float LeaveTime { get; }

        /// <summary>
        /// Whether eligible for rewards.
        /// </summary>
        public bool IsEligibleForRewards { get; }

        public EventParticipantData(
            string participantId,
            string eventId,
            float contributionScore,
            ParticipationLevel participationLevel,
            float joinTime,
            float leaveTime = 0f,
            bool isEligibleForRewards = true)
        {
            ParticipantId = participantId;
            EventId = eventId;
            ContributionScore = contributionScore;
            ParticipationLevel = participationLevel;
            JoinTime = joinTime;
            LeaveTime = leaveTime;
            IsEligibleForRewards = isEligibleForRewards;
        }
    }
}
