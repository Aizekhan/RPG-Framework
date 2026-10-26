namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Current state of an active dialogue.
    /// </summary>
    public enum DialogueState
    {
        /// <summary>
        /// Dialogue has not been started.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Dialogue is currently active and in progress.
        /// </summary>
        Active,

        /// <summary>
        /// Dialogue has been completed naturally (reached End node).
        /// </summary>
        Completed,

        /// <summary>
        /// Dialogue was interrupted or cancelled before completion.
        /// </summary>
        Interrupted
    }
}
