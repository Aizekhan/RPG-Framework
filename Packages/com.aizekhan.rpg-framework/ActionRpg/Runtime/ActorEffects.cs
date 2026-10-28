using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.StatusEffects;
using RPGFramework.Stats;
using RPGFramework.Systems;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A status effect on a character as the screen sees it (here or mirrored from a co-op host).
    /// </summary>
    public readonly struct ActorEffect
    {
        public ActorEffect(string effectId, int stacks, int sourceId, float remaining)
        {
            EffectId = effectId;
            Stacks = stacks;
            SourceId = sourceId;
            Remaining = remaining;
        }

        public string EffectId { get; }
        public int Stacks { get; }

        /// <summary>
        /// The character that put it on; -1 for nobody.
        /// </summary>
        public int SourceId { get; }

        /// <summary>
        /// Seconds left when it was last reported; negative for an effect without an end.
        /// </summary>
        public float Remaining { get; }
    }

    /// <summary>
    /// Where status effects land on the characters of an <see cref="ActorWorld"/>: stat modifiers go on the actor's
    /// stats (by <see cref="ActorStat"/> name), the "Health" resource heals or hurts through the world's rules as
    /// effect damage, movement becomes <see cref="Actor.MoveMultiplier"/> and controls ("Stun") become
    /// <see cref="Actor.Controls"/>. The world is looked up when needed, so the target can be made before it.
    /// </summary>
    public sealed class ActorEffectTarget : IStatusEffectTarget
    {
        /// <summary>
        /// The resource name of a character's health.
        /// </summary>
        public const string Health = "Health";

        private readonly Func<ActorWorld> _world;

        public ActorEffectTarget(Func<ActorWorld> world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        private Actor Get(int entityId) => _world()?.Get(entityId);

        public void ApplyStatModifier(int entityId, string modifierId, string statId, ModifierOperation operation, float value)
        {
            var actor = Get(entityId);
            if (actor == null || !Enum.TryParse(statId, false, out ActorStat stat) || !Enum.IsDefined(typeof(ActorStat), stat))
                return;
            StatModifierType type;
            switch (operation)
            {
                case ModifierOperation.Multiply:
                    // The container multiplies by 1 + value
                    type = StatModifierType.Multiplicative;
                    value -= 1f;
                    break;
                case ModifierOperation.Override:
                    type = StatModifierType.Override;
                    break;
                default:
                    type = StatModifierType.Additive;
                    break;
            }
            actor.AddModifier(new StatModifier<ActorStat, float>(modifierId, stat, value, type));
        }

        public void RemoveStatModifier(int entityId, string modifierId) => Get(entityId)?.RemoveModifier(modifierId);

        public bool TryGetResource(int entityId, string resourceId, out float value)
        {
            var actor = Get(entityId);
            value = actor?.Health ?? 0f;
            return actor != null && resourceId == Health;
        }

        public void ChangeResource(int entityId, string resourceId, float amount)
        {
            var world = _world();
            if (world == null || resourceId != Health)
                return;
            if (world.Effects != null)
                world.Effects.ChangeHealth(entityId, amount);
            else if (amount > 0f)
                world.Heal(entityId, amount);
            else if (amount < 0f)
                world.DealDamage(-1, entityId, -amount, AttackContext.Effect(0));
        }

        public void SetMovementMultiplier(int entityId, float multiplier)
        {
            var actor = Get(entityId);
            if (actor != null)
                actor.MoveMultiplier = Math.Max(0f, multiplier);
        }

        public void SetControl(int entityId, string controlId, bool active) => Get(entityId)?.SetControl(controlId, active);
    }

    /// <summary>
    /// Status effects in an action RPG: put them on characters, see what is on whom. Time runs with the game
    /// (a system): effects run out, damage and healing over time tick. Damage over time goes through the world's
    /// rules as effect damage (<see cref="AttackContext.FromEffect"/>, source <see cref="SourceIdOf"/> the effect id)
    /// and counts for whoever put the effect on. Effects end when their character falls or leaves.
    /// Turned on by <see cref="ActionRpgSettings.UseStatusEffects"/> or effects in <see cref="ActionRpgContent.StatusEffects"/>.
    /// </summary>
    public sealed class ActorEffects : IUpdateSystem, IDisposable
    {
        private readonly ActorWorld _world;
        private readonly IStatusEffectManager _manager;
        private readonly StatusEffectManager _full;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly List<int> _ids = new List<int>();
        private readonly List<(int Entity, int Source, int Effect, float Damage, float Healing)> _ticks =
            new List<(int, int, int, float, float)>();
        private bool _ticking;

        public ActorEffects(ActorWorld world, IStatusEffectManager manager, IEventBus bus)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _full = manager as StatusEffectManager;
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));
            bus.Subscribe<ActorDespawnedEvent>(e => _manager.RemoveAllEffects(e.EntityId)).AddTo(_subscriptions);
            bus.Subscribe<DeathEvent>(e =>
            {
                if (_world.Get(e.EntityId) != null)
                    _manager.RemoveAllEffects(e.EntityId);
            }).AddTo(_subscriptions);
        }

        /// <summary>
        /// Seconds of game time since the world started (effect start times and ticks use it).
        /// </summary>
        public float Time { get; private set; }

        public IStatusEffectManager Manager => _manager;

        /// <summary>
        /// The <see cref="AttackContext.SourceId"/> of damage from this effect: the same number on every machine
        /// and in every run, so a damage calculator can compare it.
        /// </summary>
        public static int SourceIdOf(string effectId)
        {
            // FNV-1a, stable across runs unlike string.GetHashCode
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in effectId ?? "")
                    hash = (hash ^ c) * 16777619;
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// Puts an effect on a living character.
        /// </summary>
        /// <param name="sourceId">The character who put it on (kills by its damage count for them); -1 for nobody.</param>
        public bool Apply(int targetId, string effectId, int sourceId = -1)
        {
            var target = _world.Get(targetId);
            if (target == null || !target.IsAlive() || string.IsNullOrEmpty(effectId))
                return false;
            return _manager.ApplyEffect(targetId, effectId, sourceId.ToString(CultureInfo.InvariantCulture), Time);
        }

        /// <summary>
        /// Puts an effect on a living character with a strength and a duration of this application's own: a trap's
        /// poison twice as strong (<paramref name="power"/> 2 doubles its damage per tick and its flat stat changes),
        /// a shield that lasts as long as the pickup says. Applied again, the stronger power stays and the time
        /// starts over.
        /// </summary>
        /// <param name="duration">Seconds (-1 = until removed); null keeps the duration of the effect.</param>
        public bool Apply(int targetId, string effectId, int sourceId, float power, float? duration = null)
        {
            var target = _world.Get(targetId);
            if (target == null || !target.IsAlive() || string.IsNullOrEmpty(effectId))
                return false;
            return _manager.ApplyEffect(targetId, effectId, sourceId.ToString(CultureInfo.InvariantCulture), Time, power, duration);
        }

        public bool Remove(int targetId, string effectId) => _manager.RemoveEffect(targetId, effectId);

        public bool Has(int targetId, string effectId) => _manager.HasEffect(targetId, effectId);

        /// <summary>
        /// The effects on a character now.
        /// </summary>
        public IEnumerable<ActorEffect> On(int entityId) =>
            _manager.GetActiveEffects(entityId).Select(e => new ActorEffect(e.EffectId, e.CurrentStacks, SourceOf(e), e.RemainingDuration));

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            Time += deltaTime;
            _ids.Clear();
            _ids.AddRange(_world.Actors.Select(a => a.EntityId));
            foreach (int id in _ids)
            {
                if (_world.Get(id) == null)
                    continue;
                // Ticks are dealt after the manager is done with the character: a tick may kill it
                _ticks.Clear();
                _ticking = true;
                try
                {
                    _full?.ProcessTicks(id, Time, OnTick);
                    _manager.UpdateEffects(id, deltaTime);
                }
                finally
                {
                    _ticking = false;
                }
                foreach (var tick in _ticks)
                {
                    if (tick.Healing > 0f)
                        _world.Heal(tick.Entity, tick.Healing);
                    if (tick.Damage > 0f)
                        _world.DealDamage(tick.Source, tick.Entity, tick.Damage, AttackContext.Effect(tick.Effect));
                }
            }
        }

        private void OnTick(int entityId, string effectId, float damage, float healing)
        {
            var instance = _manager.GetActiveEffects(entityId).FirstOrDefault(e => e.EffectId == effectId);
            _ticks.Add((entityId, instance != null ? SourceOf(instance) : -1, SourceIdOf(effectId), damage, healing));
        }

        /// <summary>
        /// A change of health from an effect's Resource modifier: dealt now, or after the manager is done when it
        /// comes in the middle of a tick.
        /// </summary>
        internal void ChangeHealth(int entityId, float amount)
        {
            if (_ticking)
            {
                _ticks.Add((entityId, -1, 0, Math.Max(0f, -amount), Math.Max(0f, amount)));
                return;
            }
            if (amount > 0f)
                _world.Heal(entityId, amount);
            else if (amount < 0f)
                _world.DealDamage(-1, entityId, -amount, AttackContext.Effect(0));
        }

        private static int SourceOf(IStatusEffectInstance instance) =>
            int.TryParse(instance.SourceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : -1;

        public void Dispose() => _subscriptions.Dispose();
    }
}
