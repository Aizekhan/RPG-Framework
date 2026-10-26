namespace RPGFramework.Combat
{
    /// <summary>
    /// Represents the life state of an entity in the RPG system.
    /// </summary>
    /// <remarks>
    /// Life states define the current existence state of an entity, from alive to dead,
    /// including transitional states like dying and resurrecting.
    /// </remarks>
    public enum LifeState
    {
        /// <summary>
        /// Entity is alive and can perform actions normally.
        /// </summary>
        Alive = 0,

        /// <summary>
        /// Entity is transitioning to death (allows for death animations, effects, etc.).
        /// </summary>
        /// <remarks>
        /// Game layer can use this state to play death animations before transitioning to Dead.
        /// </remarks>
        Dying = 1,

        /// <summary>
        /// Entity is dead and cannot perform actions.
        /// </summary>
        Dead = 2,

        /// <summary>
        /// Entity is transitioning from dead to alive (resurrection/respawn in progress).
        /// </summary>
        /// <remarks>
        /// Game layer can use this state to play resurrection animations before transitioning to Alive.
        /// </remarks>
        Resurrecting = 3,

        /// <summary>
        /// Entity is temporarily invulnerable and cannot die.
        /// </summary>
        /// <remarks>
        /// Useful for temporary immunity effects, cutscenes, or game mechanics that prevent death.
        /// Invulnerable entities can still take damage but will not transition to Dying or Dead states.
        /// </remarks>
        Invulnerable = 4
    }
}
