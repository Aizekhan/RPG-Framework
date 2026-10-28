using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Event raised when a running cooldown is cleared before it ends (a reset, a refund, "all cooldowns ready").
    /// </summary>
    public struct CooldownClearedEvent : IEvent
    {
        /// <summary>Ability ID whose cooldown was cleared; null when every cooldown of the entity was</summary>
        public string AbilityId;

        /// <summary>Entity with the cooldown</summary>
        public int EntityId;
    }
}
