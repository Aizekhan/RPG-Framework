using RPGFramework.Events;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Event fired when a status effect is applied to an entity.
    /// Published by <c>StatusEffectManager.ApplyEffect</c> (when given an event publisher) on every successful apply,
    /// including re-applies that refresh or stack an effect already present; StartTime is the time of this apply
    /// and Stacks the stack count after it.
    /// </summary>
    public struct EffectAppliedEvent : IEvent
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
