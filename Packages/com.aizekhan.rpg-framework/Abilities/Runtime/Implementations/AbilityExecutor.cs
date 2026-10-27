using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Default implementation of IAbilityExecutor.
    /// Uses an ability the way it was authored:
    /// - Passive abilities are always on and refuse manual use. Triggered abilities are used like Active ones by
    ///   whatever trigger the game runs.
    /// - The target is checked with the target validator. Range is only measured when a distance check was given to
    ///   the constructor; without one the executor cannot know distances and leaves range to the game.
    /// - Costs are checked before anything happens and paid when the ability lands. A percentage cost is
    ///   Amount x the resource maximum (0.1 = 10%), read through the resource maximum callback; with no such callback
    ///   the ability is refused rather than charged a wrong amount. Passing no check and no consume callback skips costs.
    /// - Cooldowns live in the cooldown tracker (the single source of truth): checked before use, started when the
    ///   ability lands.
    /// - An ability with a cast time starts a cast: nothing is paid or applied yet. <see cref="CompleteCast"/> (or
    ///   <see cref="Update"/>) finishes it once the cast time has passed, checks target and costs again, then pays,
    ///   applies the effect, starts the cooldown and raises AbilityExecuted. A canceled or interrupted cast costs nothing.
    /// - An ability with a channel duration lands when the channel starts (after any cast time) and keeps the caster
    ///   busy until the duration is over or the channel is canceled. Effects over the channel are driven by the game
    ///   (see <see cref="IsChanneling"/>).
    /// - A toggle ability switches on like an Active one (target, costs, cooldown) and the next use switches it off
    ///   for free, even during its cooldown.
    /// - A caster does one cast or channel at a time; other abilities are refused until it ends.
    /// With an event publisher, raises AbilityExecuted (also for failed attempts, Success = false), CooldownStarted
    /// (unless the cooldown tracker already raises it on the same publisher), AbilityToggled and AbilityCanceled.
    /// </summary>
    public class AbilityExecutor : IAbilityExecutor
    {
        private readonly IAbilityRegistry _registry;
        private readonly ICooldownTracker _cooldownTracker;
        private readonly ITargetValidator _targetValidator;
        private readonly IEventPublisher _events;
        private readonly Func<int, int, float> _distanceCheck;
        private readonly Func<int, int, bool> _relationshipCheck;
        private readonly Func<int, string, float> _resourceMaximum;

        // Track active ability instances per entity
        private readonly Dictionary<int, Dictionary<string, IAbilityInstance>> _abilityInstances =
            new Dictionary<int, Dictionary<string, IAbilityInstance>>();

        // The cast or channel each entity is busy with
        private readonly Dictionary<int, ActiveCast> _casts = new Dictionary<int, ActiveCast>();
        private readonly Dictionary<int, HashSet<string>> _toggledOn = new Dictionary<int, HashSet<string>>();
        private readonly List<int> _due = new List<int>();

        // Latest game time the executor was given; stamps CancelCast calls made without a time
        private float _lastTime;

        private static readonly Func<int, int, float> NoDistance = (caster, target) => 0f;

        private sealed class ActiveCast
        {
            public IAbility Ability;
            public IAbilityInstance Instance;
            public int TargetEntityId;
            public bool Channeling;
            public float EndTime;
            public Func<int, string, float, bool> ResourceCheck;
            public Action<int, string, float> ResourceConsume;
            public Action<int, int, string> ApplyEffect;
        }

        /// <param name="distanceCheck">Distance between two entities, for ability range. Optional.</param>
        /// <param name="relationshipCheck">True when the second entity is an enemy of the first. Optional.</param>
        /// <param name="resourceMaximum">Maximum of a resource for an entity (entityId, resourceType), for percentage costs. Optional.</param>
        public AbilityExecutor(
            IAbilityRegistry registry,
            ICooldownTracker cooldownTracker,
            ITargetValidator targetValidator,
            IEventPublisher events = null,
            Func<int, int, float> distanceCheck = null,
            Func<int, int, bool> relationshipCheck = null,
            Func<int, string, float> resourceMaximum = null)
        {
            _registry = registry;
            _cooldownTracker = cooldownTracker;
            _targetValidator = targetValidator;
            _events = events;
            _distanceCheck = distanceCheck;
            _relationshipCheck = relationshipCheck;
            _resourceMaximum = resourceMaximum;
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
            _lastTime = currentTime;

            // Get ability definition
            IAbility ability = _registry.GetAbility(abilityId);
            if (ability == null)
            {
                return Fail(
                    abilityId, casterEntityId, "Ability not found in registry", currentTime);
            }

            if (ability.Type == AbilityType.Passive)
                return Fail(abilityId, casterEntityId, "Passive abilities are always active and cannot be used", currentTime);

            // Get or create ability instance
            IAbilityInstance instance = GetOrCreateInstance(casterEntityId, ability);

            if (ability.Type == AbilityType.Toggle && IsToggledOn(casterEntityId, abilityId))
                return SwitchOff(ability, casterEntityId, targetEntityId, currentTime);

            string blocked = Blocked(ability, instance, casterEntityId, currentTime);
            if (blocked != null)
                return Fail(abilityId, casterEntityId, blocked, currentTime);

            if (!TargetIsValid(ability, casterEntityId, targetEntityId))
                return Fail(abilityId, casterEntityId, "Invalid target", currentTime);

            string cannotPay = CheckCosts(ability, casterEntityId, resourceCheck, resourceConsume);
            if (cannotPay != null)
                return Fail(abilityId, casterEntityId, cannotPay, currentTime);

            var cast = new ActiveCast
            {
                Ability = ability,
                Instance = instance,
                TargetEntityId = targetEntityId,
                EndTime = currentTime + ability.CastInfo.CastTime,
                ResourceCheck = resourceCheck,
                ResourceConsume = resourceConsume,
                ApplyEffect = applyEffect
            };

            if (ability.CastInfo.CastTime > 0f)
            {
                // Costs, effect and cooldown wait for CompleteCast
                instance.StartCast(currentTime);
                _casts[casterEntityId] = cast;
                var started = AbilityExecutionResult.CreateSuccess(abilityId, casterEntityId, targetEntityId, currentTime);
                started.CastStarted = true;
                return started;
            }

            return Land(cast, casterEntityId, currentTime);
        }

        public bool CanExecuteAbility(string abilityId, int casterEntityId, float currentTime)
        {
            IAbility ability = _registry.GetAbility(abilityId);
            if (ability == null || ability.Type == AbilityType.Passive)
                return false;

            if (ability.Type == AbilityType.Toggle && IsToggledOn(casterEntityId, abilityId))
                return true;

            IAbilityInstance instance = GetOrCreateInstance(casterEntityId, ability);
            return Blocked(ability, instance, casterEntityId, currentTime) == null;
        }

        public AbilityExecutionResult CompleteCast(int entityId, float currentTime)
        {
            _lastTime = currentTime;
            if (!_casts.TryGetValue(entityId, out var cast) || cast.Channeling)
                return AbilityExecutionResult.CreateFailure(null, entityId, "Not casting", currentTime);

            string abilityId = cast.Ability.AbilityId;
            if (currentTime < cast.EndTime)
                return AbilityExecutionResult.CreateFailure(abilityId, entityId, "Cast not finished", currentTime);

            _casts.Remove(entityId);

            // The world may have changed during the cast: the target moved away, the caster spent the mana
            if (!TargetIsValid(cast.Ability, entityId, cast.TargetEntityId))
            {
                cast.Instance.CancelCast();
                return Fail(abilityId, entityId, "Invalid target", currentTime);
            }

            string cannotPay = CheckCosts(cast.Ability, entityId, cast.ResourceCheck, cast.ResourceConsume);
            if (cannotPay != null)
            {
                cast.Instance.CancelCast();
                return Fail(abilityId, entityId, cannotPay, currentTime);
            }

            return Land(cast, entityId, currentTime);
        }

        public int Update(float currentTime)
        {
            _lastTime = currentTime;
            _due.Clear();
            foreach (var pair in _casts)
            {
                if (currentTime >= pair.Value.EndTime)
                    _due.Add(pair.Key);
            }

            int finished = 0;
            foreach (int entityId in _due)
            {
                if (!_casts.TryGetValue(entityId, out var cast) || currentTime < cast.EndTime)
                    continue;

                if (cast.Channeling)
                {
                    _casts.Remove(entityId);
                    cast.Instance.CancelCast();
                }
                else
                {
                    CompleteCast(entityId, currentTime);
                }
                finished++;
            }
            return finished;
        }

        public void CancelCast(int entityId)
        {
            CancelCast(entityId, _lastTime);
        }

        public bool CancelCast(int entityId, float currentTime)
        {
            _lastTime = currentTime;
            if (!_casts.TryGetValue(entityId, out var cast))
                return false;

            EndCast(entityId, cast, currentTime, "canceled");
            return true;
        }

        public bool InterruptCast(int entityId, float currentTime, string reason = "interrupted")
        {
            _lastTime = currentTime;
            if (!_casts.TryGetValue(entityId, out var cast) || !cast.Ability.CastInfo.IsInterruptible)
                return false;

            EndCast(entityId, cast, currentTime, reason);
            return true;
        }

        public bool IsCasting(int entityId)
        {
            return _casts.TryGetValue(entityId, out var cast) && !cast.Channeling;
        }

        public bool IsChanneling(int entityId)
        {
            return _casts.TryGetValue(entityId, out var cast) && cast.Channeling;
        }

        public bool IsToggledOn(int entityId, string abilityId)
        {
            return _toggledOn.TryGetValue(entityId, out var on) && on.Contains(abilityId);
        }

        public void UnregisterEntity(int entityId)
        {
            _abilityInstances.Remove(entityId);
            _casts.Remove(entityId);
            _toggledOn.Remove(entityId);
            _cooldownTracker?.ClearAllCooldowns(entityId);
        }

        // Pays, applies, starts the cooldown and raises the events; then begins the channel if the ability has one.
        private AbilityExecutionResult Land(ActiveCast cast, int casterEntityId, float currentTime)
        {
            IAbility ability = cast.Ability;
            string abilityId = ability.AbilityId;

            PayCosts(ability, casterEntityId, cast.ResourceConsume);
            cast.ApplyEffect?.Invoke(casterEntityId, cast.TargetEntityId, abilityId);

            // Ends the cast and uses a charge
            cast.Instance.CompleteCast(currentTime);

            _events?.Publish(new AbilityExecutedEvent
            {
                AbilityId = abilityId,
                CasterEntityId = casterEntityId,
                TargetEntityId = cast.TargetEntityId,
                Timestamp = currentTime,
                Success = true
            });

            if (ability.Cooldown > 0f)
            {
                _cooldownTracker?.StartCooldown(casterEntityId, abilityId, ability.Cooldown, currentTime);
                if (_events != null && !TrackerRaisesCooldowns())
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

            if (ability.Type == AbilityType.Toggle)
            {
                if (!_toggledOn.TryGetValue(casterEntityId, out var on))
                    _toggledOn[casterEntityId] = on = new HashSet<string>();
                on.Add(abilityId);
                _events?.Publish(new AbilityToggledEvent
                {
                    AbilityId = abilityId, CasterEntityId = casterEntityId, IsOn = true, Timestamp = currentTime
                });
            }

            if (ability.CastInfo.ChannelDuration > 0f)
            {
                cast.Instance.StartCast(currentTime);
                cast.Channeling = true;
                cast.EndTime = currentTime + ability.CastInfo.ChannelDuration;
                _casts[casterEntityId] = cast;
            }

            return AbilityExecutionResult.CreateSuccess(abilityId, casterEntityId, cast.TargetEntityId, currentTime);
        }

        private AbilityExecutionResult SwitchOff(IAbility ability, int casterEntityId, int targetEntityId, float currentTime)
        {
            _toggledOn[casterEntityId].Remove(ability.AbilityId);
            _events?.Publish(new AbilityToggledEvent
            {
                AbilityId = ability.AbilityId, CasterEntityId = casterEntityId, IsOn = false, Timestamp = currentTime
            });
            var result = AbilityExecutionResult.CreateSuccess(ability.AbilityId, casterEntityId, targetEntityId, currentTime);
            result.ToggledOff = true;
            return result;
        }

        private void EndCast(int entityId, ActiveCast cast, float currentTime, string reason)
        {
            _casts.Remove(entityId);
            cast.Instance.CancelCast();
            _events?.Publish(new AbilityCanceledEvent
            {
                AbilityId = cast.Ability.AbilityId,
                CasterEntityId = entityId,
                Timestamp = currentTime,
                Reason = reason
            });
        }

        // Why the ability cannot be used right now, or null when it can (target and costs aside).
        private string Blocked(IAbility ability, IAbilityInstance instance, int casterEntityId, float currentTime)
        {
            if (_casts.TryGetValue(casterEntityId, out var active))
                return $"Caster is busy {(active.Channeling ? "channeling" : "casting")} {active.Ability.AbilityId}";

            if (instance.RemainingCharges == 0)
                return "No charges remaining";

            bool onCooldown = _cooldownTracker != null
                ? _cooldownTracker.IsOnCooldown(casterEntityId, ability.AbilityId, currentTime)
                : instance.CooldownState.IsOnCooldown(currentTime);
            return onCooldown ? "Ability on cooldown" : null;
        }

        private bool TargetIsValid(IAbility ability, int casterEntityId, int targetEntityId)
        {
            return _targetValidator == null || _targetValidator.ValidateTarget(
                ability, casterEntityId, targetEntityId, _distanceCheck ?? NoDistance, _relationshipCheck);
        }

        // Why the caster cannot pay, or null when it can (or when the caller opted out of costs).
        private string CheckCosts(
            IAbility ability, int casterEntityId,
            Func<int, string, float, bool> resourceCheck, Action<int, string, float> resourceConsume)
        {
            if (resourceCheck == null && resourceConsume == null)
                return null;

            foreach (var cost in ability.ResourceCosts)
            {
                if (!TryPrice(cost, casterEntityId, out float amount))
                    return $"Cannot price the {cost.ResourceType} cost: it is a percentage and no resource maximum was given";

                if (resourceCheck != null && !resourceCheck(casterEntityId, cost.ResourceType, amount))
                    return $"Insufficient {cost.ResourceType}";
            }
            return null;
        }

        private void PayCosts(IAbility ability, int casterEntityId, Action<int, string, float> resourceConsume)
        {
            if (resourceConsume == null)
                return;

            foreach (var cost in ability.ResourceCosts)
            {
                if (TryPrice(cost, casterEntityId, out float amount))
                    resourceConsume(casterEntityId, cost.ResourceType, amount);
            }
        }

        private bool TryPrice(ResourceCost cost, int casterEntityId, out float amount)
        {
            if (!cost.IsPercentage)
            {
                amount = cost.Amount;
                return true;
            }

            if (_resourceMaximum == null)
            {
                amount = 0f;
                return false;
            }

            amount = cost.Amount * _resourceMaximum(casterEntityId, cost.ResourceType);
            return true;
        }

        private bool TrackerRaisesCooldowns()
        {
            return _cooldownTracker is CooldownTracker tracker && tracker.Events != null && ReferenceEquals(tracker.Events, _events);
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
