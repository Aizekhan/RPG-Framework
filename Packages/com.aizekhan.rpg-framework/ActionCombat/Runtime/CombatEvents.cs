using RPGFramework.Events;

namespace RPGFramework.ActionCombat
{
    // Every event carries Time: the rules' clock (CombatRules.Time) at the moment it happened, also inside a long
    // tick, so a timeline shows the exact frame of a phase.

    /// <summary>
    /// An attack started: its wind-up, or (<see cref="Charging"/>) its charge.
    /// </summary>
    public readonly struct AttackStartedEvent : IEvent
    {
        public AttackStartedEvent(int entityId, int swingId, string attackId, bool charging, float time)
        {
            EntityId = entityId;
            SwingId = swingId;
            AttackId = attackId;
            Charging = charging;
            Time = time;
        }

        public int EntityId { get; }
        public int SwingId { get; }
        public string AttackId { get; }
        public bool Charging { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A wind-up was held at its attack's hold point (<see cref="Held"/>), or let go on to the blow
    /// (<see cref="CombatRules.HoldWindUp"/>). The animation stands still while it is held
    /// (<see cref="CombatRules.AnimationPaceOf"/> is 0 there).
    /// </summary>
    /// <summary>
    /// A parry began (<see cref="CombatRules.Parry"/>).
    /// </summary>
    public readonly struct ParryStartedEvent : IEvent
    {
        public ParryStartedEvent(int entityId, float window, float seconds, float time)
        {
            EntityId = entityId;
            Window = window;
            Seconds = seconds;
            Time = time;
        }

        public int EntityId { get; }

        /// <summary>
        /// Seconds the parry is open.
        /// </summary>
        public float Window { get; }

        /// <summary>
        /// Seconds the whole parry lasts, its recovery included.
        /// </summary>
        public float Seconds { get; }

        public float Time { get; }
    }

    /// <summary>
    /// A blow was parried: no damage; a melee attacker's attack is broken and it is opened for
    /// <see cref="StunSeconds"/> (0 for a projectile, or when the parrier's settings open no one).
    /// </summary>
    public readonly struct ParriedEvent : IEvent
    {
        public ParriedEvent(int defenderId, int attackerId, int swingId, string attackId, float stunSeconds, bool onBlockRaise, float time)
        {
            DefenderId = defenderId;
            AttackerId = attackerId;
            SwingId = swingId;
            AttackId = attackId;
            StunSeconds = stunSeconds;
            OnBlockRaise = onBlockRaise;
            Time = time;
        }

        public int DefenderId { get; }
        public int AttackerId { get; }
        public int SwingId { get; }
        public string AttackId { get; }
        public float StunSeconds { get; }

        /// <summary>
        /// The block had just risen (<see cref="CombatantSettings.ParryOnBlockRaise"/>), not a parry of its own.
        /// </summary>
        public bool OnBlockRaise { get; }

        public float Time { get; }
    }

    public readonly struct WindUpHeldEvent : IEvent
    {
        public WindUpHeldEvent(int entityId, int swingId, string attackId, bool held, float time)
        {
            EntityId = entityId;
            SwingId = swingId;
            AttackId = attackId;
            Held = held;
            Time = time;
        }

        public int EntityId { get; }
        public int SwingId { get; }
        public string AttackId { get; }
        public bool Held { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A charged attack was let go: what it became (<see cref="AttackId"/>, the quick attack when
    /// <see cref="Quick"/>), how long it was held (clamped) and what it multiplies the damage by.
    /// </summary>
    public readonly struct AttackReleasedEvent : IEvent
    {
        public AttackReleasedEvent(int entityId, int swingId, string attackId, float chargeSeconds, float multiplier, bool quick, float time)
        {
            EntityId = entityId;
            SwingId = swingId;
            AttackId = attackId;
            ChargeSeconds = chargeSeconds;
            Multiplier = multiplier;
            Quick = quick;
            Time = time;
        }

        public int EntityId { get; }
        public int SwingId { get; }
        public string AttackId { get; }
        public float ChargeSeconds { get; }
        public float Multiplier { get; }
        public bool Quick { get; }
        public float Time { get; }
    }

    /// <summary>
    /// The attack's hit window opened (for a projectile: fire it now with this swing).
    /// </summary>
    public readonly struct AttackActiveStartedEvent : IEvent
    {
        public AttackActiveStartedEvent(int entityId, int swingId, string attackId, bool projectile, float time)
        {
            EntityId = entityId;
            SwingId = swingId;
            AttackId = attackId;
            Projectile = projectile;
            Time = time;
        }

        public int EntityId { get; }
        public int SwingId { get; }
        public string AttackId { get; }
        public bool Projectile { get; }
        public float Time { get; }
    }

    /// <summary>
    /// The attacker's active phase ended (a melee swing can no longer hit; a projectile flies on).
    /// </summary>
    public readonly struct AttackActiveEndedEvent : IEvent
    {
        public AttackActiveEndedEvent(int entityId, int swingId, string attackId, float time)
        {
            EntityId = entityId;
            SwingId = swingId;
            AttackId = attackId;
            Time = time;
        }

        public int EntityId { get; }
        public int SwingId { get; }
        public string AttackId { get; }
        public float Time { get; }
    }

    /// <summary>
    /// The attack is over: recovered, or cut short (<see cref="Interrupted"/>: a stagger, a dodge out of the charge or
    /// the recovery, the combatant leaving).
    /// </summary>
    public readonly struct AttackEndedEvent : IEvent
    {
        public AttackEndedEvent(int entityId, int swingId, string attackId, bool interrupted, float time)
        {
            EntityId = entityId;
            SwingId = swingId;
            AttackId = attackId;
            Interrupted = interrupted;
            Time = time;
        }

        public int EntityId { get; }
        public int SwingId { get; }
        public string AttackId { get; }
        public bool Interrupted { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A hitbox touched a body and the rules decided what it was.
    /// </summary>
    public readonly struct ContactResolvedEvent : IEvent
    {
        public ContactResolvedEvent(int attackerId, int targetId, int swingId, string attackId, ContactResult result,
            ContactIgnored ignored, float damage, bool guardBroken, CombatVector point, float time)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            SwingId = swingId;
            AttackId = attackId;
            Result = result;
            Ignored = ignored;
            Damage = damage;
            GuardBroken = guardBroken;
            Point = point;
            Time = time;
        }

        public int AttackerId { get; }
        public int TargetId { get; }
        public int SwingId { get; }

        /// <summary>
        /// The swing's attack; null when the swing is unknown.
        /// </summary>
        public string AttackId { get; }

        public ContactResult Result { get; }

        /// <summary>
        /// Why it was ignored (<see cref="ContactIgnored.None"/> unless <see cref="Result"/> is Ignored).
        /// </summary>
        public ContactIgnored Ignored { get; }

        /// <summary>
        /// Damage dealt (a hit, or what got through a block).
        /// </summary>
        public float Damage { get; }

        /// <summary>
        /// The defender blocked but could not pay for it: the blow landed.
        /// </summary>
        public bool GuardBroken { get; }

        public CombatVector Point { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A hit staggered the combatant for <see cref="Seconds"/>: how hard (<see cref="Tier"/>) and how far it is pushed
    /// along <see cref="Direction"/> (the Unity side moves the body).
    /// </summary>
    public readonly struct HitReactStartedEvent : IEvent
    {
        public HitReactStartedEvent(int entityId, int attackerId, float seconds, float time)
            : this(entityId, attackerId, seconds, time, HitTier.Light, 0f, CombatVector.Zero)
        {
        }

        public HitReactStartedEvent(int entityId, int attackerId, float seconds, float time, HitTier tier, float knockback,
            CombatVector direction)
        {
            EntityId = entityId;
            AttackerId = attackerId;
            Seconds = seconds;
            Time = time;
            Tier = tier;
            Knockback = knockback;
            Direction = direction;
        }

        public int EntityId { get; }
        public int AttackerId { get; }
        public float Seconds { get; }
        public float Time { get; }
        public HitTier Tier { get; }

        /// <summary>
        /// Metres to push the body back (0: none).
        /// </summary>
        public float Knockback { get; }

        /// <summary>
        /// Away from the attacker, on the ground (zero when the poses are unknown).
        /// </summary>
        public CombatVector Direction { get; }
    }

    public readonly struct HitReactEndedEvent : IEvent
    {
        public HitReactEndedEvent(int entityId, float time)
        {
            EntityId = entityId;
            Time = time;
        }

        public int EntityId { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A dodge started; invulnerable from <see cref="InvulnerableFrom"/> until <see cref="InvulnerableUntil"/>
    /// (rules time).
    /// </summary>
    public readonly struct DodgeStartedEvent : IEvent
    {
        public DodgeStartedEvent(int entityId, CombatVector direction, float seconds, float invulnerableFrom,
            float invulnerableUntil, float time)
        {
            EntityId = entityId;
            Direction = direction;
            Seconds = seconds;
            InvulnerableFrom = invulnerableFrom;
            InvulnerableUntil = invulnerableUntil;
            Time = time;
        }

        public int EntityId { get; }
        public CombatVector Direction { get; }
        public float Seconds { get; }
        public float InvulnerableFrom { get; }
        public float InvulnerableUntil { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A dodge ended (<see cref="Interrupted"/>: a hit outside its invulnerability cut it short).
    /// </summary>
    public readonly struct DodgeEndedEvent : IEvent
    {
        public DodgeEndedEvent(int entityId, bool interrupted, float time)
        {
            EntityId = entityId;
            Interrupted = interrupted;
            Time = time;
        }

        public int EntityId { get; }
        public bool Interrupted { get; }
        public float Time { get; }
    }

    public readonly struct BlockRaisedEvent : IEvent
    {
        public BlockRaisedEvent(int entityId, float time)
        {
            EntityId = entityId;
            Time = time;
        }

        public int EntityId { get; }
        public float Time { get; }
    }

    public readonly struct BlockLoweredEvent : IEvent
    {
        public BlockLoweredEvent(int entityId, float time)
        {
            EntityId = entityId;
            Time = time;
        }

        public int EntityId { get; }
        public float Time { get; }
    }
}
