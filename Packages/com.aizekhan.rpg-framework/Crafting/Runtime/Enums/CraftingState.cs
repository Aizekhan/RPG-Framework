namespace RPGFramework.Crafting
{
    /// <summary>
    /// State of an active crafting operation.
    /// </summary>
    public enum CraftingState
    {
        /// <summary>
        /// No crafting in progress.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Crafting is currently in progress.
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Crafting has been completed successfully.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Crafting failed and produced no results.
        /// </summary>
        Failed = 3
    }
}
