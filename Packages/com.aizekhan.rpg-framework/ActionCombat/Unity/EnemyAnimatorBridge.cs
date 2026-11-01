using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// Plays a <see cref="CombatEnemyAI"/>'s locomotion and moments on an <see cref="Animator"/>: Speed (m/s),
    /// MoveX / MoveY (local sideways / forward m/s, for a strafe blend tree), Notice (a roar) and Death with DeathIndex
    /// (which death clip). Attacks, blocks, dodges and hit reactions are <see cref="CombatAnimatorBridge"/>'s. A
    /// parameter the controller does not have is skipped.
    /// </summary>
    public class EnemyAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private CombatEnemyAI _enemy;

        [Tooltip("Empty: the Animator on this object or its children")]
        [SerializeField] private Animator _animator;

        [Header("Parameters")]
        [SerializeField] private string _speed = "Speed";
        [SerializeField] private string _moveX = "MoveX";
        [SerializeField] private string _moveY = "MoveY";
        [SerializeField] private string _notice = "Notice";
        [SerializeField] private string _death = "Death";
        [SerializeField] private string _deathIndex = "DeathIndex";

        [Tooltip("Smoothing of the locomotion values (seconds)")]
        [SerializeField, Min(0f)] private float _damping = 0.1f;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly HashSet<int> _parameters = new HashSet<int>();
        private RuntimeAnimatorController _known;

        public CombatEnemyAI Enemy => _enemy != null ? _enemy : _enemy = GetComponentInParent<CombatEnemyAI>();
        public Animator Animator => _animator != null ? _animator : _animator = GetComponentInChildren<Animator>();

        protected virtual void OnEnable()
        {
            var arena = Enemy != null ? Enemy.Combatant.Arena : null;
            if (arena == null)
                return;
            _subscriptions.Add(arena.Events.Subscribe<EnemyNoticedEvent>(e =>
            {
                if (!e.Struck && Mine(e.EnemyId))
                    Trigger(_notice);
            }));
            _subscriptions.Add(arena.Events.Subscribe<EnemyDiedEvent>(e =>
            {
                if (!Mine(e.EnemyId))
                    return;
                if (Has(_deathIndex))
                    Animator.SetInteger(_deathIndex, e.Variant);
                Trigger(_death);
            }));
        }

        protected virtual void OnDisable()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
        }

        protected virtual void LateUpdate()
        {
            if (Enemy == null || Animator == null)
                return;
            var velocity = Enemy.Velocity;
            velocity.y = 0f;
            var local = Enemy.transform.InverseTransformDirection(velocity);
            SetFloat(_speed, velocity.magnitude);
            SetFloat(_moveX, local.x);
            SetFloat(_moveY, local.z);
        }

        private bool Mine(int entityId) => Enemy != null && Animator != null && entityId == Enemy.Combatant.Id;

        private void SetFloat(string parameter, float value)
        {
            if (Has(parameter))
                Animator.SetFloat(parameter, value, _damping, Time.deltaTime);
        }

        private void Trigger(string parameter)
        {
            if (Has(parameter))
                Animator.SetTrigger(parameter);
        }

        private bool Has(string parameter)
        {
            if (string.IsNullOrEmpty(parameter) || Animator.runtimeAnimatorController == null)
                return false;
            if (_known != Animator.runtimeAnimatorController)
            {
                _known = Animator.runtimeAnimatorController;
                _parameters.Clear();
                foreach (var known in Animator.parameters)
                    _parameters.Add(known.nameHash);
            }
            return _parameters.Contains(Animator.StringToHash(parameter));
        }
    }
}
