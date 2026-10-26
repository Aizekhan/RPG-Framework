using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of IAbilityExecutor.
    /// Executes abilities with validation, cooldown management, and resource costs.
    /// With an event publisher, raises AbilityExecuted (also for failed attempts, Success = false), CooldownStarted
    /// for instant casts and AbilityCanceled for interrupted casts. Casts with a cast time are completed by the game.
    /// </summary>
    public class AbilityExecutor : IAbilityExecutor
    {
        private readonly IAbilityRegistry _registry;
        private readonly ICooldownTracker _cooldownTracker;
        private readonly ITargetValidator _targetValidator;
        private readonly IEventPublisher _events;

        // Track active ability instances per entity
        private readonly Dictionary<int, Dictionary<string, IAbilityInstance>> _abilityInstances =
            new Dictionary<int, Dictionary<string, IAbilityInstance>>();

        public AbilityExecutor(
            IAbilityRegistry registry,
            ICooldownTracker cooldownTracker,
            ITargetValidator targetValidator,
            IEventPublisher events = null)
        {
            _registry = registry;
            _cooldownTracker = cooldownTracker;
            _targetValidator = targetValidator;
            _events = events;
        }

        public AbilityExecutionResult ExecuteAbility(
            string abilityId,
            int casterEntityId,
            int targetEntityId,
            float currentTime,
            Func<int, string, float, bool> resourceCheck,
            Action<int, string, float> resourceConsume,
            Action<int, int, string> applyEffect)
        {
            // Get ability definition
            IAbility ability = _registry.GetAbility(abilityId);
            if (ability == null)
            {
                return Fail(
                    abilityId, casterEntityId, "Ability not found in registry", currentTime);
            }

            // Get or create ability instance
            IAbilityInstance instance = GetOrCreateInstance(casterEntityId, ability);

            // Check if can use
            if (!instance.CanUse(currentTime))
            {
                return Fail(
                    abilityId, casterEntityId, "Ability on cooldown or no charges remaining", currentTime);
            }

            // Check resource costs
            foreach (var cost in ability.ResourceCosts)
            {
                if (resourceCheck != null && !resourceCheck(casterEntityId, cost.ResourceType, cost.Amount))
                {
                    return Fail(
                        abilityId, casterEntityId, $"Insufficient {cost.ResourceType}", currentTime);
                }
            }

            // Handle instant cast
            if (ability.CastInfo.CastTime <= 0f)
            {
                // Consume resources
                if (resourceConsume != null)
                {
                    foreach (var cost in ability.ResourceCosts)
                    {
                        resourceConsume(casterEntityId, cost.ResourceType, cost.Amount);
                    }
                }

                // Apply effect
                applyEffect?.Invoke(casterEntityId, targetEntityId, abilityId);

                // Complete cast (starts cooldown)
                instance.CompleteCast(currentTime);

                if (_events != null)
                {
                    _events.Publish(new AbilityExecutedEvent
                    {
                        AbilityId = abilityId,
                        CasterEntityId = casterEntityId,
                        TargetEntityId = targetEntityId,
                        Timestamp = currentTime,
                        Success = true
                    });
                    if (ability.Cooldown > 0f)
                    {
                        _events.Publish(new CooldownStartedEvent
                        {
                            AbilityId = abilityId,
                            EntityId = casterEntityId,
                            Duration = ability.Cooldown,
                            Timestamp = currentTime
                        });
                    }
                }

                return AbilityExecutionResult.CreateSuccess(abilityId, casterEntityId, targetEntityId, currentTime);
            }
            else
            {
                // Start casting (for abilities with cast time)
                instance.StartCast(currentTime);

                // Game layer is responsible for completing cast after CastTime duration
                // They should call CompleteCast on the instance when ready

                return AbilityExecutionResult.CreateSuccess(abilityId, casterEntityId, targetEntityId, currentTime);
            }
        }

        public bool CanExecuteAbility(string abilityId, int casterEntityId, float currentTime)
        {
            IAbility ability = _registry.GetAbility(abilityId);
            if (ability == null)
                return false;

            IAbilityInstance instance = GetOrCreateInstance(casterEntityId, ability);
            return instance.CanUse(currentTime);
        }

        public void CancelCast(int entityId)
        {
            if (_abilityInstances.TryGetValue(entityId, out var instances))
            {
                foreach (var instance in instances.Values)
                {
                    if (instance.IsCasting)
                    {
                        instance.CancelCast();
                        _events?.Publish(new AbilityCanceledEvent
                        {
                            AbilityId = instance.Ability.AbilityId,
                            CasterEntityId = entityId,
                            Timestamp = 0f,
                            Reason = "canceled"
                        });
                    }
                }
            }
        }

        private AbilityExecutionResult Fail(string abilityId, int casterEntityId, string reason, float currentTime)
        {
            _events?.Publish(new AbilityExecutedEvent
            {
                AbilityId = abilityId,
                CasterEntityId = casterEntityId,
                TargetEntityId = -1,
                Timestamp = currentTime,
                Success = false
            });
            return AbilityExecutionResult.CreateFailure(abilityId, casterEntityId, reason, currentTime);
        }

        private IAbilityInstance GetOrCreateInstance(int entityId, IAbility ability)
        {
            if (!_abilityInstances.ContainsKey(entityId))
            {
                _abilityInstances[entityId] = new Dictionary<string, IAbilityInstance>();
            }

            var entityInstances = _abilityInstances[entityId];

            if (!entityInstances.ContainsKey(ability.AbilityId))
            {
                entityInstances[ability.AbilityId] = new AbilityInstance(ability, entityId);
            }

            return entityInstances[ability.AbilityId];
        }
    }
}
