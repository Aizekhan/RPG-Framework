namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Represents a chain of events with branching based on success/failure.
    /// </summary>
    public interface IEventChain
    {
        /// <summary>
        /// Unique identifier for this event chain.
        /// </summary>
        string ChainId { get; }

        /// <summary>
        /// Display name of the event chain.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Current step in the chain (0-based index).
        /// </summary>
        int CurrentStep { get; }

        /// <summary>
        /// Total number of steps in the current branch.
        /// </summary>
        int TotalSteps { get; }

        /// <summary>
        /// Whether the chain is complete (reached an end state).
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Whether the chain succeeded (reached success end state).
        /// </summary>
        bool IsSuccessful { get; }

        /// <summary>
        /// Current active event ID in the chain.
        /// </summary>
        string? CurrentEventId { get; }

        /// <summary>
        /// Gets the next event template ID based on the current event outcome.
        /// </summary>
        /// <param name="currentEventId">Current event ID.</param>
        /// <param name="success">Whether the current event succeeded.</param>
        /// <returns>Next event template ID, or null if chain is complete.</returns>
        string? GetNextEvent(string currentEventId, bool success);

        /// <summary>
        /// Advances the chain to the next event.
        /// </summary>
        /// <param name="success">Whether the current event succeeded.</param>
        /// <returns>Next event template ID, or null if chain is complete.</returns>
        string? AdvanceChain(bool success);

        /// <summary>
        /// Resets the chain to the initial state.
        /// </summary>
        void Reset();

        /// <summary>
        /// Gets the description of the current step.
        /// </summary>
        string GetCurrentStepDescription();
    }
}
