using System;
using RPGFramework.Events;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Event raised when a dynamic event is spawned.
    /// Published by <see cref="EventManager"/> during <c>Update</c> when a registered spawner reports a spawn.
    /// The manager does not build the event from the spawner's template; create it from your handler if you need it.
    /// </summary>
    public class EventSpawnedEvent : IEvent
    {
        /// <summary>
        /// Event ID that was spawned.
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// Event template ID.
        /// </summary>
        public string TemplateId { get; }

        /// <summary>
        /// Location where the event spawned.
        /// </summary>
        public string LocationId { get; }

        /// <summary>
        /// Spawner that created the event.
        /// </summary>
        public string SpawnerId { get; }

        public EventSpawnedEvent(string eventId, string templateId, string locationId, string spawnerId)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
            LocationId = locationId ?? throw new ArgumentNullException(nameof(locationId));
            SpawnerId = spawnerId ?? throw new ArgumentNullException(nameof(spawnerId));
        }
    }
}
