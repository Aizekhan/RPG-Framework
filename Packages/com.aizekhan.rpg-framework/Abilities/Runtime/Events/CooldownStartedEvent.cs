using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Event raised when an ability cooldown starts.
    /// </summary>
    public struct CooldownStartedEvent : IEvent
    {
        /// <summary>Ability ID on cooldown</summary>
        public string AbilityId;

        /// <summary>Entity with the cooldown</summary>
        public int EntityId;

        /// <summary>Cooldown duration in seconds</summary>
        public float Duration;

        /// <summary>Timestamp when cooldown started</summary>
        public float Timestamp;
    }
}
