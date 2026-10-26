using RPGFramework.ECS;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// ECS component data for active dynamic events.
    /// </summary>
    public readonly struct DynamicEventData : IComponent
    {
        /// <summary>
        /// Unique event instance identifier.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Event template identifier.
        /// </summary>
        public string TemplateId { get; }

        /// <summary>
        /// Type of event.
        /// </summary>
        public EventType EventType { get; }

        /// <summary>
        /// Current state of the event.
        /// </summary>
        public EventState State { get; }

        /// <summary>
        /// Difficulty level.
        /// </summary>
        public EventDifficulty Difficulty { get; }

        /// <summary>
        /// Location identifier where the event occurs.
        /// </summary>
        public string LocationId { get; }

        /// <summary>
        /// Time remaining in seconds.
        /// </summary>
        public float TimeRemaining { get; }

        /// <summary>
        /// Maximum duration in seconds.
        /// </summary>
        public float MaxDuration { get; }

        /// <summary>
        /// Current participant count.
        /// </summary>
        public int ParticipantCount { get; }

        /// <summary>
        /// Optional chain ID if part of an event chain.
        /// </summary>
        public string ChainId { get; }

        public DynamicEventData(
            string eventId,
            string templateId,
            EventType eventType,
            EventState state,
            EventDifficulty difficulty,
            string locationId,
            float timeRemaining,
            float maxDuration,
            int participantCount = 0,
            string? chainId = null)
        {
            EventId = eventId;
            TemplateId = templateId;
            EventType = eventType;
            State = state;
            Difficulty = difficulty;
            LocationId = locationId;
            TimeRemaining = timeRemaining;
            MaxDuration = maxDuration;
            ParticipantCount = participantCount;
            ChainId = chainId;
        }
    }
}
