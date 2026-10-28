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
    /// "Silence" by default) cannot use them. A character that leaves is forgotten. Turned on by
    /// <see cref="ActionRpgSettings.UseAbilities"/> or abilities in <see cref="ActionRpgContent.Abilities"/>.
    /// </summary>
    public sealed class ActorAbilities : IUpdateSystem, IDisposable
    {
        private readonly ActorWorld _world;
        private readonly IAbilityExecutor _executor;
        private readonly ICooldownTracker _cooldowns;
        private readonly IAbilityRegistry _registry;
        private readonly Dictionary<string, Action<int, int>> _effects = new Dictionary<string, Action<int, int>>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        public ActorAbilities(ActorWorld world, IAbilityExecutor executor, ICooldownTracker cooldowns,
            IAbilityRegistry registry, IEventBus bus)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _cooldowns = cooldowns ?? throw new ArgumentNullException(nameof(cooldowns));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));
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
            if (targetId < 0 && ResolveTarget != null)
            {
                var ability = _registry.GetAbility(abilityId);
                if (ability != null)
                    targetId = ResolveTarget(casterId, ability);
            }
            return _executor.ExecuteAbility(abilityId, casterId, targetId, Time,
                ResourceCheck ?? ((_, __, ___) => true), ResourceConsume ?? ((_, __, ___) => { }), Apply);
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
        }

        private void Apply(int casterId, int targetId, string abilityId)
        {
            if (_effects.TryGetValue(abilityId, out var effect))
                effect(casterId, targetId);
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
