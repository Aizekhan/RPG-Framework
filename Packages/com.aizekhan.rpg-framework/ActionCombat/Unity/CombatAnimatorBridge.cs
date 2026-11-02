using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// Plays its <see cref="Combatant"/>'s combat on an <see cref="Animator"/>: an attack's start fires its trigger
    /// (the attack's own, or Attack), a charge holds a bool and its release fires a trigger, a dodge and a stagger fire
    /// triggers, a raised block holds a bool. A parameter the controller does not have is skipped. The rules keep the
    /// time; make the clips match it (an <see cref="AttackAsset"/> with a clip takes its phases from the clip). With
    /// an attack speed (<see cref="Combatant.AttackSpeed"/>) the pace float follows
    /// <see cref="CombatRules.AnimationPaceOf"/>: use it as the attack states' speed multiplier, and the clip's
    /// contact stays on the rules' active window however fast or slow the attack is.
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
        [Tooltip("Trigger on every stagger")]
        [SerializeField] private string _hitReact = "HitReact";

        [Tooltip("Triggers by how hard the hit was (the Combatant's hit tiers)")]
        [SerializeField] private string _hitLight = "HitLight";
        [SerializeField] private string _hitHeavy = "HitHeavy";
        [SerializeField] private string _knockdown = "Knockdown";

        [Tooltip("Trigger when a parry starts")]
        [SerializeField] private string _parry = "Parry";

        [Tooltip("Trigger on the parrier when it parries a blow")]
        [SerializeField] private string _parrySuccess = "ParrySuccess";

        [Tooltip("Trigger on an attacker whose blow was parried (it is opened)")]
        [SerializeField] private string _opened = "Opened";

        [Tooltip("Bool, true while the block is up")]
        [SerializeField] private string _block = "Block";

        [Tooltip("Float, the attack's pace (its attack speed during an attack, 1 otherwise): the attack states' speed multiplier")]
        [SerializeField] private string _attackPace = "AttackPace";

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
            _subscriptions.Add(events.Subscribe<HitReactStartedEvent>(e => Mine(e.EntityId, () =>
            {
                Trigger(_hitReact);
                Trigger(e.Tier == HitTier.Knockdown ? _knockdown : e.Tier == HitTier.Heavy ? _hitHeavy : _hitLight);
            })));
            _subscriptions.Add(events.Subscribe<ParryStartedEvent>(e => Mine(e.EntityId, () => Trigger(_parry))));
            _subscriptions.Add(events.Subscribe<ParriedEvent>(e =>
            {
                Mine(e.DefenderId, () => Trigger(_parrySuccess));
                if (e.StunSeconds > 0f)
                    Mine(e.AttackerId, () => Trigger(_opened));
            }));
            _subscriptions.Add(events.Subscribe<BlockRaisedEvent>(e => Mine(e.EntityId, () => SetBool(_block, true))));
            _subscriptions.Add(events.Subscribe<BlockLoweredEvent>(e => Mine(e.EntityId, () => SetBool(_block, false))));
        }

        protected virtual void Update()
        {
            var arena = Owner != null ? Owner.Arena : null;
            if (arena == null || Animator == null || !Has(_attackPace))
                return;
            Animator.SetFloat(_attackPace, arena.Rules.AnimationPaceOf(Owner.Id));
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
