using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A character entered the world; the presentation shows it.
    /// </summary>
    public readonly struct ActorSpawnedEvent : IEvent
    {
        public ActorSpawnedEvent(int entityId, string characterId, bool isPlayer)
        {
            EntityId = entityId;
            CharacterId = characterId;
            IsPlayer = isPlayer;
        }

        public int EntityId { get; }
        public string CharacterId { get; }
        public bool IsPlayer { get; }
    }

    /// <summary>
    /// A character left the world (a slain enemy, or everyone at shutdown); the presentation removes it.
    /// </summary>
    public readonly struct ActorDespawnedEvent : IEvent
    {
        public ActorDespawnedEvent(int entityId, string characterId, bool isPlayer)
        {
            EntityId = entityId;
            CharacterId = characterId;
            IsPlayer = isPlayer;
        }

        public int EntityId { get; }
        public string CharacterId { get; }
        public bool IsPlayer { get; }
    }

    /// <summary>
    /// An enemy was killed, published before it leaves the world (bounties, combo counters, achievements).
    /// </summary>
    public readonly struct ActorKilledEvent : IEvent
    {
        public ActorKilledEvent(int entityId, string characterId, int killerId, bool byPlayer, AttackContext context = default)
        {
            EntityId = entityId;
            CharacterId = characterId;
            KillerId = killerId;
            ByPlayer = byPlayer;
            Context = context;
        }

        public int EntityId { get; }
        public string CharacterId { get; }
        public int KillerId { get; }
        public bool ByPlayer { get; }

        /// <summary>
        /// The hit that killed: its tags and source, and whether an effect dealt it (a poison kill has
        /// <c>FromEffect</c> and <c>SourceId == ActorEffects.SourceIdOf("poison")</c>). Default when the character
        /// was killed without a hit (a rule calling the life states directly).
        /// </summary>
        public AttackContext Context { get; }
    }

    /// <summary>
    /// A hit landed (hit effects, damage numbers), with what kind of hit it was.
    /// </summary>
    public readonly struct ActorDamagedEvent : IEvent
    {
        public ActorDamagedEvent(int targetId, int attackerId, float amount, float healthLeft)
            : this(targetId, attackerId, amount, healthLeft, default)
        {
        }

        public ActorDamagedEvent(int targetId, int attackerId, float amount, float healthLeft, AttackContext context)
        {
            TargetId = targetId;
            AttackerId = attackerId;
            Amount = amount;
            HealthLeft = healthLeft;
            Context = context;
        }

        public int TargetId { get; }

        /// <summary>
        /// Who dealt it; -1 when nobody did (a trap, the world).
        /// </summary>
        public int AttackerId { get; }

        public float Amount { get; }
        public float HealthLeft { get; }

        /// <summary>
        /// Tags, source and whether it came from an effect (<see cref="AttackContext.FromEffect"/>).
        /// </summary>
        public AttackContext Context { get; }
    }

    /// <summary>
    /// A character got health back (healing numbers).
    /// </summary>
    public readonly struct ActorHealedEvent : IEvent
    {
        public ActorHealedEvent(int entityId, float amount, float health)
        {
            EntityId = entityId;
            Amount = amount;
            Health = health;
        }

        public int EntityId { get; }
        public float Amount { get; }
        public float Health { get; }
    }

    /// <summary>
    /// A new wave of enemies started.
    /// </summary>
    public readonly struct WaveStartedEvent : IEvent
    {
        public WaveStartedEvent(int wave, int enemies)
        {
            Wave = wave;
            Enemies = enemies;
        }

        public int Wave { get; }
        public int Enemies { get; }
    }
}
