namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Commands that can be issued to companions.
    /// </summary>
    public enum CompanionCommand
    {
        /// <summary>
        /// Follow the owner at a set distance.
        /// </summary>
        Follow = 0,

        /// <summary>
        /// Stay at current location.
        /// </summary>
        Stay = 1,

        /// <summary>
        /// Attack the designated target.
        /// </summary>
        Attack = 2,

        /// <summary>
        /// Defend the owner or a specific target.
        /// </summary>
        Defend = 3,

        /// <summary>
        /// Patrol between designated points.
        /// </summary>
        Patrol = 4,

        /// <summary>
        /// Return to owner's position immediately.
        /// </summary>
        Return = 5,

        /// <summary>
        /// Stop current action and idle.
        /// </summary>
        Stop = 6,

        /// <summary>
        /// Use companion's special ability.
        /// </summary>
        UseAbility = 7
    }
}
