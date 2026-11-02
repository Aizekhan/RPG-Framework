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
    ///   busy until the duration is over or the channel is canceled. With <see cref="CastInfo.ChannelTickInterval"/>
    ///   the effect is called again on every tick during the channel (<see cref="Update"/> drives it,
    ///   AbilityChannelTick); without one, effects over the channel are driven by the game (see <see cref="IsChanneling"/>).
    ///   AbilityChannelEnded is raised when the channel ends, run out or stopped.
    /// - Charges with <see cref="AbilityMetadata.ChargeRechargeTime"/> come back one at a time while the executor is
    ///   given time.
    /// - A toggle ability switches on like an Active one (target, costs, cooldown) and the next use switches it off
    ///   for free, even during its cooldown.
    /// - A caster does one cast or channel at a time; other abilities are refused until it ends.
    /// - An ability with delayed hits (<see cref="CastInfo.ImpactDelay"/>, <see cref="CastInfo.ImpactCount"/>) goes
    ///   off as any other (paid, charge, cooldown, AbilityExecuted), and each hit calls the effect when its time
    ///   comes (<see cref="Update"/>, AbilityImpact), sped up or slowed down by <see cref="ImpactSpeedFor"/>. An
    ///   interrupt stops the hits still to come unless <see cref="CastInfo.ImpactsSurviveInterrupt"/>.
    /// With an event publisher, raises AbilityExecuted (also for failed attempts, Success = false), CooldownStarted
    /// (unless the cooldown tracker already raises it on the same publisher), AbilityToggled and AbilityCanceled.
    /// </summary>
    public class AbilityExecutor : IAbilityExecutor, IAbilityImpacts
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
        private readonly Dictionary<(int Entity, string Ability), Upkeep> _upkeeps = new Dictionary<(int, string), Upkeep>();
        private readonly List<(int Entity, string Ability)> _unpaid = new List<(int, string)>();
        private readonly List<int> _due = new List<int>();
        private readonly List<PendingImpact> _pending = new List<PendingImpact>();
        private readonly List<PendingImpact> _stopped = new List<PendingImpact>();
        private int _lastUse;
        private long _lastImpact;
        private AbilityImpact _current;

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
            public float NextTick;
            public int Ticks;
            public Func<int, string, float, bool> ResourceCheck;
            public Action<int, string, float> ResourceConsume;
            public Action<int, int, string> ApplyEffect;
            public float UpkeepPaidUntil;
            public int UseId;
        }

        // A hit still to come of a use that went off
        private sealed class PendingImpact
        {
            public AbilityImpact Impact;
            public IAbility Ability;
            public Action<int, int, string> ApplyEffect;
            public long Order;
        }

        // What a toggle that is on keeps paying for
        private sealed class Upkeep
        {
            public IAbility Ability;
            public Func<int, string, float, bool> ResourceCheck;
            public Action<int, string, float> ResourceConsume;
            public float PaidUntil;
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

        public Func<int, IAbility, float> ImpactSpeedFor { get; set; }

        public AbilityImpact Current => _current;

        public event Action<AbilityImpact> UseWentOff;

        public event Action<AbilityImpact, string> ImpactMissed;

        public bool HasPendingImpacts(int entityId)
        {
            foreach (var pending in _pending)
            {
                if (pending.Impact.CasterEntityId == entityId)
                    return true;
            }
            return false;
        }

        public int CancelImpacts(int entityId, float currentTime, string reason, bool interruptibleOnly = false)
        {
            _lastTime = currentTime;
            _stopped.Clear();
            for (int i = 0; i < _pending.Count; i++)
            {
                var pending = _pending[i];
                if (pending.Impact.CasterEntityId == entityId &&
                    (!interruptibleOnly || !pending.Ability.CastInfo.ImpactsSurviveInterrupt))
                    _stopped.Add(pending);
            }
            foreach (var pending in _stopped)
                _pending.Remove(pending);
            foreach (var pending in _stopped)
                Miss(pending, reason ?? AbilityImpactReasons.Canceled, currentTime);
            int count = _stopped.Count;
            _stopped.Clear();
            return count;
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
            foreach (var byAbility in _abilityInstances.Values)
            {
                foreach (var instance in byAbility.Values)
                    instance.RefreshCharges(currentTime);
            }

            PayUpkeeps(currentTime);

            // Channel ticks up to now, the last one at most at the channel's end
            _due.Clear();
            foreach (var pair in _casts)
            {
                if (pair.Value.Channeling && pair.Value.Ability.CastInfo.ChannelTickInterval > 0f)
                    _due.Add(pair.Key);
            }
            foreach (int entityId in _due)
                TickChannel(entityId, currentTime);

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
                    PublishChannelEnded(entityId, cast, currentTime, null);
                }
                else
                {
                    CompleteCast(entityId, currentTime);
                }
                finished++;
            }

            LandImpacts(currentTime);
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
            bool interrupted = false;
            if (_casts.TryGetValue(entityId, out var cast) && cast.Ability.CastInfo.IsInterruptible)
            {
                EndCast(entityId, cast, currentTime, reason);
                interrupted = true;
            }
            // A swing stopped before its contact; an arrow already loosed flies on
            if (CancelImpacts(entityId, currentTime, reason, interruptibleOnly: true) > 0)
                interrupted = true;
            return interrupted;
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
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Impact.CasterEntityId == entityId)
                    _pending.RemoveAt(i);
            }
            _unpaid.Clear();
            foreach (var key in _upkeeps.Keys)
            {
                if (key.Entity == entityId)
                    _unpaid.Add(key);
            }
            foreach (var key in _unpaid)
                _upkeeps.Remove(key);
            _unpaid.Clear();
            _cooldownTracker?.ClearAllCooldowns(entityId);
        }

        // Pays, applies (or queues its delayed hits), starts the cooldown and raises the events; then begins the
        // channel if the ability has one.
        private AbilityExecutionResult Land(ActiveCast cast, int casterEntityId, float currentTime)
        {
            IAbility ability = cast.Ability;
            string abilityId = ability.AbilityId;
            var info = ability.CastInfo;
            int useId = ++_lastUse;
            cast.UseId = useId;
            bool delayed = info.HasDelayedImpacts;
            int hits = delayed ? Math.Max(1, info.ImpactCount) : 1;
            float speed = delayed ? SpeedOf(casterEntityId, ability) : 1f;
            float firstAt = delayed ? currentTime + Math.Max(0f, info.ImpactDelay) / speed : currentTime;
            var use = new AbilityImpact(useId, abilityId, casterEntityId, cast.TargetEntityId, 0, hits, currentTime, delayed);

            _current = use;
            PayCosts(ability, casterEntityId, cast.ResourceConsume);
            if (!delayed)
            {
                _current = new AbilityImpact(useId, abilityId, casterEntityId, cast.TargetEntityId, 1, 1, currentTime, false);
                cast.ApplyEffect?.Invoke(casterEntityId, cast.TargetEntityId, abilityId);
            }
            _current = default;

            // Ends the cast and uses a charge
            cast.Instance.CompleteCast(currentTime);

            if (delayed)
            {
                float interval = Math.Max(0f, info.ImpactInterval) / speed;
                for (int i = 0; i < hits; i++)
                {
                    _pending.Add(new PendingImpact
                    {
                        Impact = new AbilityImpact(useId, abilityId, casterEntityId, cast.TargetEntityId, i + 1, hits,
                            firstAt + i * interval, true),
                        Ability = ability,
                        ApplyEffect = cast.ApplyEffect,
                        Order = ++_lastImpact
                    });
                }
                UseWentOff?.Invoke(use);
            }

            _events?.Publish(new AbilityExecutedEvent
            {
                AbilityId = abilityId,
                CasterEntityId = casterEntityId,
                TargetEntityId = cast.TargetEntityId,
                Timestamp = currentTime,
                Success = true,
                UseId = useId,
                ImpactAt = firstAt,
                Impacts = hits
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
                if (HasUpkeep(ability))
                {
                    _upkeeps[(casterEntityId, abilityId)] = new Upkeep
                    {
                        Ability = ability,
                        ResourceCheck = cast.ResourceCheck,
                        ResourceConsume = cast.ResourceConsume,
                        PaidUntil = currentTime
                    };
                }
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
                cast.NextTick = currentTime + ability.CastInfo.ChannelTickInterval;
                cast.Ticks = 0;
                cast.UpkeepPaidUntil = currentTime;
                _casts[casterEntityId] = cast;
            }

            // A first hit without a delay lands now
            if (delayed)
                LandImpacts(currentTime);

            var result = AbilityExecutionResult.CreateSuccess(abilityId, casterEntityId, cast.TargetEntityId, currentTime);
            result.UseId = useId;
            result.ImpactAt = firstAt;
            result.ImpactsPending = delayed ? PendingOf(useId) : 0;
            return result;
        }

        // The hits due by now, earliest first (the effect may stop or add others)
        private void LandImpacts(float currentTime)
        {
            while (true)
            {
                int next = -1;
                for (int i = 0; i < _pending.Count; i++)
                {
                    var pending = _pending[i];
                    if (pending.Impact.Time > currentTime + 1e-5f)
                        continue;
                    if (next < 0 || pending.Impact.Time < _pending[next].Impact.Time ||
                        pending.Impact.Time == _pending[next].Impact.Time && pending.Order < _pending[next].Order)
                        next = i;
                }
                if (next < 0)
                    return;

                var due = _pending[next];
                _pending.RemoveAt(next);
                var impact = due.Impact;
                if (due.Ability.CastInfo.RecheckTargetOnImpact &&
                    !TargetIsValid(due.Ability, impact.CasterEntityId, impact.TargetEntityId))
                {
                    Miss(due, AbilityImpactReasons.InvalidTarget, impact.Time);
                    continue;
                }
                _current = impact;
                due.ApplyEffect?.Invoke(impact.CasterEntityId, impact.TargetEntityId, impact.AbilityId);
                _current = default;
                PublishImpact(impact, true, null, impact.Time);
            }
        }

        private void Miss(PendingImpact pending, string reason, float time)
        {
            ImpactMissed?.Invoke(pending.Impact, reason);
            PublishImpact(pending.Impact, false, reason, time);
        }

        private void PublishImpact(AbilityImpact impact, bool landed, string reason, float time)
        {
            _events?.Publish(new AbilityImpactEvent
            {
                AbilityId = impact.AbilityId,
                CasterEntityId = impact.CasterEntityId,
                TargetEntityId = impact.TargetEntityId,
                UseId = impact.UseId,
                Hit = impact.Hit,
                Hits = impact.Hits,
                Landed = landed,
                Reason = reason,
                Timestamp = time
            });
        }

        private int PendingOf(int useId)
        {
            int count = 0;
            foreach (var pending in _pending)
            {
                if (pending.Impact.UseId == useId)
                    count++;
            }
            return count;
        }

        private float SpeedOf(int casterEntityId, IAbility ability)
        {
            var speedFor = ImpactSpeedFor;
            if (speedFor == null)
                return 1f;
            float speed = speedFor(casterEntityId, ability);
            return float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f ? 1f : speed;
        }

        private AbilityExecutionResult SwitchOff(IAbility ability, int casterEntityId, int targetEntityId, float currentTime)
        {
            _toggledOn[casterEntityId].Remove(ability.AbilityId);
            if (_upkeeps.TryGetValue((casterEntityId, ability.AbilityId), out var upkeep))
            {
                // The seconds up to the switch are paid
                Drain(casterEntityId, upkeep.Ability, upkeep.ResourceCheck, upkeep.ResourceConsume, upkeep.PaidUntil, currentTime, out _);
                _upkeeps.Remove((casterEntityId, ability.AbilityId));
            }
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
            // Ticks due before the stop still land
            if (cast.Channeling)
                TickChannel(entityId, currentTime);
            _casts.Remove(entityId);
            cast.Instance.CancelCast();
            _events?.Publish(new AbilityCanceledEvent
            {
                AbilityId = cast.Ability.AbilityId,
                CasterEntityId = entityId,
                Timestamp = currentTime,
                Reason = reason
            });
            if (cast.Channeling)
                PublishChannelEnded(entityId, cast, currentTime, reason);
        }

        private void TickChannel(int entityId, float currentTime)
        {
            if (!_casts.TryGetValue(entityId, out var cast) || !cast.Channeling)
                return;
            float interval = cast.Ability.CastInfo.ChannelTickInterval;
            if (interval <= 0f)
                return;
            float until = Math.Min(currentTime, cast.EndTime);
            while (cast.NextTick <= until + 1e-5f)
            {
                float at = cast.NextTick;
                cast.Ticks++;
                cast.NextTick += interval;
                _current = new AbilityImpact(cast.UseId, cast.Ability.AbilityId, entityId, cast.TargetEntityId, 0, 0, at, false);
                cast.ApplyEffect?.Invoke(entityId, cast.TargetEntityId, cast.Ability.AbilityId);
                _current = default;
                _events?.Publish(new AbilityChannelTickEvent
                {
                    AbilityId = cast.Ability.AbilityId,
                    CasterEntityId = entityId,
                    TargetEntityId = cast.TargetEntityId,
                    Tick = cast.Ticks,
                    Timestamp = at
                });
                // The effect may have ended the channel (the caster died)
                if (!_casts.TryGetValue(entityId, out var still) || still != cast)
                    return;
            }
        }

        private void PublishChannelEnded(int entityId, ActiveCast cast, float currentTime, string reason)
        {
            _events?.Publish(new AbilityChannelEndedEvent
            {
                AbilityId = cast.Ability.AbilityId,
                CasterEntityId = entityId,
                TargetEntityId = cast.TargetEntityId,
                Completed = reason == null,
                Reason = reason,
                Ticks = cast.Ticks,
                Timestamp = currentTime
            });
        }

        // Why the ability cannot be used right now, or null when it can (target and costs aside).
        private string Blocked(IAbility ability, IAbilityInstance instance, int casterEntityId, float currentTime)
        {
            instance.RefreshCharges(currentTime);
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
                // An upkeep needs one second's worth to start
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
                if (!cost.PerSecond && TryPrice(cost, casterEntityId, out float amount))
                    resourceConsume(casterEntityId, cost.ResourceType, amount);
            }
        }

        private static bool HasUpkeep(IAbility ability)
        {
            foreach (var cost in ability.ResourceCosts)
            {
                if (cost.PerSecond)
                    return true;
            }
            return false;
        }

        // Takes the upkeep of the seconds from paidUntil to now; false (nothing taken) when one of them cannot be paid
        private bool Drain(int entityId, IAbility ability, Func<int, string, float, bool> resourceCheck,
            Action<int, string, float> resourceConsume, float paidUntil, float currentTime, out string unpaid)
        {
            unpaid = null;
            float seconds = currentTime - paidUntil;
            if (seconds <= 0f)
                return true;
            foreach (var cost in ability.ResourceCosts)
            {
                if (!cost.PerSecond || !TryPrice(cost, entityId, out float perSecond))
                    continue;
                if (resourceCheck != null && !resourceCheck(entityId, cost.ResourceType, perSecond * seconds))
                {
                    unpaid = $"Insufficient {cost.ResourceType}";
                    return false;
                }
            }
            if (resourceConsume == null)
                return true;
            foreach (var cost in ability.ResourceCosts)
            {
                if (cost.PerSecond && TryPrice(cost, entityId, out float perSecond))
                    resourceConsume(entityId, cost.ResourceType, perSecond * seconds);
            }
            return true;
        }

        // Toggles that are on and channels that last pay for the time since they last paid; unpaid ones stop
        private void PayUpkeeps(float currentTime)
        {
            _unpaid.Clear();
            foreach (var pair in _upkeeps)
            {
                var upkeep = pair.Value;
                if (Drain(pair.Key.Entity, upkeep.Ability, upkeep.ResourceCheck, upkeep.ResourceConsume, upkeep.PaidUntil, currentTime, out _))
                    upkeep.PaidUntil = Math.Max(upkeep.PaidUntil, currentTime);
                else
                    _unpaid.Add(pair.Key);
            }
            foreach (var key in _unpaid)
            {
                var upkeep = _upkeeps[key];
                Drain(key.Entity, upkeep.Ability, upkeep.ResourceCheck, null, upkeep.PaidUntil, currentTime, out string reason);
                _upkeeps.Remove(key);
                if (_toggledOn.TryGetValue(key.Entity, out var on))
                    on.Remove(key.Ability);
                _events?.Publish(new AbilityToggledEvent
                {
                    AbilityId = key.Ability, CasterEntityId = key.Entity, IsOn = false, Timestamp = currentTime, Reason = reason
                });
            }

            _due.Clear();
            foreach (var pair in _casts)
            {
                if (pair.Value.Channeling && HasUpkeep(pair.Value.Ability))
                    _due.Add(pair.Key);
            }
            foreach (int entityId in _due)
            {
                var cast = _casts[entityId];
                float until = Math.Min(currentTime, cast.EndTime);
                if (Drain(entityId, cast.Ability, cast.ResourceCheck, cast.ResourceConsume, cast.UpkeepPaidUntil, until, out string reason))
                    cast.UpkeepPaidUntil = Math.Max(cast.UpkeepPaidUntil, until);
                else
                    EndCast(entityId, cast, currentTime, reason);
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
