using System;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Data for an active status effect instance.
    /// </summary>
    [Serializable]
    public struct StatusEffectInstanceData
    {
        public string EffectId;
        public int EntityId;
        public float StartTime;
        public float RemainingDuration;
        public int CurrentStacks;
        public string SourceId;
        public float LastTickTime;

        /// <summary>
        /// Create a new StatusEffectInstanceData.
        /// </summary>
        public static StatusEffectInstanceData Create(string effectId, int entityId, float duration, string sourceId, float currentTime)
        {
            return new StatusEffectInstanceData
            {
                EffectId = effectId,
                EntityId = entityId,
                StartTime = currentTime,
                RemainingDuration = duration,
                CurrentStacks = 1,
                SourceId = sourceId,
                LastTickTime = currentTime
            };
        }
    }
}
