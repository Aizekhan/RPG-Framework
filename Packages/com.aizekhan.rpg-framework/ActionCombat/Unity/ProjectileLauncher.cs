using System;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// Fires a <see cref="CombatProjectile"/> from a muzzle when its <see cref="Combatant"/>'s projectile attack enters
    /// its active phase (<see cref="AttackActiveStartedEvent"/>): a bow's release, a bolt. It flies along
    /// <see cref="Aim"/> (the game sets it from the camera or the target), or the muzzle's forward.
    /// </summary>
    public class ProjectileLauncher : MonoBehaviour
    {
        [SerializeField] private Combatant _owner;

        [Tooltip("Where it leaves from and, without an aim, which way it flies; empty: this object")]
        [SerializeField] private Transform _muzzle;

        [SerializeField] private CombatProjectile _prefab;
        [SerializeField, Min(0f)] private float _speed = 25f;

        [Tooltip("Fires only for these attack ids; empty: every projectile attack of the owner")]
        [SerializeField] private string[] _attackIds = new string[0];

        private IDisposable _subscription;

        /// <summary>
        /// The direction to fire in; null: the muzzle's forward.
        /// </summary>
        public Vector3? Aim { get; set; }

        public Combatant Owner => _owner != null ? _owner : _owner = GetComponentInParent<Combatant>();
        public Transform Muzzle => _muzzle != null ? _muzzle : transform;

        /// <summary>
        /// Raised with every projectile it fires.
        /// </summary>
        public event Action<CombatProjectile> Fired;

        /// <summary>
        /// Fires one for a swing now (the launcher calls it itself on the active phase).
        /// </summary>
        public CombatProjectile Fire(int swingId, float lifetime)
        {
            if (_prefab == null || Owner == null)
                return null;
            var muzzle = Muzzle;
            var direction = Aim ?? muzzle.forward;
            var projectile = Instantiate(_prefab, muzzle.position, Quaternion.LookRotation(direction));
            projectile.Launch(Owner, swingId, direction, _speed, lifetime);
            Fired?.Invoke(projectile);
            return projectile;
        }

        protected virtual void OnEnable()
        {
            var arena = Owner != null ? Owner.Arena : null;
            if (arena != null)
                _subscription = arena.Events.Subscribe<AttackActiveStartedEvent>(OnActive);
        }

        protected virtual void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnActive(AttackActiveStartedEvent started)
        {
            if (!started.Projectile || Owner == null || started.EntityId != Owner.Id || !Fires(started.AttackId))
                return;
            var attack = Owner.Arena.Rules.AttackOf(started.EntityId);
            Fire(started.SwingId, attack != null ? attack.ProjectileLifetime : 5f);
        }

        private bool Fires(string attackId)
        {
            if (_attackIds == null || _attackIds.Length == 0)
                return true;
            return Array.IndexOf(_attackIds, attackId) >= 0;
        }
    }
}
