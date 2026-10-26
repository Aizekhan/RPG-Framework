using System;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Manages life states for entities, handling death, resurrection, and state transitions.
    /// </summary>
    /// <remarks>
    /// The life state manager coordinates death detection, state transitions, and lifecycle callbacks.
    /// It works with death conditions and respawn handlers to provide a flexible life/death system.
    /// </remarks>
    public interface ILifeStateManager
    {
        /// <summary>
        /// Starts tracking an entity's life state; its handler receives the death/resurrection callbacks.
        /// </summary>
        /// <param name="entityId">ID of the entity to register.</param>
        /// <param name="handler">Life state handler for the entity.</param>
        /// <param name="initialState">Initial life state; alive when null.</param>
        void RegisterEntity(int entityId, ILifeStateHandler handler, LifeStateComponent? initialState = null);

        /// <summary>
        /// Stops tracking an entity (e.g. when it is despawned).
        /// </summary>
        void UnregisterEntity(int entityId);

        /// <summary>
        /// Kills the specified entity.
        /// </summary>
        /// <param name="entityId">ID of the entity to kill.</param>
        /// <param name="killerId">ID of the entity that caused death, or -1 if environmental/self.</param>
        /// <param name="reason">Reason for death (e.g., "resource_depletion", "instant_death").</param>
        /// <returns>True if the entity was killed; false if it could not die (e.g., already dead, immortal).</returns>
        bool Kill(int entityId, int killerId, string reason);

        /// <summary>
        /// Resurrects the specified entity.
        /// </summary>
        /// <param name="entityId">ID of the entity to resurrect.</param>
        /// <param name="resurrectorId">ID of the entity performing resurrection, or -1 if self-resurrection.</param>
        /// <param name="resurrectType">Type of resurrection (e.g., "respawn", "player_revive").</param>
        /// <returns>True if the entity was resurrected; false if it could not be resurrected.</returns>
        bool Resurrect(int entityId, int resurrectorId, string resurrectType);

        /// <summary>
        /// Sets the entity as invulnerable for a specified duration.
        /// </summary>
        /// <param name="entityId">ID of the entity to make invulnerable.</param>
        /// <param name="duration">Duration of invulnerability in seconds. Use -1 for permanent invulnerability.</param>
        void SetInvulnerable(int entityId, float duration);

        /// <summary>
        /// Removes invulnerability from the entity.
        /// </summary>
        /// <param name="entityId">ID of the entity.</param>
        void RemoveInvulnerability(int entityId);

        /// <summary>
        /// Checks whether the entity should die based on registered death conditions.
        /// </summary>
        /// <param name="entityId">ID of the entity to check.</param>
        /// <param name="condition">The death condition to evaluate.</param>
        /// <returns>True if the entity meets the death condition; otherwise, false.</returns>
        bool ShouldDie(int entityId, IDeathCondition condition);

        /// <summary>
        /// Gets the current life state of the specified entity.
        /// </summary>
        /// <param name="entityId">ID of the entity.</param>
        /// <returns>The entity's current life state.</returns>
        LifeState GetLifeState(int entityId);

        /// <summary>
        /// Checks whether the entity is alive.
        /// </summary>
        /// <param name="entityId">ID of the entity.</param>
        /// <returns>True if the entity is alive; otherwise, false.</returns>
        bool IsAlive(int entityId);

        /// <summary>
        /// Checks whether the entity is dead.
        /// </summary>
        /// <param name="entityId">ID of the entity.</param>
        /// <returns>True if the entity is dead; otherwise, false.</returns>
        bool IsDead(int entityId);

        /// <summary>
        /// Checks whether the entity is invulnerable.
        /// </summary>
        /// <param name="entityId">ID of the entity.</param>
        /// <returns>True if the entity is invulnerable; otherwise, false.</returns>
        bool IsInvulnerable(int entityId);

        /// <summary>
        /// Event raised when an entity dies.
        /// </summary>
        event Action<DeathEvent> OnDeath;

        /// <summary>
        /// Event raised when an entity is resurrected.
        /// </summary>
        event Action<ResurrectionEvent> OnResurrection;
    }
}
