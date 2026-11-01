using System.Collections.Generic;
using RPGFramework.ActionRpg;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// A character in action combat: joins the scene's <see cref="CombatArena"/> under its entity id, tells the rules
    /// where it stands and faces every frame, and offers the actions (attack, charge and release, dodge, block). On an
    /// action RPG kit character the presenter gives it the entity id; elsewhere set <c>Entity Id</c> by hand. Its
    /// <see cref="Hitbox"/>es and <see cref="Hurtbox"/>es find it in their parents.
    /// </summary>
    public class Combatant : ActorView
    {
        public const string InvulnerableStartEvent = "InvulnerableStart";
        public const string InvulnerableEndEvent = "InvulnerableEnd";

        [Tooltip("The entity id when this is not an action RPG kit character (the presenter's id wins)")]
        [SerializeField] private int _entityId = -1;

        [Tooltip("Its attacks; the first is the default")]
        [SerializeField] private AttackAsset[] _attacks = new AttackAsset[0];

        [Header("Hit reaction")]
        [Tooltip("Seconds a stagger lasts; 0: never staggered")]
        [SerializeField, Min(0f)] private float _hitReactSeconds = 0.4f;

        [Tooltip("Stagger it shrugs off before reacting; 0: every staggering hit")]
        [SerializeField, Min(0f)] private float _poise;

        [SerializeField, Min(0f)] private float _poiseRecoverySeconds = 3f;

        [Header("Dodge")]
        [SerializeField, Min(0f)] private float _dodgeSeconds = 0.5f;

        [Tooltip("Invulnerable from this many seconds into the dodge")]
        [SerializeField, Min(0f)] private float _invulnerableFrom;

        [Tooltip("Invulnerable until this many seconds into the dodge; below 0: until its end")]
        [SerializeField] private float _invulnerableUntil = -1f;

        [Tooltip("The dodge's animation: its length (at Dodge Clip Speed) is the dodge, and animation events named " +
                 "InvulnerableStart / InvulnerableEnd set the invulnerability")]
        [SerializeField] private AnimationClip _dodgeClip;

        [SerializeField, Min(0.01f)] private float _dodgeClipSpeed = 1f;

        [Header("Block")]
        [Tooltip("The block covers blows from this cone in front (degrees)")]
        [SerializeField, Range(0f, 360f)] private float _blockAngle = 120f;

        [Tooltip("Share of a blocked blow's damage that still gets through")]
        [SerializeField, Range(0f, 1f)] private float _blockDamageMultiplier;

        private CombatArena _arena;
        private int _joined = -1;

        /// <summary>
        /// Its entity id in the fight (-1 when it has none yet).
        /// </summary>
        public int Id => IsBound ? EntityId : _entityId;

        public CombatArena Arena => _arena != null ? _arena : _arena = CombatArena.Current;

        public IReadOnlyList<AttackAsset> Attacks => _attacks;

        public CombatState State => Arena != null ? Arena.Rules.StateOf(Id) : CombatState.Idle;

        /// <summary>
        /// The swing of its current attack (what its hitboxes report), or 0.
        /// </summary>
        public int Swing => Arena != null ? Arena.Rules.SwingOf(Id) : 0;

        /// <summary>
        /// Sets the entity id of a combatant that is not an action RPG kit character (it joins the fight under it).
        /// </summary>
        public void UseEntityId(int entityId)
        {
            _entityId = entityId;
            Join();
        }

        /// <summary>
        /// The attack with this id, or the first one for null or "".
        /// </summary>
        public AttackAsset AttackNamed(string attackId)
        {
            foreach (var attack in _attacks)
            {
                if (attack != null && (string.IsNullOrEmpty(attackId) || attack.Id == attackId))
                    return attack;
            }
            return null;
        }

        /// <summary>
        /// Starts an attack (the first for null).
        /// </summary>
        public bool Attack(string attackId = null)
        {
            var attack = AttackNamed(attackId);
            return attack != null && CanAct() && Arena.Rules.Attack(Id, attack.Definition);
        }

        /// <summary>
        /// Starts charging an attack that can be charged (a heavy swing, a bow's draw).
        /// </summary>
        public bool BeginCharge(string attackId = null)
        {
            var attack = AttackNamed(attackId);
            return attack != null && attack.Definition.CanCharge && CanAct() && Arena.Rules.BeginCharge(Id, attack.Definition);
        }

        public bool Release() => CanAct() && Arena.Rules.Release(Id);

        /// <summary>
        /// Dodges along a direction in the world (the body's own movement is the game's).
        /// </summary>
        public bool Dodge(Vector3 direction) => CanAct() && Arena.Rules.Dodge(Id, ToCombat(direction));

        public void SetBlocking(bool held)
        {
            if (Arena != null && Id >= 0)
                Arena.Rules.SetBlocking(Id, held);
        }

        /// <summary>
        /// The rules' settings from the inspector (a dodge clip gives the dodge's time and invulnerability).
        /// </summary>
        public CombatantSettings BuildSettings()
        {
            var settings = new CombatantSettings
            {
                HitReactSeconds = _hitReactSeconds,
                Poise = _poise,
                PoiseRecoverySeconds = _poiseRecoverySeconds,
                DodgeSeconds = _dodgeSeconds,
                InvulnerableFrom = _invulnerableFrom,
                InvulnerableUntil = _invulnerableUntil,
                BlockAngle = _blockAngle,
                BlockDamageMultiplier = _blockDamageMultiplier
            };
            if (_dodgeClip != null && _dodgeClip.length > 0f)
            {
                float speed = Mathf.Max(0.0001f, _dodgeClipSpeed);
                settings.DodgeSeconds = _dodgeClip.length / speed;
                foreach (var animationEvent in _dodgeClip.events)
                {
                    if (animationEvent.functionName == InvulnerableStartEvent)
                        settings.InvulnerableFrom = animationEvent.time / speed;
                    else if (animationEvent.functionName == InvulnerableEndEvent)
                        settings.InvulnerableUntil = animationEvent.time / speed;
                }
            }
            return settings;
        }

        protected virtual void OnEnable() => Join();

        protected virtual void OnDisable() => Leave();

        protected override void OnBound() => Join();

        protected override void OnUnbound() => Leave();

        protected virtual void LateUpdate()
        {
            if (_joined < 0 || Arena == null)
                return;
            Arena.Rules.SetPose(_joined, ToCombat(transform.position), ToCombat(transform.forward));
        }

        // A character of the action RPG kit acts only while alive and not stunned
        private bool CanAct()
        {
            if (Arena == null || Id < 0)
                return false;
            Join();
            if (!IsBound)
                return true;
            var actor = Actor;
            return actor != null && actor.IsAlive() && !actor.IsStunned;
        }

        private void Join()
        {
            int id = Id;
            if (!isActiveAndEnabled || id < 0 || id == _joined || Arena == null)
                return;
            Leave();
            Arena.Rules.Add(id, BuildSettings());
            _joined = id;
        }

        private void Leave()
        {
            if (_joined < 0)
                return;
            if (Arena != null)
                Arena.Rules.Remove(_joined);
            _joined = -1;
        }

        internal static CombatVector ToCombat(Vector3 vector) => new CombatVector(vector.x, vector.y, vector.z);

        internal static Vector3 ToUnity(CombatVector vector) => new Vector3(vector.X, vector.Y, vector.Z);
    }
}
