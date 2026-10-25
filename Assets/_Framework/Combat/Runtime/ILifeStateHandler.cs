namespace RPGFramework.Combat
{
    /// <summary>
    /// Represents an entity that has a life state and can respond to life/death events.
    /// </summary>
    /// <remarks>
    /// This interface provides the contract for entities that participate in the life/death system.
    /// Implementers should handle state transitions and provide lifecycle callbacks.
    /// </remarks>
    public interface ILifeStateHandler
    {
        /// <summary>
        /// Gets the current life state of this entity.
        /// </summary>
        LifeState CurrentState { get; }

        /// <summary>
        /// Gets whether this entity can die.
        /// </summary>
        /// <remarks>
        /// Some entities may be marked as immortal or protected by game rules.
        /// </remarks>
        bool CanDie { get; }

        /// <summary>
        /// Gets whether this entity can be resurrected.
        /// </summary>
        /// <remarks>
        /// Some death types may be permanent (e.g., permadeath mode).
        /// </remarks>
        bool CanResurrect { get; }

        /// <summary>
        /// Called when the entity dies.
        /// </summary>
        /// <param name="killerId">ID of the entity that caused death, or -1 if environmental/self.</param>
        /// <param name="reason">Reason for death (e.g., "resource_depletion", "instant_death").</param>
        void OnDeath(int killerId, string reason);

        /// <summary>
        /// Called when the entity is resurrected.
        /// </summary>
        /// <param name="resurrectorId">ID of the entity that performed resurrection, or -1 if self-resurrection.</param>
        /// <param name="resurrectType">Type of resurrection (e.g., "respawn", "player_revive").</param>
        void OnResurrect(int resurrectorId, string resurrectType);

        /// <summary>
        /// Called when the life state changes.
        /// </summary>
        /// <param name="oldState">Previous life state.</param>
        /// <param name="newState">New life state.</param>
        void OnLifeStateChanged(LifeState oldState, LifeState newState);

        /// <summary>
        /// Gets whether the entity is alive.
        /// </summary>
        bool IsAlive();

        /// <summary>
        /// Gets whether the entity is dead.
        /// </summary>
        bool IsDead();

        /// <summary>
        /// Gets whether the entity is currently invulnerable.
        /// </summary>
        bool IsInvulnerable();
    }
}
