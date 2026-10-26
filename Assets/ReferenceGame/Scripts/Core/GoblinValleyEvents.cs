using RPGFramework.Events;

namespace ReferenceGame
{
    /// <summary>
    /// A combatant entered the valley; the presentation layer spawns its view.
    /// </summary>
    public readonly struct CombatantSpawnedEvent : IEvent
    {
        public CombatantSpawnedEvent(int entityId, string kind)
        {
            EntityId = entityId;
            Kind = kind;
        }

        public int EntityId { get; }
        public string Kind { get; }
    }

    /// <summary>
    /// A combatant left the valley (a slain goblin); the presentation layer removes its view.
    /// </summary>
    public readonly struct CombatantDespawnedEvent : IEvent
    {
        public CombatantDespawnedEvent(int entityId, string kind)
        {
            EntityId = entityId;
            Kind = kind;
        }

        public int EntityId { get; }
        public string Kind { get; }
    }

    /// <summary>
    /// A new wave of goblins started.
    /// </summary>
    public readonly struct WaveStartedEvent : IEvent
    {
        public WaveStartedEvent(int wave, int goblins)
        {
            Wave = wave;
            Goblins = goblins;
        }

        public int Wave { get; }
        public int Goblins { get; }
    }
}
