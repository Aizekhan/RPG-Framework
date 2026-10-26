using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Manages the lifecycle of dynamic events, participants, and rewards.
    /// Handles event scaling, progression, and completion.
    /// </summary>
    public interface IEventManager
    {
        /// <summary>
        /// All currently active events.
        /// </summary>
        IReadOnlyDictionary<string, IDynamicEvent> ActiveEvents { get; }

        /// <summary>
        /// All registered event spawners.
        /// </summary>
        IReadOnlyList<IEventSpawner> Spawners { get; }

        /// <summary>
        /// Creates and starts a new event.
        /// </summary>
        /// <param name="templateId">Event template identifier.</param>
        /// <param name="locationId">World location where the event occurs.</param>
        /// <returns>The created event.</returns>
        IDynamicEvent CreateEvent(string templateId, string locationId);

        /// <summary>
        /// Gets an active event by ID.
        /// </summary>
        /// <param name="eventId">Event ID to find.</param>
        /// <returns>The event, or null if not found.</returns>
        IDynamicEvent? GetEvent(string eventId);

        /// <summary>
        /// Gets all events at a specific location.
        /// </summary>
        /// <param name="locationId">Location to search.</param>
        /// <returns>List of events at that location.</returns>
        IReadOnlyList<IDynamicEvent> GetEventsAtLocation(string locationId);

        /// <summary>
        /// Adds a participant to an event.
        /// </summary>
        /// <param name="eventId">Event ID to join.</param>
        /// <param name="participantId">Participant entity ID.</param>
        /// <returns>The created participant, or null if join failed.</returns>
        IEventParticipant? JoinEvent(string eventId, string participantId);

        /// <summary>
        /// Removes a participant from an event.
        /// </summary>
        /// <param name="eventId">Event ID to leave.</param>
        /// <param name="participantId">Participant entity ID.</param>
        void LeaveEvent(string eventId, string participantId);

        /// <summary>
        /// Gets a participant's data for an event.
        /// </summary>
        /// <param name="eventId">Event ID.</param>
        /// <param name="participantId">Participant entity ID.</param>
        /// <returns>Participant data, or null if not found.</returns>
        IEventParticipant? GetParticipant(string eventId, string participantId);

        /// <summary>
        /// Gets all participants in an event.
        /// </summary>
        /// <param name="eventId">Event ID.</param>
        /// <returns>List of participants.</returns>
        IReadOnlyList<IEventParticipant> GetParticipants(string eventId);

        /// <summary>
        /// Updates objective progress for an event.
        /// </summary>
        /// <param name="eventId">Event ID.</param>
        /// <param name="objectiveId">Objective ID.</param>
        /// <param name="amount">Amount to progress.</param>
        /// <param name="contributorId">Optional contributor ID.</param>
        void UpdateObjectiveProgress(string eventId, string objectiveId, int amount, string? contributorId = null);

        /// <summary>
        /// Scales event difficulty based on participant count.
        /// </summary>
        /// <param name="eventId">Event ID to scale.</param>
        void ScaleEvent(string eventId);

        /// <summary>
        /// Distributes rewards to eligible participants.
        /// </summary>
        /// <param name="eventId">Event ID.</param>
        /// <returns>Dictionary of participant IDs to their reward data.</returns>
        IReadOnlyDictionary<string, object> DistributeRewards(string eventId);

        /// <summary>
        /// Registers an event factory for creating events from templates.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="factory">Factory function to create events.</param>
        void RegisterEventFactory(string templateId, System.Func<string, string, IDynamicEvent> factory);

        /// <summary>
        /// Registers an event spawner.
        /// </summary>
        /// <param name="spawner">Spawner to register.</param>
        void RegisterSpawner(IEventSpawner spawner);

        /// <summary>
        /// Unregisters an event spawner.
        /// </summary>
        /// <param name="spawnerId">Spawner ID to unregister.</param>
        void UnregisterSpawner(string spawnerId);

        /// <summary>
        /// Updates all active events and spawners.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <param name="worldState">Current world state.</param>
        void Update(float deltaTime, IReadOnlyDictionary<string, object> worldState);
    }
}
