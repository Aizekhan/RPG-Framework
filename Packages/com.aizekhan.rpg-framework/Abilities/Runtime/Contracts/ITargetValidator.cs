using System;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Validates targets for ability execution.
    /// Uses callback pattern to avoid Unity/game-specific dependencies.
    /// </summary>
    public interface ITargetValidator
    {
        /// <summary>
        /// Validate target for ability.
        /// Returns true if target is valid, false otherwise.
        /// </summary>
        /// <param name="ability">Ability being used</param>
        /// <param name="casterEntityId">Entity using ability</param>
        /// <param name="targetEntityId">Target entity (-1 for no target)</param>
        /// <param name="distanceCheck">Callback to check distance between entities (returns distance in units)</param>
        /// <param name="relationshipCheck">Callback to check if target is enemy (true) or ally (false)</param>
        bool ValidateTarget(
            IAbility ability,
            int casterEntityId,
            int targetEntityId,
            Func<int, int, float> distanceCheck,
            Func<int, int, bool> relationshipCheck);
    }
}
