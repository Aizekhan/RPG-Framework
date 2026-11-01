using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// A flying hitbox (an arrow, a bolt): it flies straight along its direction and sweeps a sphere each frame, so a
    /// fast one does not pass through a body. The bodies it meets are reported with its swing; its flight is the
    /// swing's active window. It stops at the first body (unless it pierces) or at a solid collider that is not a body,
    /// and ends its swing (<see cref="CombatRules.EndSwing"/>). <see cref="ProjectileLauncher"/> fires it.
    /// </summary>
    public class CombatProjectile : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _radius = 0.1f;

        [Tooltip("Flies through bodies (each once) instead of stopping at the first")]
        [SerializeField] private bool _pierce;

        [Tooltip("Stops at solid colliders that are not bodies (walls, the ground)")]
        [SerializeField] private bool _stopOnWorld = true;

        [SerializeField] private LayerMask _layers = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Combatant _owner;
        private CombatRules _rules;
        private Vector3 _velocity;
        private float _lifeLeft;
        private bool _flying;

        public int Swing { get; private set; }
        public Combatant Owner => _owner;
        public bool IsFlying => _flying;

        /// <summary>
        /// Sends it off for <paramref name="owner"/>'s swing.
        /// </summary>
        public void Launch(Combatant owner, int swingId, Vector3 direction, float speed, float lifetime)
        {
            _owner = owner;
            _rules = owner != null && owner.Arena != null ? owner.Arena.Rules : null;
            Swing = swingId;
            _velocity = direction.normalized * speed;
            _lifeLeft = lifetime;
            _flying = true;
            if (_velocity.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(_velocity);
        }

        /// <summary>
        /// Moves it by one step of <paramref name="deltaTime"/> and reports what it met.
        /// </summary>
        public void Step(float deltaTime)
        {
            if (!_flying)
                return;
            _lifeLeft -= deltaTime;
            var from = transform.position;
            var travel = _velocity * deltaTime;
            float distance = travel.magnitude;
            if (distance > 0f)
            {
                int count = Physics.SphereCastNonAlloc(from, _radius, travel / distance, _hits, distance, _layers,
                    QueryTriggerInteraction.Collide);
                System.Array.Sort(_hits, 0, count, HitDistance.Instance);
                for (int i = 0; i < count; i++)
                {
                    if (Meet(_hits[i]))
                    {
                        transform.position = _hits[i].distance > 0f ? from + travel / distance * _hits[i].distance : from;
                        Finish();
                        return;
                    }
                }
                transform.position = from + travel;
            }
            if (_lifeLeft <= 0f)
                Finish();
        }

        /// <summary>
        /// Stops it and ends its swing (it is destroyed; override <see cref="Expire"/> to pool it).
        /// </summary>
        public void Finish()
        {
            if (!_flying)
                return;
            _flying = false;
            _rules?.EndSwing(Swing);
            Expire();
        }

        protected virtual void Expire() => Destroy(gameObject);

        protected virtual void Update() => Step(Time.deltaTime);

        // True when the projectile stops here
        private bool Meet(RaycastHit hit)
        {
            var hurtbox = Hurtbox.Of(hit.collider);
            if (hurtbox != null)
            {
                int target = hurtbox.EntityId;
                if (_owner == null || target < 0 || target == _owner.Id)
                    return false;
                var result = _rules != null
                    ? _rules.ReportContact(_owner.Id, target, Swing, Combatant.ToCombat(hit.point))
                    : ContactResult.Ignored;
                // A dodged arrow flies on; a pierce flies through
                return result != ContactResult.Dodged && result != ContactResult.Ignored && !_pierce;
            }
            if (hit.collider.isTrigger || !_stopOnWorld)
                return false;
            // Its own shooter's body does not stop it
            return _owner == null || !hit.collider.transform.IsChildOf(_owner.transform);
        }

        private sealed class HitDistance : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();

            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
