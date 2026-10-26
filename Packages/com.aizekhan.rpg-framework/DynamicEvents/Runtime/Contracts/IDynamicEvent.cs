using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Represents a dynamic world event that players can participate in.
    /// Events spawn dynamically, support shared participation, and have time limits.
    /// </summary>
    public interface IDynamicEvent
    {
        /// <summary>
        /// Unique identifier for this event instance.
        /// </summary>
        string EventId { get; }

        /// <summary>
        /// Type of event (Defense, Attack, Escort, etc.).
        /// </summary>
        EventType EventType { get; }

        /// <summary>
        /// Current state of the event.
        /// </summary>
        EventState State { get; }

        /// <summary>
        /// Difficulty level of this event.
        /// </summary>
        EventDifficulty Difficulty { get; }

        /// <summary>
        /// Display name of the event.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of the event shown to players.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// World location where the event occurs (zone/area identifier).
        /// </summary>
        string LocationId { get; }

        /// <summary>
        /// Radius from location where players can participate (in world units).
        /// </summary>
        float ParticipationRadius { get; }

        /// <summary>
        /// Time remaining for the event in seconds (-1 for unlimited).
        /// </summary>
        float TimeRemaining { get; }

        /// <summary>
        /// Maximum duration of the event in seconds.
        /// </summary>
        float MaxDuration { get; }

        /// <summary>
        /// Current number of active participants.
        /// </summary>
        int ParticipantCount { get; }

        /// <summary>
        /// Objectives that must be completed for event success.
        /// </summary>
        IReadOnlyList<IEventObjective> Objectives { get; }

        /// <summary>
        /// Optional next event ID if this is part of a chain.
        /// </summary>
        string? NextEventId { get; }

        /// <summary>
        /// Tags for categorization and filtering.
        /// </summary>
        IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Starts the event.
        /// </summary>
        void Start();

        /// <summary>
        /// Updates the event state (call each frame/tick).
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        void Update(float deltaTime);

        /// <summary>
        /// Ends the event with the specified outcome.
        /// </summary>
        /// <param name="success">Whether the event was successful.</param>
        void End(bool success);

        /// <summary>
        /// Checks if a participant meets the requirements to join.
        /// </summary>
        /// <param name="participantId">Participant entity ID.</param>
        /// <returns>True if the participant can join.</returns>
        bool CanJoin(string participantId);

        /// <summary>
        /// Gets the overall progress of the event (0.0 to 1.0).
        /// </summary>
        float GetProgress();
    }
}
