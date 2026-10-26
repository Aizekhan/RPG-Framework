namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a status effect is removed from an entity.
    /// </summary>
    public struct EffectRemovedEvent
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
