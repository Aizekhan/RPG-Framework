namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a DoT/HoT effect ticks.
    /// </summary>
    public struct EffectTickEvent
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
