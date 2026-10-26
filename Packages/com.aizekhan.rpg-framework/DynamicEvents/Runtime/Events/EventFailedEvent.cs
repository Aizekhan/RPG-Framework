using System;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when a dynamic event fails.
    /// </summary>
    public class EventFailedEvent
    {
        /// <summary>
        /// Event ID that failed.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Reason for failure.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Number of participants when the event failed.
        /// </summary>
        public int ParticipantCount { get; }

        /// <summary>
        /// Optional next event ID in a chain (failure branch).
        /// </summary>
        public string? NextEventId { get; }

        public EventFailedEvent(string eventId, string reason, int participantCount, string? nextEventId = null)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            ParticipantCount = participantCount;
            NextEventId = nextEventId;
        }
    }
}
