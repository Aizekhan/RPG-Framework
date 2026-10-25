namespace RPGFramework.Combat
{
    /// <summary>
    /// Represents a handler for entity respawn logic.
    /// </summary>
    /// <remarks>
    /// Respawn handlers provide extensible contracts for how entities return to life.
    /// Game layer implements specific respawn strategies (e.g., checkpoint-based, time-based, player revive).
    /// </remarks>
    public interface IRespawnHandler
    {
        /// <summary>
        /// Determines whether the target entity can respawn.
        /// </summary>
        /// <param name="target">The entity to check.</param>
        /// <returns>True if the entity can respawn; otherwise, false.</returns>
        bool CanRespawn(ILifeStateHandler target);

        /// <summary>
        /// Calculates the delay before respawn occurs.
        /// </summary>
        /// <param name="target">The entity to respawn.</param>
        /// <returns>Delay in seconds before respawn. Return 0 for instant respawn.</returns>
        float CalculateRespawnDelay(ILifeStateHandler target);

        /// <summary>
        /// Called when the entity respawns.
        /// </summary>
        /// <param name="target">The entity that is respawning.</param>
        /// <remarks>
        /// Implementers should restore entity resources (HP, MP, etc.) and apply any respawn effects.
        /// </remarks>
        void OnRespawn(ILifeStateHandler target);

        /// <summary>
        /// Gets the respawn type identifier.
        /// </summary>
        /// <remarks>
        /// Examples: "checkpoint", "time_based", "player_revive", "auto_respawn".
        /// </remarks>
        string RespawnType { get; }
    }
}
