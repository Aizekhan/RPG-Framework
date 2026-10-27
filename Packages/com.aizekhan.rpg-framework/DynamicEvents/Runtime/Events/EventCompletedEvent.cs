using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when a dynamic event completes (success or failure).
    /// Published by <see cref="EventManager"/> during <c>Update</c> when it removes an ended event, after
    /// <see cref="EventSucceededEvent"/> or <see cref="EventFailedEvent"/>; <c>Rewards</c> is what
    /// <see cref="EventManager.DistributeRewards"/> returns for it and <c>TotalParticipants</c> counts everyone who joined.
    /// </summary>
    public class EventCompletedEvent : IEvent
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
