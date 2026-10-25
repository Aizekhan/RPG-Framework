namespace RPGFramework.Quests
{
    /// <summary>
    /// Type of quest objective.
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>
        /// Kill a specific number of enemies.
        /// </summary>
        Kill,

        /// <summary>
        /// Collect a specific number of items.
        /// </summary>
        Collect,

        /// <summary>
        /// Interact with a specific object or NPC.
        /// </summary>
        Interact,

        /// <summary>
        /// Explore or reach a specific location.
        /// </summary>
        Explore,

        /// <summary>
        /// Escort an NPC to a destination.
        /// </summary>
        Escort,

        /// <summary>
        /// Custom objective type (game-specific).
        /// </summary>
        Custom
    }
}
