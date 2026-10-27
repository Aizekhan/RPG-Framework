using RPGFramework.Events;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a DoT/HoT effect ticks.
    /// Published by <c>StatusEffectManager.ProcessTicks</c> (when given an event publisher) for each effect that ticks
    /// with non-zero damage or healing (the same ticks the onTick callback receives); TickTime is the current time passed in.
    /// </summary>
    public struct EffectTickEvent : IEvent
    {
        public int EntityId;
        public string EffectId;
        public float Damage;   // For DoT effects
        public float Healing;  // For HoT effects
        public float TickTime;

        public EffectTickEvent(int entityId, string effectId, float damage, float healing, float tickTime)
        {
            EntityId = entityId;
            EffectId = effectId;
            Damage = damage;
            Healing = healing;
            TickTime = tickTime;
        }
    }
}
