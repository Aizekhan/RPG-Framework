using System;
using RPGFramework.Events;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when a dynamic event starts.
    /// Published by <see cref="EventManager.CreateEvent"/> right after the new event is started.
    /// </summary>
    public class EventStartedEvent : IEvent
    {
        /// <summary>
        /// Event ID that started.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Event type.
        /// </summary>
        public EventType EventType { get; }

        /// <summary>
        /// Event name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Location where the event occurs.
        /// </summary>
        public string LocationId { get; }

        /// <summary>
        /// Event difficulty.
        /// </summary>
        public EventDifficulty Difficulty { get; }

        public EventStartedEvent(string eventId, EventType eventType, string name, string locationId, EventDifficulty difficulty)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            EventType = eventType;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            LocationId = locationId ?? throw new ArgumentNullException(nameof(locationId));
            Difficulty = difficulty;
        }
    }
}
