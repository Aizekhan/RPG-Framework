using System;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Executes abilities with validation and cooldown management.
    /// Uses callback pattern for resource checks and effects.
    /// </summary>
    public interface IAbilityExecutor
    {
        /// <summary>
        /// Execute ability for entity.
        /// </summary>
        /// <param name="abilityId">Ability to execute</param>
        /// <param name="casterEntityId">Entity using ability</param>
        /// <param name="targetEntityId">Target entity (-1 for none)</param>
        /// <param name="currentTime">Current game time</param>
        /// <param name="resourceCheck">Callback to check if entity has required resources (entityId, resourceType, amount) -> hasEnough</param>
        /// <param name="resourceConsume">Callback to consume resources (entityId, resourceType, amount)</param>
        /// <param name="applyEffect">Callback to apply ability effects (casterEntityId, targetEntityId, abilityId)</param>
        /// <returns>Execution result</returns>
        AbilityExecutionResult ExecuteAbility(
            string abilityId,
            int casterEntityId,
            int targetEntityId,
            float currentTime,
            Func<int, string, float, bool> resourceCheck,
            Action<int, string, float> resourceConsume,
            Action<int, int, string> applyEffect);

        /// <summary>Check if ability can be executed</summary>
        bool CanExecuteAbility(string abilityId, int casterEntityId, float currentTime);

        /// <summary>Cancel active cast</summary>
        void CancelCast(int entityId);
    }
}
