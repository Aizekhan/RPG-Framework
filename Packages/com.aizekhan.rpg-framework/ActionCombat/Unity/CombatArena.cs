using System;
using System.Collections.Generic;
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

        [Header("Enemies")]
        [Tooltip("At most this many enemies attack one target at once; the others circle")]
        [SerializeField, Min(0)] private int _attackersPerTarget = 2;

        [Tooltip("Seeds the enemies' rolls (moves, defences, wandering)")]
        [SerializeField] private int _seed;

        [Tooltip("What blocks an enemy's view (line of sight)")]
        [SerializeField] private LayerMask _sightBlockers = ~0;

        [Tooltip("Eye height above a body's origin for the line of sight")]
        [SerializeField, Min(0f)] private float _eyeHeight = 1.5f;

        private readonly Dictionary<int, Transform> _bodies = new Dictionary<int, Transform>();
        private readonly HashSet<int> _targets = new HashSet<int>();
        private readonly RaycastHit[] _sightHits = new RaycastHit[16];
        private EnemyDirector _enemies;

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
        /// The enemies' combat AI (<see cref="CombatEnemyAI"/> joins it).
        /// </summary>
        public EnemyDirector Enemies
        {
            get
            {
                Prepare();
                return _enemies;
            }
        }

        /// <summary>
        /// Where a body is, by entity id (every <see cref="Combatant"/> registers itself; add others here).
        /// </summary>
        public void RegisterBody(int entityId, Transform body)
        {
            if (entityId >= 0 && body != null)
                _bodies[entityId] = body;
        }

        public void ForgetBody(int entityId, Transform body)
        {
            if (_bodies.TryGetValue(entityId, out var known) && known == body)
                _bodies.Remove(entityId);
        }

        public bool TryGetBody(int entityId, out Transform body) =>
            _bodies.TryGetValue(entityId, out body) && body != null;

        /// <summary>
        /// Someone the enemies fight besides the kit's living players (a training dummy, a game without the kit).
        /// </summary>
        public void AddEnemyTarget(int entityId) => _targets.Add(entityId);

        public void RemoveEnemyTarget(int entityId) => _targets.Remove(entityId);

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
            _enemies?.Dispose();
        }

        private void Prepare()
        {
            if (_rules != null)
                return;
            _events = new EventBus();
            _rules = new CombatRules(new ActorWorldDamage(() => World), _events, new CombatTimeline(_timelineCapacity));
            _enemies = new EnemyDirector(_rules, new ArenaEnemyWorld(this), _events, _seed, new AttackTokens(_attackersPerTarget));
        }

        private void Update()
        {
            var world = World;
            if (world != null && world.IsPaused)
                return;
            Rules.Tick(Time.deltaTime);
            Enemies.Tick();
        }

        // The scene as the enemies see it: the kit's living hostile players and the added targets, bodies' poses,
        // a raycast for the line of sight
        private sealed class ArenaEnemyWorld : IEnemyWorld
        {
            private readonly CombatArena _arena;

            public ArenaEnemyWorld(CombatArena arena) => _arena = arena;

            public IEnumerable<int> TargetsOf(int enemyId)
            {
                var world = _arena.World;
                if (world != null)
                {
                    foreach (int player in world.LivingPlayers)
                    {
                        if (world.AreHostile(enemyId, player))
                            yield return player;
                    }
                }
                foreach (int target in _arena._targets)
                {
                    if (target != enemyId && IsAlive(target))
                        yield return target;
                }
            }

            public bool TryGetPose(int entityId, out CombatVector position, out CombatVector forward)
            {
                if (_arena.TryGetBody(entityId, out var body))
                {
                    position = Combatant.ToCombat(body.position);
                    forward = Combatant.ToCombat(body.forward);
                    return true;
                }
                position = forward = CombatVector.Zero;
                return false;
            }

            public bool CanSee(int fromId, int toId)
            {
                if (!_arena.TryGetBody(fromId, out var from) || !_arena.TryGetBody(toId, out var to))
                    return false;
                var eye = from.position + Vector3.up * _arena._eyeHeight;
                var aim = to.position + Vector3.up * _arena._eyeHeight;
                var line = aim - eye;
                float distance = line.magnitude;
                if (distance < 0.01f)
                    return true;
                int count = Physics.RaycastNonAlloc(eye, line / distance, _arena._sightHits, distance, _arena._sightBlockers,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = _arena._sightHits[i].transform;
                    if (!hit.IsChildOf(from) && !hit.IsChildOf(to))
                        return false;
                }
                return true;
            }

            public float HealthShare(int entityId) => _arena.World?.Get(entityId)?.HealthFraction ?? 1f;

            public bool IsAlive(int entityId)
            {
                var actor = _arena.World?.Get(entityId);
                return actor == null ? _arena._bodies.ContainsKey(entityId) || _arena._targets.Contains(entityId) : actor.IsAlive();
            }
        }
    }
}
