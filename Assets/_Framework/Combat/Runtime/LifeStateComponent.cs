namespace RPGFramework.Combat
{
    /// <summary>
    /// Component for tracking an entity's life state.
    /// </summary>
    /// <remarks>
    /// This component stores the current life state and related properties.
    /// It is designed to be attached to entities in an ECS system.
    /// </remarks>
    public struct LifeStateComponent
    {
        /// <summary>
        /// Current life state of the entity.
        /// </summary>
        public LifeState CurrentState;

        /// <summary>
        /// Whether the entity can die.
        /// </summary>
        /// <remarks>
        /// Some entities may be marked as immortal or protected by game rules.
        /// Set to false to prevent death (e.g., essential NPCs, invincible bosses).
        /// </remarks>
        public bool CanDie;

        /// <summary>
        /// Whether the entity can be resurrected.
        /// </summary>
        /// <remarks>
        /// Set to false for permanent death scenarios (e.g., permadeath mode, final boss kills).
        /// </remarks>
        public bool CanResurrect;

        /// <summary>
        /// Time when invulnerability ends.
        /// </summary>
        /// <remarks>
        /// Use -1 for no invulnerability.
        /// Use float.MaxValue for permanent invulnerability.
        /// Compare with game time to check if invulnerability has expired.
        /// </remarks>
        public float InvulnerabilityEndTime;

        /// <summary>
        /// Time when the entity died.
        /// </summary>
        /// <remarks>
        /// Use -1 if the entity is alive.
        /// Used for tracking death duration, respawn timers, etc.
        /// </remarks>
        public float DeathTime;

        /// <summary>
        /// ID of the entity that killed this entity.
        /// </summary>
        /// <remarks>
        /// Use -1 for environmental deaths or self-inflicted death.
        /// Used for tracking kill credits, revenge systems, etc.
        /// </remarks>
        public int LastKillerId;

        /// <summary>
        /// Reason for the last death.
        /// </summary>
        /// <remarks>
        /// Examples: "resource_depletion", "instant_death", "environmental".
        /// Empty string if entity has never died.
        /// </remarks>
        public string LastDeathReason;

        /// <summary>
        /// Creates a new life state component with default alive state.
        /// </summary>
        /// <returns>A new life state component initialized to alive state.</returns>
        public static LifeStateComponent CreateAlive()
        {
            return new LifeStateComponent
            {
                CurrentState = LifeState.Alive,
                CanDie = true,
                CanResurrect = true,
                InvulnerabilityEndTime = -1f,
                DeathTime = -1f,
                LastKillerId = -1,
                LastDeathReason = string.Empty
            };
        }

        /// <summary>
        /// Creates a new life state component with immortal settings.
        /// </summary>
        /// <returns>A new life state component that cannot die.</returns>
        public static LifeStateComponent CreateImmortal()
        {
            return new LifeStateComponent
            {
                CurrentState = LifeState.Alive,
                CanDie = false,
                CanResurrect = false,
                InvulnerabilityEndTime = float.MaxValue,
                DeathTime = -1f,
                LastKillerId = -1,
                LastDeathReason = string.Empty
            };
        }
    }
}
