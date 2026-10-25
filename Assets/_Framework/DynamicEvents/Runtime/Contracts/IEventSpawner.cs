using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Spawns dynamic events based on rules and world state.
    /// </summary>
    public interface IEventSpawner
    {
        /// <summary>
        /// Unique identifier for this spawner.
        /// </summary>
        string SpawnerId { get; }

        /// <summary>
        /// Event template ID that this spawner creates.
        /// </summary>
        string EventTemplateId { get; }

        /// <summary>
        /// Location where events spawn.
        /// </summary>
        string LocationId { get; }

        /// <summary>
        /// Spawn rules that determine when events spawn.
        /// </summary>
        EventSpawnRule SpawnRule { get; }

        /// <summary>
        /// Whether this spawner is currently active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Time until next spawn attempt (in seconds).
        /// </summary>
        float TimeUntilNextSpawn { get; }

        /// <summary>
        /// Maximum number of concurrent events from this spawner.
        /// </summary>
        int MaxConcurrentEvents { get; }

        /// <summary>
        /// Current number of active events from this spawner.
        /// </summary>
        int ActiveEventCount { get; }

        /// <summary>
        /// Updates the spawner state and attempts to spawn events.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        /// <param name="worldState">Current world state for condition checking.</param>
        /// <returns>Event ID if an event was spawned, null otherwise.</returns>
        string? Update(float deltaTime, IReadOnlyDictionary<string, object> worldState);

        /// <summary>
        /// Checks if spawn conditions are met.
        /// </summary>
        /// <param name="worldState">Current world state.</param>
        /// <returns>True if an event can spawn.</returns>
        bool CanSpawn(IReadOnlyDictionary<string, object> worldState);

        /// <summary>
        /// Notifies the spawner that one of its events ended.
        /// </summary>
        /// <param name="eventId">Event ID that ended.</param>
        void OnEventEnded(string eventId);
    }
}
