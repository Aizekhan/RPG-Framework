using System;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Implementation of event participant tracking.
    /// </summary>
    public class EventParticipant : IEventParticipant
    {
        private static readonly float[] ParticipationThresholds = { 0f, 10f, 50f, 150f, 500f };

        public string ParticipantId { get; }
        public string EventId { get; }
        public float ContributionScore { get; private set; }
        public ParticipationLevel ParticipationLevel { get; private set; }
        public float JoinTime { get; }
        public float LeaveTime { get; private set; }
        public bool IsActive => !_hasLeft;
        public bool IsEligibleForRewards { get; private set; }

        private bool _hasLeft;

        public EventParticipant(string participantId, string eventId, float joinTime)
        {
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            JoinTime = joinTime;
            LeaveTime = 0f;
            ContributionScore = 0f;
            ParticipationLevel = ParticipationLevel.Observer;
            IsEligibleForRewards = false;
        }

        public void AddContribution(float amount)
        {
            if (amount < 0)
                throw new ArgumentException("Contribution amount cannot be negative", nameof(amount));

            if (!IsActive)
                return;

            ContributionScore += amount;
            UpdateParticipationLevel();
        }

        public void Leave(float currentTime)
        {
            if (!IsActive)
                return;

            LeaveTime = currentTime;
            _hasLeft = true;
            UpdateParticipationLevel();
        }

        public void UpdateParticipationLevel()
        {
            var oldLevel = ParticipationLevel;

            if (ContributionScore >= ParticipationThresholds[(int)ParticipationLevel.Legend])
                ParticipationLevel = ParticipationLevel.Legend;
            else if (ContributionScore >= ParticipationThresholds[(int)ParticipationLevel.Hero])
                ParticipationLevel = ParticipationLevel.Hero;
            else if (ContributionScore >= ParticipationThresholds[(int)ParticipationLevel.Contributor])
                ParticipationLevel = ParticipationLevel.Contributor;
            else if (ContributionScore >= ParticipationThresholds[(int)ParticipationLevel.Helper])
                ParticipationLevel = ParticipationLevel.Helper;
            else
                ParticipationLevel = ParticipationLevel.Observer;

            // Update reward eligibility
            IsEligibleForRewards = ParticipationLevel > ParticipationLevel.Observer;
        }
    }
}
