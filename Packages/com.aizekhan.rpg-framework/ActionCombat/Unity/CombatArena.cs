using System;
using RPGFramework.ActionRpg;
using RPGFramework.Combat;
using RPGFramework.Events;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// The action RPG kit's world as the damage of action combat: a landed blow is <see cref="IActorWorld.Attack"/>
    /// (the attacker's strike through the damage calculator, crits, evasion, life steal) with the attack's multiplier.
    /// </summary>
    public sealed class ActorWorldDamage : ICombatDamage
    {
        private readonly Func<IActorWorld> _world;

        public ActorWorldDamage(Func<IActorWorld> world) => _world = world ?? throw new ArgumentNullException(nameof(world));

        public bool CanHit(int attackerId, int targetId)
        {
            var world = _world();
            var target = world?.Get(targetId);
            return target != null && target.IsAlive() && world.AreHostile(attackerId, targetId);
        }

        public float Deal(int attackerId, int targetId, AttackContext context) =>
            _world()?.Attack(attackerId, targetId, context) ?? 0f;
    }

    /// <summary>
    /// Runs action combat in a scene: the rules (<see cref="Rules"/>), their events (<see cref="Events"/>) and the
    /// fight's <see cref="Timeline"/>. Put one in the scene; <see cref="Combatant"/>s join it. Damage goes to the
    /// action RPG kit's world (<see cref="ActionRpgHost.CurrentWorld"/>) unless the game gives its own
    /// (<see cref="UseDamage"/>). The fight stands still while the world is paused.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class CombatArena : MonoBehaviour
    {
        [Tooltip("How many entries the timeline keeps (the oldest go first)")]
        [SerializeField, Min(1)] private int _timelineCapacity = 4096;

        private static CombatArena _current;
        private ActionRpgHost _host;
        private float _nextHostSearch;
        private CombatRules _rules;
        private EventBus _events;

        /// <summary>
        /// The arena of the loaded scene (found once and kept), or null.
        /// </summary>
        public static CombatArena Current
        {
            get
            {
                if (_current == null)
                    _current = FindAnyObjectByType<CombatArena>();
                return _current;
            }
        }

        public CombatRules Rules
        {
            get
            {
                Prepare();
                return _rules;
            }
        }

        public CombatTimeline Timeline => Rules.Timeline;

        /// <summary>
        /// The arena's own bus: subscribe to the combat events here (<see cref="AttackActiveStartedEvent"/>,
        /// <see cref="ContactResolvedEvent"/>...).
        /// </summary>
        public IEventBus Events
        {
            get
            {
                Prepare();
                return _events;
            }
        }

        /// <summary>
        /// The world damage goes to (the scene's action RPG host's), or null.
        /// </summary>
        public IActorWorld World
        {
            get
            {
                // A scene without a host is searched again once a second, not every frame
                if (_host == null && Time.unscaledTime >= _nextHostSearch)
                {
                    _host = FindAnyObjectByType<ActionRpgHost>();
                    _nextHostSearch = Time.unscaledTime + 1f;
                }
                return _host != null ? _host.CurrentWorld : null;
            }
        }

        /// <summary>
        /// Landed blows deal damage through this instead of the action RPG kit's world.
        /// </summary>
        public void UseDamage(ICombatDamage damage) => Rules.Damage = damage;

        /// <summary>
        /// Blocks cost this per blow (stamina); null: blocking is free.
        /// </summary>
        public void UseBlockStock(IBlockStock stock) => Rules.BlockStock = stock;

        private void Awake()
        {
            // One arena per scene; a newer one (the next scene's) takes over
            _current = this;
            Prepare();
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
        }

        private void Prepare()
        {
            if (_rules != null)
                return;
            _events = new EventBus();
            _rules = new CombatRules(new ActorWorldDamage(() => World), _events, new CombatTimeline(_timelineCapacity));
        }

        private void Update()
        {
            var world = World;
            if (world != null && world.IsPaused)
                return;
            Rules.Tick(Time.deltaTime);
        }
    }
}
