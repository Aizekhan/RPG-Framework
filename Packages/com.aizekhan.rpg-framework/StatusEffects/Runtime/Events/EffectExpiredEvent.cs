namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a status effect expires.
    /// </summary>
    public struct EffectExpiredEvent
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
