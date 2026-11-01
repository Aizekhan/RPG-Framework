using System;
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
    /// What a block spends per blow (stamina, a shield's durability). Without one, blocking is free.
    /// </summary>
    public interface IBlockStock
    {
        /// <returns>False when the defender cannot pay: the guard breaks and the blow lands.</returns>
        bool TrySpend(int entityId, float amount);
    }
}
