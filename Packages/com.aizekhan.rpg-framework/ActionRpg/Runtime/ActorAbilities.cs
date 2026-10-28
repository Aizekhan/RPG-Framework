using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.Events;
using RPGFramework.Systems;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Abilities in an action RPG: a character uses one on a target, the framework's executor checks range, side,
    /// costs and cooldown, runs cast times and channels with the game clock (a system), and then the game's effect
    /// for that ability runs (<see cref="SetEffect"/>). Characters under a control that stops abilities ("Stun",
    /// "Silence" by default) cannot use them. The game can refuse a use (<see cref="CanUse"/>), pick the cooldown
    /// for each use (<see cref="CooldownFor"/>), and give back cost and cooldown when an effect found nothing
    /// (<see cref="SetEffect(string, Func{int, int, bool})"/>). A character that leaves is forgotten. Turned on by
    /// <see cref="ActionRpgSettings.UseAbilities"/> or abilities in <see cref="ActionRpgContent.Abilities"/>.
    /// </summary>
    public sealed class ActorAbilities : IUpdateSystem, IDisposable
    {
        private readonly ActorWorld _world;
        private readonly IAbilityExecutor _executor;
        private readonly ICooldownTracker _cooldowns;
        private readonly IAbilityRegistry _registry;
        private readonly Dictionary<string, Func<int, int, bool>> _effects = new Dictionary<string, Func<int, int, bool>>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly IEventBus _bus;
        private readonly Action<int, string, float> _consume;
        private readonly Action<int, int, string> _apply;
        // What the landing ability paid (the executor pays right before the effect)
        private readonly List<(int Entity, string Type, float Amount)> _paid = new List<(int, string, float)>();
        // Abilities that landed during an executor call; their cooldowns are fixed up once it returns
        private readonly List<Landing> _landed = new List<Landing>();

        private struct Landing
        {
            public int CasterId;
            public string AbilityId;
            public bool Refunded;
            public float Cooldown;
        }

        public ActorAbilities(ActorWorld world, IAbilityExecutor executor, ICooldownTracker cooldowns,
            IAbilityRegistry registry, IEventBus bus)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _cooldowns = cooldowns ?? throw new ArgumentNullException(nameof(cooldowns));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _consume = Consume;
            _apply = Apply;
            bus.Subscribe<ActorDespawnedEvent>(e => _executor.UnregisterEntity(e.EntityId)).AddTo(_subscriptions);
        }

        /// <summary>
        /// Seconds of game time since the world started (cooldowns and casts use it).
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// Controls that stop a character from using abilities.
        /// </summary>
        public HashSet<string> BlockingControls { get; } = new HashSet<string> { "Stun", "Silence" };

        /// <summary>
        /// Whether a character can pay an ability's cost (entity, resource type, amount); null: every cost is paid.
        /// </summary>
        public Func<int, string, float, bool> ResourceCheck { get; set; }

        /// <summary>
        /// Takes the cost (entity, resource type, amount); null takes nothing.
        /// </summary>
        public Action<int, string, float> ResourceConsume { get; set; }

        /// <summary>
        /// Gives a cost back (entity, resource type, amount) when an ability's effect found nothing; null gives
        /// nothing back (the cooldown is still cleared).
        /// </summary>
        public Action<int, string, float> ResourceRefund { get; set; }

        /// <summary>
        /// Whether the game lets a character use an ability now (caster, ability id): a perk not bought, a strike
        /// that needs a weapon in hand, a mount. Checked before range, target, costs and cooldown; runs where the
        /// rules run, so a friend's use in co-op is checked on the host. Null: every use is allowed.
        /// </summary>
        public Func<int, string, bool> CanUse { get; set; }

        /// <summary>
        /// The cooldown of this use in seconds (caster, ability): shorter with a perk, longer with a heavy weapon.
        /// A negative or not-a-number answer keeps the ability's own <see cref="IAbility.Cooldown"/>; 0 means none.
        /// Null: always the ability's own.
        /// </summary>
        public Func<int, IAbility, float> CooldownFor { get; set; }

        public IAbilityRegistry Registry => _registry;

        /// <summary>
        /// Picks the target when an ability is used without one (target -1): the game's own idea of "in front",
        /// "nearest in my lane", "the boss" (caster, ability; return -1 for none). Runs where the rules run, so a
        /// friend's ability in co-op is aimed with the host's world. Null: an ability used without a target has none.
        /// </summary>
        public Func<int, IAbility, int> ResolveTarget { get; set; }

        /// <summary>
        /// What an ability does once it goes off (caster, target; -1 without a target): deal damage with
        /// <see cref="ActorWorld.Attack"/> or <see cref="ActorWorld.DealDamage"/>, put on an effect, heal.
        /// Abilities without one only start their cooldown and announce themselves (<see cref="AbilityExecutedEvent"/>).
        /// </summary>
        public void SetEffect(string abilityId, Action<int, int> effect)
        {
            if (effect == null)
                SetEffect(abilityId, (Func<int, int, bool>)null);
            else
                SetEffect(abilityId, (caster, target) => { effect(caster, target); return true; });
        }

        /// <summary>
        /// An effect that can find nothing to work on (caster, target; -1 without a target): return false when the
        /// strike hit no one or the dash had nowhere to go, and the use costs nothing: <see cref="ResourceRefund"/>
        /// gives the cost back, the cooldown is cleared and <see cref="AbilityRefundedEvent"/> is published (a charge,
        /// if the ability has them, stays used). Return true when it worked.
        /// </summary>
        public void SetEffect(string abilityId, Func<int, int, bool> effect)
        {
            if (string.IsNullOrEmpty(abilityId))
                throw new ArgumentException("An ability needs an id", nameof(abilityId));
            if (effect == null)
                _effects.Remove(abilityId);
            else
                _effects[abilityId] = effect;
        }

        /// <summary>
        /// A character uses an ability (on a target, or -1). Cast times finish later, with the game clock.
        /// </summary>
        public AbilityExecutionResult Use(int casterId, string abilityId, int targetId = -1)
        {
            var caster = _world.Get(casterId);
            if (caster == null || !caster.IsAlive())
                return AbilityExecutionResult.CreateFailure(abilityId, casterId, "The caster is not in the world", Time);
            if (caster.Controls.Any(BlockingControls.Contains))
                return AbilityExecutionResult.CreateFailure(abilityId, casterId, "The caster cannot act", Time);
            if (CanUse != null && !CanUse(casterId, abilityId))
                return AbilityExecutionResult.CreateFailure(abilityId, casterId, "The game does not allow it now", Time);
            if (targetId < 0 && ResolveTarget != null)
            {
                var ability = _registry.GetAbility(abilityId);
                if (ability != null)
                    targetId = ResolveTarget(casterId, ability);
            }
            var result = _executor.ExecuteAbility(abilityId, casterId, targetId, Time,
                ResourceCheck ?? ((_, __, ___) => true), _consume, _apply);
            if (SettleLandings(casterId, abilityId) && result.Success && !result.CastStarted)
            {
                result.Success = false;
                result.FailureReason = "The ability found nothing; its cost came back";
            }
            return result;
        }

        public bool IsOnCooldown(int entityId, string abilityId) => _cooldowns.IsOnCooldown(entityId, abilityId, Time);

        /// <summary>
        /// Seconds until the ability can be used again (0 when it can).
        /// </summary>
        public float CooldownLeft(int entityId, string abilityId) =>
            Math.Max(0f, _cooldowns.GetRemainingCooldown(entityId, abilityId, Time));

        /// <summary>
        /// Cuts what is left of every running cooldown of a character by a share (0.2 = a fifth shorter).
        /// </summary>
        public void ReduceCooldowns(int entityId, float fraction)
        {
            if (float.IsNaN(fraction))
                throw new ArgumentException("The share must be a number", nameof(fraction));
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            foreach (var cooldown in _cooldowns.GetActiveCooldowns(entityId, Time).ToList())
            {
                float left = Math.Max(0f, _cooldowns.GetRemainingCooldown(entityId, cooldown.AbilityId, Time)) * (1f - fraction);
                if (left <= 0f)
                    _cooldowns.ClearCooldown(entityId, cooldown.AbilityId);
                else
                    _cooldowns.StartCooldown(entityId, cooldown.AbilityId, left, Time);
            }
        }

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            Time += deltaTime;
            _executor.Update(Time);
            SettleLandings(-1, null);
        }

        private void Consume(int entityId, string resourceType, float amount)
        {
            ResourceConsume?.Invoke(entityId, resourceType, amount);
            _paid.Add((entityId, resourceType, amount));
        }

        private void Apply(int casterId, int targetId, string abilityId)
        {
            bool found = !_effects.TryGetValue(abilityId, out var effect) || effect(casterId, targetId);
            if (!found && ResourceRefund != null)
            {
                foreach (var paid in _paid)
                {
                    if (paid.Entity == casterId)
                        ResourceRefund(paid.Entity, paid.Type, paid.Amount);
                }
            }
            _paid.Clear();

            float cooldown = -1f;
            if (found && CooldownFor != null)
            {
                var ability = _registry.GetAbility(abilityId);
                if (ability != null)
                    cooldown = CooldownFor(casterId, ability);
            }
            if (!found || cooldown >= 0f)
                _landed.Add(new Landing { CasterId = casterId, AbilityId = abilityId, Refunded = !found, Cooldown = cooldown });
        }

        // The executor starts the ability's own cooldown after the effect; this replaces it once the call returns.
        // True when the given caster's given ability was refunded.
        private bool SettleLandings(int casterId, string abilityId)
        {
            bool refunded = false;
            for (int i = 0; i < _landed.Count; i++)
            {
                var landing = _landed[i];
                if (landing.Refunded || landing.Cooldown <= 0f)
                    _cooldowns.ClearCooldown(landing.CasterId, landing.AbilityId);
                else
                    _cooldowns.StartCooldown(landing.CasterId, landing.AbilityId, landing.Cooldown, Time);
                if (landing.Refunded)
                {
                    _bus.Publish(new AbilityRefundedEvent(landing.CasterId, landing.AbilityId));
                    if (landing.CasterId == casterId && landing.AbilityId == abilityId)
                        refunded = true;
                }
            }
            _landed.Clear();
            return refunded;
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
