using System;
using System.Collections.Generic;
using RPGFramework.ActionRpg;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// A monster that fights like in an action RPG (<see cref="EnemyDirector"/>): it notices, runs up, circles while
    /// others attack, picks between its moves, chains combos, blocks or dodges, reels from hits by their force and dies
    /// with an animation. It moves its body along the director's <see cref="EnemyIntent"/> (a CharacterController when
    /// there is one), turns no faster than allowed (not at all in an attack's hit window) and is pushed back by heavy
    /// hits. Replaces <see cref="EnemyBrain"/> on a kit enemy; needs a <see cref="Combatant"/> and an
    /// <see cref="EnemyBehaviourAsset"/>. Give the character a corpse time for the death clip
    /// (<see cref="CharacterDefinition.CorpseSeconds"/>).
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public class CombatEnemyAI : EnemyBrain
    {
        [SerializeField] private EnemyBehaviourAsset _behaviour;

        [Tooltip("Waypoints to patrol in a loop; empty: wander or stand at home")]
        [SerializeField] private Transform[] _patrolPoints = new Transform[0];

        [Tooltip("The model raised along a leap attack's arc (a child of the body); empty: the body stays on the ground")]
        [SerializeField] private Transform _leapRoot;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private EnemyBehaviour _built;
        private Combatant _combatant;
        private CharacterController _body;
        private CombatArena _arena;
        private int _joined = -1;
        private Vector3 _push;
        private float _pushLeft;
        private float _leapBase;

        public EnemyBehaviourAsset Behaviour
        {
            get => _behaviour;
            set
            {
                _behaviour = value;
                Leave();
            }
        }

        /// <summary>
        /// Fights by this behaviour instead of the asset's (a monster set up from code).
        /// </summary>
        public void UseBehaviour(EnemyBehaviour behaviour)
        {
            _built = behaviour;
            Leave();
        }

        public Combatant Combatant => _combatant != null ? _combatant : _combatant = GetComponent<Combatant>();

        public EnemyState State => _joined >= 0 && _arena != null ? _arena.Enemies.StateOf(_joined) : EnemyState.Idle;

        public EnemyIntent Intent => _joined >= 0 && _arena != null ? _arena.Enemies.IntentOf(_joined) : default;

        /// <summary>
        /// How the body moved this frame (metres per second, world space), for locomotion animation.
        /// </summary>
        public Vector3 Velocity { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _body = GetComponent<CharacterController>();
            if (_leapRoot != null)
                _leapBase = _leapRoot.localPosition.y;
        }

        protected virtual void OnEnable() => Join();

        protected virtual void OnDisable() => Leave();

        protected override void OnBound()
        {
            base.OnBound();
            Join();
        }

        protected override void OnUnbound()
        {
            Leave();
            base.OnUnbound();
        }

        protected override void OnKilled()
        {
            if (_joined >= 0 && _arena != null)
                _arena.Enemies.Kill(_joined);
        }

        protected override void Update()
        {
            if (_joined != Combatant.Id)
                Join();
            if (_joined < 0 || (IsBound && World.IsPaused))
            {
                Velocity = Vector3.zero;
                return;
            }

            var intent = _arena.Enemies.IntentOf(_joined);
            if (intent.State == EnemyState.Dead)
            {
                Velocity = Vector3.zero;
                return;
            }

            var face = Combatant.ToUnity(intent.Face);
            if (intent.TurnSpeed > 0f)
                Face(face, intent.TurnSpeed);

            var velocity = Combatant.ToUnity(intent.Move) * intent.Speed;
            if (_pushLeft > 0f)
            {
                velocity += _push;
                _pushLeft -= Time.deltaTime;
            }
            if (_body != null && _body.enabled)
                _body.SimpleMove(velocity);
            else
                transform.position += velocity * Time.deltaTime;
            Velocity = velocity;
            if (_leapRoot != null)
            {
                var local = _leapRoot.localPosition;
                _leapRoot.localPosition = new Vector3(local.x, _leapBase + intent.Height, local.z);
            }
        }

        private void Join()
        {
            int id = Combatant.Id;
            if (!isActiveAndEnabled || id < 0 || (_behaviour == null && _built == null) || id == _joined)
                return;
            Leave();
            _arena = Combatant.Arena;
            if (_arena == null)
                return;
            var points = new List<CombatVector>();
            foreach (var point in _patrolPoints)
            {
                if (point != null)
                    points.Add(Combatant.ToCombat(point.position));
            }
            var behaviour = _built ?? _behaviour.Build(points);
            _arena.Enemies.Add(id, behaviour, Combatant.ToCombat(transform.position));
            _subscriptions.Add(_arena.Events.Subscribe<HitReactStartedEvent>(OnHit));
            _joined = id;
        }

        private void Leave()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            if (_joined >= 0 && _arena != null)
                _arena.Enemies.Remove(_joined);
            _joined = -1;
            _pushLeft = 0f;
        }

        // A heavy hit or a knockdown pushes the body back over the first part of the reaction
        private void OnHit(HitReactStartedEvent hit)
        {
            if (hit.EntityId != _joined || hit.Knockback <= 0f)
                return;
            float seconds = Mathf.Clamp(hit.Seconds * 0.4f, 0.1f, 0.5f);
            var direction = Combatant.ToUnity(hit.Direction);
            if (direction.sqrMagnitude < 0.01f)
                direction = -transform.forward;
            _push = direction.normalized * (hit.Knockback / seconds);
            _pushLeft = seconds;
        }
    }
}
