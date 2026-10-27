using RPGFramework.Events;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a status effect expires.
    /// Published by <c>StatusEffectManager.UpdateEffects</c> (when given an event publisher) when an effect's duration
    /// runs out, followed by an <see cref="EffectRemovedEvent"/> with WasExpired = true. ExpireTime is 0 there,
    /// because UpdateEffects only receives a time delta, not the current time.
    /// </summary>
    public struct EffectExpiredEvent : IEvent
    {
        public int EntityId;
        public string EffectId;
        public float ExpireTime;

        public EffectExpiredEvent(int entityId, string effectId, float expireTime)
        {
            EntityId = entityId;
            EffectId = effectId;
            ExpireTime = expireTime;
        }
    }
}
