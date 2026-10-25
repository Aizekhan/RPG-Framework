namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a status effect is applied to an entity.
    /// </summary>
    public struct EffectAppliedEvent
    {
        public int EntityId;
        public string EffectId;
        public string SourceId;
        public float StartTime;
        public int Stacks;

        public EffectAppliedEvent(int entityId, string effectId, string sourceId, float startTime, int stacks)
        {
            EntityId = entityId;
            EffectId = effectId;
            SourceId = sourceId;
            StartTime = startTime;
            Stacks = stacks;
        }
    }
}
