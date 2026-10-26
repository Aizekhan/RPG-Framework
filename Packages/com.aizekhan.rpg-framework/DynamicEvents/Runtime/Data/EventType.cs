namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Types of dynamic world events.
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// Defend a location from attackers.
        /// </summary>
        Defense,

        /// <summary>
        /// Attack an enemy position.
        /// </summary>
        Attack,

        /// <summary>
        /// Escort an NPC or caravan to safety.
        /// </summary>
        Escort,

        /// <summary>
        /// Gather resources or items.
        /// </summary>
        Gather,

        /// <summary>
        /// Boss encounter event.
        /// </summary>
        Boss,

        /// <summary>
        /// Large-scale invasion event.
        /// </summary>
        Invasion,

        /// <summary>
        /// Rescue NPCs or players.
        /// </summary>
        Rescue,

        /// <summary>
        /// Explore and discover new areas.
        /// </summary>
        Exploration
    }
}
