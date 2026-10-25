namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Current state of a dynamic event.
    /// </summary>
    public enum EventState
    {
        /// <summary>
        /// Event is pending and not yet started.
        /// </summary>
        Pending,

        /// <summary>
        /// Event is actively in progress.
        /// </summary>
        Active,

        /// <summary>
        /// Event was successful.
        /// </summary>
        Success,

        /// <summary>
        /// Event failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Event has completed (either success or failed).
        /// </summary>
        Completed
    }
}
