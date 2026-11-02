using System;
using System.Collections.Generic;
using RPGFramework.Combat;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// One attack of an action combat: how long it winds up, how long it can hit (its active window) and how long
    /// the attacker recovers, what it multiplies the attacker's damage by, how hard it staggers, and whether it can be
    /// held to charge (a heavy swing, a bow's draw) or flies as a projectile.
    /// </summary>
    public sealed class AttackDefinition
    {
        private float _damageMultiplier = 1f;
        private float _stagger = 1f;
        private float _blockCost = 1f;
        private float _maxChargeMultiplier = 1f;
        private float _projectileLifetime = 5f;
        private float _holdPoint = 1f;
        private float _staminaCost;
        private AttackWarp _warp;
        private AttackMotion[] _motions = Array.Empty<AttackMotion>();
        private SweepSegment[] _sweep = Array.Empty<SweepSegment>();

        /// <param name="id">The game's name for the attack ("slash", "heavy", "bow").</param>
        /// <param name="windUp">Seconds before the hit window opens.</param>
        /// <param name="active">Seconds the hit window is open (for a projectile, the release; its flight is the window).</param>
        /// <param name="recovery">Seconds after the window before the attacker can act again.</param>
        public AttackDefinition(string id, float windUp, float active, float recovery)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("An attack needs an id", nameof(id));
            Id = id;
            WindUp = Seconds(windUp, nameof(windUp));
            Active = Seconds(active, nameof(active));
            Recovery = Seconds(recovery, nameof(recovery));
        }

        public string Id { get; }
        public float WindUp { get; }
        public float Active { get; }
        public float Recovery { get; }

        /// <summary>
        /// The whole attack, wind-up to the end of recovery.
        /// </summary>
        public float Duration => WindUp + Active + Recovery;

        /// <summary>
        /// The attacker's damage times this (1 = a plain strike).
        /// </summary>
        public float DamageMultiplier
        {
            get => _damageMultiplier;
            set => _damageMultiplier = NotNegative(value, nameof(DamageMultiplier));
        }

        /// <summary>
        /// How much poise a hit takes from the target (<see cref="CombatantSettings.Poise"/>); 0 never staggers.
        /// </summary>
        public float Stagger
        {
            get => _stagger;
            set => _stagger = NotNegative(value, nameof(Stagger));
        }

        /// <summary>
        /// False: hyper armour - a hit does not stagger the attacker out of this attack (it still takes the damage).
        /// </summary>
        public bool Interruptible { get; set; } = true;

        /// <summary>
        /// What blocking one blow of this attack costs the defender (<see cref="IBlockStock"/>, such as stamina).
        /// </summary>
        public float BlockCost
        {
            get => _blockCost;
            set => _blockCost = NotNegative(value, nameof(BlockCost));
        }

        /// <summary>
        /// What starting the attack costs the attacker (stamina) from <see cref="CombatRules.AttackStock"/>; an attacker
        /// that cannot pay does not start it. 0 = free.
        /// </summary>
        public float StaminaCost
        {
            get => _staminaCost;
            set => _staminaCost = NotNegative(value, nameof(StaminaCost));
        }

        /// <summary>
        /// False: it cannot be parried (a parry meets it as a block would, or as a failed parry).
        /// </summary>
        public bool Parryable { get; set; } = true;

        /// <summary>
        /// Motion warping: during part of a phase the attack pulls its attacker to its target and turns it to face it
        /// (<see cref="AttackWarp"/>, <see cref="CombatRules.WarpOf"/>); default: none.
        /// </summary>
        public AttackWarp Warp
        {
            get => _warp;
            set
            {
                string wrong = value.Problem();
                if (wrong != null)
                    throw new ArgumentException("Warp: " + wrong, nameof(Warp));
                _warp = value;
            }
        }

        /// <summary>
        /// Game-defined bits passed to the damage rules (<see cref="AttackContext.Tags"/>).
        /// </summary>
        public int Tags { get; set; }

        /// <summary>
        /// What dealt the hit, as the game numbers it (<see cref="AttackContext.SourceId"/>); 0 = a plain strike.
        /// </summary>
        public int SourceId { get; set; }

        /// <summary>
        /// The longest charge that still counts, in seconds; 0 = the attack cannot be charged.
        /// </summary>
        public float MaxCharge { get; set; }

        /// <summary>
        /// A release before this many seconds is a quick attack (<see cref="QuickAttack"/>, or this one uncharged).
        /// </summary>
        public float MinCharge { get; set; }

        /// <summary>
        /// The damage multiplier of a full charge (a charge of <see cref="MinCharge"/> multiplies by 1).
        /// </summary>
        public float MaxChargeMultiplier
        {
            get => _maxChargeMultiplier;
            set => _maxChargeMultiplier = NotNegative(value, nameof(MaxChargeMultiplier));
        }

        /// <summary>
        /// The shape of the charge: share of the charge (0..1) → share of the extra damage (0..1). Null: linear.
        /// </summary>
        public Func<float, float> ChargeCurve { get; set; }

        /// <summary>
        /// What a release under <see cref="MinCharge"/> plays instead (a quick slash, a weak shot); null: this attack,
        /// uncharged.
        /// </summary>
        public AttackDefinition QuickAttack { get; set; }

        public bool CanCharge => MaxCharge > 0f;

        /// <summary>
        /// The attack flies (an arrow, a bolt): the hit window opens when the active phase starts and stays open until
        /// the projectile ends it (<see cref="CombatRules.EndSwing"/>) or <see cref="ProjectileLifetime"/> runs out.
        /// </summary>
        public bool Projectile { get; set; }

        public float ProjectileLifetime
        {
            get => _projectileLifetime;
            set => _projectileLifetime = NotNegative(value, nameof(ProjectileLifetime));
        }

        /// <summary>
        /// Where a held wind-up waits (<see cref="CombatRules.HoldWindUp"/>), as a share of the wind-up: 1 = just
        /// before the blow (a raised axe), 0.5 = halfway.
        /// </summary>
        public float HoldPoint
        {
            get => _holdPoint;
            set
            {
                if (float.IsNaN(value) || value < 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(HoldPoint), value, "A share of the wind-up, 0..1");
                _holdPoint = value;
            }
        }

        /// <summary>
        /// How the attack moves its attacker in its phases (a step in, a lunge, a leap, a step back); empty: it stands.
        /// <see cref="CombatRules.MotionOf"/> gives the speed now.
        /// </summary>
        public IReadOnlyList<AttackMotion> Motions
        {
            get => _motions;
            set => _motions = Checked(value, motion => motion.Problem(), nameof(Motions));
        }

        /// <summary>
        /// The volume the weapon sweeps over the active window, segment by segment (<see cref="SweepSegment"/>); empty:
        /// the game's hitboxes decide contact. <see cref="CombatRules.Sweep"/> tests a body against the segment active
        /// at that moment, so a world without physics (a runner's road) can have weapon contact too.
        /// </summary>
        public IReadOnlyList<SweepSegment> Sweep
        {
            get => _sweep;
            set => _sweep = Checked(value, segment => segment.Problem(), nameof(Sweep));
        }

        internal AttackMotion[] MotionArray => _motions;
        internal SweepSegment[] SweepArray => _sweep;

        /// <summary>
        /// The damage multiplier of a charge held this long (clamped to [<see cref="MinCharge"/>,
        /// <see cref="MaxCharge"/>]); 1 for an attack that cannot be charged.
        /// </summary>
        public float ChargeMultiplier(float chargeSeconds)
        {
            if (!CanCharge)
                return 1f;
            float min = Math.Max(0f, Math.Min(MinCharge, MaxCharge));
            float held = Math.Max(min, Math.Min(MaxCharge, chargeSeconds));
            float share = MaxCharge > min ? (held - min) / (MaxCharge - min) : 1f;
            if (ChargeCurve != null)
                share = Math.Max(0f, Math.Min(1f, ChargeCurve(share)));
            return 1f + (MaxChargeMultiplier - 1f) * share;
        }

        public override string ToString() => $"{Id} ({WindUp:0.###}/{Active:0.###}/{Recovery:0.###}s)";

        private static float Seconds(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name, value, "Seconds are a finite number, 0 or more");
            return value;
        }

        private static float NotNegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name, value, "A finite number, 0 or more");
            return value;
        }

        private static T[] Checked<T>(IReadOnlyList<T> items, Func<T, string> problem, string name)
        {
            if (items == null || items.Count == 0)
                return Array.Empty<T>();
            var copy = new T[items.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                string wrong = problem(items[i]);
                if (wrong != null)
                    throw new ArgumentException($"{name}[{i}]: {wrong}", name);
                copy[i] = items[i];
            }
            return copy;
        }
    }

    /// <summary>
    /// How a combatant takes hits, dodges and blocks.
    /// </summary>
    public sealed class CombatantSettings
    {
        /// <summary>
        /// Seconds a stagger lasts; 0 = this combatant is never staggered.
        /// </summary>
        public float HitReactSeconds { get; set; } = 0.4f;

        /// <summary>
        /// Stagger it shrugs off before it reacts (<see cref="AttackDefinition.Stagger"/> of each hit is taken from
        /// it; at 0 it reacts and the poise refills). 0 = every staggering hit makes it react.
        /// </summary>
        public float Poise { get; set; }

        /// <summary>
        /// Seconds without a hit after which the poise refills.
        /// </summary>
        public float PoiseRecoverySeconds { get; set; } = 3f;

        /// <summary>
        /// A staggering blow with at least this <see cref="AttackDefinition.Stagger"/> is a heavy hit
        /// (<see cref="HitTier.Heavy"/>: <see cref="HeavyHitReactSeconds"/>, <see cref="HeavyKnockback"/>); 0 = no heavy
        /// tier, every reaction is light.
        /// </summary>
        public float HeavyStagger { get; set; }

        /// <summary>
        /// A blow with at least this stagger knocks it down (<see cref="HitTier.Knockdown"/>: down and up again in
        /// <see cref="KnockdownSeconds"/>); 0 = never knocked down.
        /// </summary>
        public float KnockdownStagger { get; set; }

        public float HeavyHitReactSeconds { get; set; } = 0.8f;

        /// <summary>
        /// Seconds of a knockdown, the fall and the get-up together.
        /// </summary>
        public float KnockdownSeconds { get; set; } = 2f;

        /// <summary>
        /// How far a heavy hit pushes it back (the Unity side moves the body; <see cref="HitReactStartedEvent.Knockback"/>).
        /// </summary>
        public float HeavyKnockback { get; set; } = 0.5f;

        public float KnockdownKnockback { get; set; } = 1.5f;

        /// <summary>
        /// Seconds a dodge lasts.
        /// </summary>
        public float DodgeSeconds { get; set; } = 0.5f;

        /// <summary>
        /// When invulnerability starts, in seconds from the start of the dodge.
        /// </summary>
        public float InvulnerableFrom { get; set; }

        /// <summary>
        /// When invulnerability ends, in seconds from the start of the dodge; below 0 = at the end of the dodge
        /// (the whole dodge is invulnerable).
        /// </summary>
        public float InvulnerableUntil { get; set; } = -1f;

        /// <summary>
        /// The block covers blows from within this cone in front (degrees, whole cone).
        /// </summary>
        public float BlockAngle { get; set; } = 120f;

        /// <summary>
        /// Share of a blocked blow's damage that still gets through (0 = all of it stopped).
        /// </summary>
        public float BlockDamageMultiplier { get; set; }

        /// <summary>
        /// Seconds a parry is open (<see cref="CombatRules.Parry"/>, or after the block rises with
        /// <see cref="ParryOnBlockRaise"/>); 0 = this combatant cannot parry.
        /// </summary>
        public float ParryWindow { get; set; }

        /// <summary>
        /// Seconds after the window before a parry ends; a blow then is a failed parry.
        /// </summary>
        public float ParryRecovery { get; set; } = 0.3f;

        /// <summary>
        /// Seconds a parried melee attacker stays opened (its attack broken, <see cref="CombatRules.IsOpened"/>);
        /// 0 = a parry only stops the blow.
        /// </summary>
        public float ParryStunSeconds { get; set; } = 1f;

        /// <summary>
        /// Share of the blow's <see cref="AttackDefinition.BlockCost"/> a parry spends from the block stock (0 = free).
        /// One it cannot pay is not a parry.
        /// </summary>
        public float ParryCost { get; set; }

        /// <summary>
        /// Share of the damage a blow deals in a parry's recovery (a failed parry); 1 = a plain hit.
        /// </summary>
        public float ParryFailDamageMultiplier { get; set; } = 1f;

        /// <summary>
        /// Raising the block parries too: a blow inside <see cref="ParryWindow"/> after the block rose is parried
        /// (a perfect block); later it is blocked as usual.
        /// </summary>
        public bool ParryOnBlockRaise { get; set; }

        /// <summary>
        /// The reaction to a staggering blow of this force.
        /// </summary>
        public HitTier TierOf(float stagger) =>
            KnockdownStagger > 0f && stagger >= KnockdownStagger ? HitTier.Knockdown
            : HeavyStagger > 0f && stagger >= HeavyStagger ? HitTier.Heavy
            : HitTier.Light;

        public float SecondsOf(HitTier tier) =>
            tier == HitTier.Knockdown ? KnockdownSeconds : tier == HitTier.Heavy ? HeavyHitReactSeconds : HitReactSeconds;

        public float KnockbackOf(HitTier tier) =>
            tier == HitTier.Knockdown ? KnockdownKnockback : tier == HitTier.Heavy ? HeavyKnockback : 0f;

        internal float InvulnerableEnd => InvulnerableUntil < 0f ? DodgeSeconds : Math.Min(InvulnerableUntil, DodgeSeconds);
    }

    /// <summary>
    /// What hits do: deals the damage of a landed (or partly blocked) blow. In an action RPG kit game this is
    /// <c>IActorWorld.Attack</c> (<c>ActorWorldDamage</c> in the Unity layer).
    /// </summary>
    public interface ICombatDamage
    {
        /// <summary>
        /// The attacker may hurt the target (both there, alive, hostile).
        /// </summary>
        bool CanHit(int attackerId, int targetId);

        /// <returns>Damage dealt.</returns>
        float Deal(int attackerId, int targetId, AttackContext context);
    }

    /// <summary>
    /// What a block spends per blow (stamina, a shield's durability); as <see cref="CombatRules.AttackStock"/>, what
    /// an attack costs to start. Without one, it is free. The game's policy lives here: a stock may let an exhausted
    /// fighter act anyway (return true) and weaken it its own way.
    /// </summary>
    public interface IBlockStock
    {
        /// <returns>False when it cannot pay: a block's guard breaks and the blow lands; an attack does not start.</returns>
        bool TrySpend(int entityId, float amount);
    }
}
