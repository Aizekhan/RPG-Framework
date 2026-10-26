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
    /// A hit landed (hit effects, damage numbers).
    /// </summary>
    public readonly struct ActorDamagedEvent : IEvent
    {
        public ActorDamagedEvent(int targetId, int attackerId, float amount, float healthLeft)
        {
            TargetId = targetId;
            AttackerId = attackerId;
            Amount = amount;
            HealthLeft = healthLeft;
        }

        public int TargetId { get; }
        public int AttackerId { get; }
        public float Amount { get; }
        public float HealthLeft { get; }
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
