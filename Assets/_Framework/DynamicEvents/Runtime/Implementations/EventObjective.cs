using System;
using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Implementation of a shared event objective.
    /// </summary>
    public class EventObjective : IEventObjective
    {
        private readonly Dictionary<string, int> _participantContributions = new();

        public string ObjectiveId { get; }
        public string EventId { get; }
        public string Description { get; }
        public string ObjectiveType { get; }
        public string TargetId { get; }
        public int CurrentCount { get; private set; }
        public int RequiredCount { get; }
        public bool IsComplete => CurrentCount >= RequiredCount;
        public bool IsOptional { get; }
        public bool TracksContribution { get; }

        public EventObjective(
            string objectiveId,
            string eventId,
            string description,
            string objectiveType,
            string targetId,
            int requiredCount,
            bool isOptional = false,
            bool tracksContribution = true)
        {
            ObjectiveId = objectiveId ?? throw new ArgumentNullException(nameof(objectiveId));
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ObjectiveType = objectiveType ?? throw new ArgumentNullException(nameof(objectiveType));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            RequiredCount = requiredCount;
            IsOptional = isOptional;
            TracksContribution = tracksContribution;
            CurrentCount = 0;
        }

        public void UpdateProgress(int amount, string? contributorId = null)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));

            if (IsComplete)
                return;

            CurrentCount = Math.Min(CurrentCount + amount, RequiredCount);

            if (TracksContribution && !string.IsNullOrEmpty(contributorId))
            {
                if (!_participantContributions.ContainsKey(contributorId))
                    _participantContributions[contributorId] = 0;

                _participantContributions[contributorId] += amount;
            }
        }

        public int GetParticipantContribution(string participantId)
        {
            if (string.IsNullOrEmpty(participantId))
                throw new ArgumentException("Participant ID cannot be null or empty", nameof(participantId));

            return _participantContributions.TryGetValue(participantId, out var contribution)
                ? contribution
                : 0;
        }

        public float GetProgressPercentage()
        {
            if (RequiredCount <= 0)
                return 1.0f;

            return Math.Clamp((float)CurrentCount / RequiredCount, 0.0f, 1.0f);
        }
    }
}
