using System;
using RPGFramework.Events;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when a dynamic event succeeds.
    /// Published by <see cref="EventManager"/> during <c>Update</c> when it removes an event that ended in success;
    /// <c>CompletionTime</c> is the time the manager updated the event for, and <c>ParticipantCount</c> its active participants.
    /// </summary>
    public class EventSucceededEvent : IEvent
    {
        /// <summary>
        /// Event ID that succeeded.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Number of participants who completed the event.
        /// </summary>
        public int ParticipantCount { get; }

        /// <summary>
        /// Time taken to complete the event in seconds.
        /// </summary>
        public float CompletionTime { get; }

        /// <summary>
        /// Optional next event ID in a chain.
        /// </summary>
        public string? NextEventId { get; }

        public EventSucceededEvent(string eventId, int participantCount, float completionTime, string? nextEventId = null)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            ParticipantCount = participantCount;
            CompletionTime = completionTime;
            NextEventId = nextEventId;
        }
    }
}
