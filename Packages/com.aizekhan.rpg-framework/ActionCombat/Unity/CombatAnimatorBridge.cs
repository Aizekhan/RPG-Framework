using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// Plays its <see cref="Combatant"/>'s combat on an <see cref="Animator"/>: an attack's start fires its trigger
    /// (the attack's own, or Attack), a charge holds a bool and its release fires a trigger, a dodge and a stagger fire
    /// triggers, a raised block holds a bool. A parameter the controller does not have is skipped. The rules keep the
    /// time; make the clips match it (an <see cref="AttackAsset"/> with a clip takes its phases from the clip).
    /// </summary>
    public class CombatAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private Combatant _owner;

        [Tooltip("Empty: the Animator on this object or its children")]
        [SerializeField] private Animator _animator;

        [Header("Parameters")]
        [Tooltip("Trigger of an attack without its own (AttackAsset.AnimatorTrigger)")]
        [SerializeField] private string _attack = "Attack";

        [Tooltip("Bool, true while a charge is held")]
        [SerializeField] private string _chargeHold = "ChargeHold";

        [Tooltip("Trigger when a charge is let go")]
        [SerializeField] private string _chargeRelease = "ChargeRelease";

        [SerializeField] private string _dodge = "Dodge";
        [SerializeField] private string _hitReact = "HitReact";

        [Tooltip("Bool, true while the block is up")]
        [SerializeField] private string _block = "Block";

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly HashSet<int> _parameters = new HashSet<int>();
        private RuntimeAnimatorController _known;

        public Combatant Owner => _owner != null ? _owner : _owner = GetComponentInParent<Combatant>();
        public Animator Animator => _animator != null ? _animator : _animator = GetComponentInChildren<Animator>();

        protected virtual void OnEnable()
        {
            var arena = Owner != null ? Owner.Arena : null;
            if (arena == null)
                return;
            var events = arena.Events;
            _subscriptions.Add(events.Subscribe<AttackStartedEvent>(OnAttackStarted));
            _subscriptions.Add(events.Subscribe<AttackReleasedEvent>(OnReleased));
            _subscriptions.Add(events.Subscribe<AttackEndedEvent>(e => Mine(e.EntityId, () => SetBool(_chargeHold, false))));
            _subscriptions.Add(events.Subscribe<DodgeStartedEvent>(e => Mine(e.EntityId, () => Trigger(_dodge))));
            _subscriptions.Add(events.Subscribe<HitReactStartedEvent>(e => Mine(e.EntityId, () => Trigger(_hitReact))));
            _subscriptions.Add(events.Subscribe<BlockRaisedEvent>(e => Mine(e.EntityId, () => SetBool(_block, true))));
            _subscriptions.Add(events.Subscribe<BlockLoweredEvent>(e => Mine(e.EntityId, () => SetBool(_block, false))));
        }

        protected virtual void OnDisable()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
        }

        private void OnAttackStarted(AttackStartedEvent started) => Mine(started.EntityId, () =>
        {
            if (started.Charging)
            {
                SetBool(_chargeHold, true);
                return;
            }
            Trigger(TriggerOf(started.AttackId));
        });

        private void OnReleased(AttackReleasedEvent released) => Mine(released.EntityId, () =>
        {
            SetBool(_chargeHold, false);
            Trigger(_chargeRelease);
            // A quick attack plays its own clip
            if (released.Quick)
                Trigger(TriggerOf(released.AttackId));
        });

        private string TriggerOf(string attackId)
        {
            var asset = Owner.AttackNamed(attackId);
            return asset != null && asset.Id == attackId && !string.IsNullOrEmpty(asset.AnimatorTrigger) ? asset.AnimatorTrigger : _attack;
        }

        private void Mine(int entityId, Action action)
        {
            if (Owner != null && entityId == Owner.Id && Animator != null)
                action();
        }

        private void Trigger(string parameter)
        {
            if (Has(parameter))
                Animator.SetTrigger(parameter);
        }

        private void SetBool(string parameter, bool value)
        {
            if (Has(parameter))
                Animator.SetBool(parameter, value);
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
