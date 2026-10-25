using System;
using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when a dynamic event completes (success or failure).
    /// </summary>
    public class EventCompletedEvent
    {
        /// <summary>
        /// Event ID that completed.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Final state of the event.
        /// </summary>
        public EventState FinalState { get; }

        /// <summary>
        /// Total participants.
        /// </summary>
        public int TotalParticipants { get; }

        /// <summary>
        /// Rewards distributed to participants.
        /// </summary>
        public IReadOnlyDictionary<string, object> Rewards { get; }

        public EventCompletedEvent(
            string eventId,
            EventState finalState,
            int totalParticipants,
            IReadOnlyDictionary<string, object> rewards)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            FinalState = finalState;
            TotalParticipants = totalParticipants;
            Rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        }
    }
}
