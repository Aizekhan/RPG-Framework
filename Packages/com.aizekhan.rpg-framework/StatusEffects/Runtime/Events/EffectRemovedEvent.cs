using RPGFramework.Events;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a status effect is removed from an entity.
    /// Published by <c>StatusEffectManager</c> (when given an event publisher) from RemoveEffect and RemoveAllEffects
    /// (WasExpired = false) and from UpdateEffects when an effect expires (WasExpired = true).
    /// RemovalTime is 0 there, because those methods do not receive the current time.
    /// </summary>
    public struct EffectRemovedEvent : IEvent
    {
        public int EntityId;
        public string EffectId;
        public float RemovalTime;
        public bool WasExpired; // true if expired naturally, false if removed manually

        public EffectRemovedEvent(int entityId, string effectId, float removalTime, bool wasExpired)
        {
            EntityId = entityId;
            EffectId = effectId;
            RemovalTime = removalTime;
            WasExpired = wasExpired;
        }
    }
}
