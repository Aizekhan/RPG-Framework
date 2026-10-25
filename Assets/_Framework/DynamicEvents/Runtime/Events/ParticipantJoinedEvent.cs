using System;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when a participant joins a dynamic event.
    /// </summary>
    public class ParticipantJoinedEvent
    {
        /// <summary>
        /// Event ID that was joined.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Participant entity ID.
        /// </summary>
        public string ParticipantId { get; }

        /// <summary>
        /// New participant count after joining.
        /// </summary>
        public int NewParticipantCount { get; }

        public ParticipantJoinedEvent(string eventId, string participantId, int newParticipantCount)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            NewParticipantCount = newParticipantCount;
        }
    }
}
