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

        /// <summary>Cancel active cast or channel, stamped with the latest time the executor was given</summary>
        void CancelCast(int entityId);

        /// <summary>Cancel active cast or channel at the given time. Returns false when the entity was not casting.</summary>
        bool CancelCast(int entityId, float currentTime);

        /// <summary>
        /// Interrupt active cast or channel (stun, silence, knockback). Casts authored as not interruptible keep going.
        /// Returns true when something was interrupted.
        /// </summary>
        bool InterruptCast(int entityId, float currentTime, string reason = "interrupted");

        /// <summary>
        /// Finish the entity's cast once its cast time has passed: checks target and costs again, pays, applies the
        /// effect and starts the cooldown, using the callbacks given when the cast began.
        /// </summary>
        AbilityExecutionResult CompleteCast(int entityId, float currentTime);

        /// <summary>Complete casts whose cast time has passed and end channels whose duration is over. Returns how many.</summary>
        int Update(float currentTime);

        /// <summary>Whether the entity is in the cast time of an ability</summary>
        bool IsCasting(int entityId);

        /// <summary>Whether the entity is channeling an ability</summary>
        bool IsChanneling(int entityId);

        /// <summary>Whether a toggle ability is switched on for the entity</summary>
        bool IsToggledOn(int entityId, string abilityId);
    }
}
