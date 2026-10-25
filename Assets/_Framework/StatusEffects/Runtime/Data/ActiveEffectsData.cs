using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Data structure for storing active effects on an entity.
    /// </summary>
    [Serializable]
    public struct ActiveEffectsData : IComponent
    {
        public List<StatusEffectInstanceData> ActiveInstances;

        /// <summary>
        /// Create an empty ActiveEffectsData.
        /// </summary>
        public static ActiveEffectsData CreateEmpty()
        {
            return new ActiveEffectsData
            {
                ActiveInstances = new List<StatusEffectInstanceData>()
            };
        }

        /// <summary>
        /// Add an effect instance.
        /// </summary>
        public void AddEffect(StatusEffectInstanceData instance)
        {
            if (ActiveInstances == null)
                ActiveInstances = new List<StatusEffectInstanceData>();

            ActiveInstances.Add(instance);
        }

        /// <summary>
        /// Remove an effect by ID.
        /// </summary>
        public void RemoveEffect(string effectId)
        {
            if (ActiveInstances == null)
                return;

            ActiveInstances.RemoveAll(e => e.EffectId == effectId);
        }

        /// <summary>
        /// Get an effect by ID.
        /// </summary>
        public StatusEffectInstanceData? GetEffect(string effectId)
        {
            if (ActiveInstances == null)
                return null;

            var effect = ActiveInstances.FirstOrDefault(e => e.EffectId == effectId);
            return effect.EffectId == effectId ? effect : (StatusEffectInstanceData?)null;
        }

        /// <summary>
        /// Check if an effect exists.
        /// </summary>
        public bool HasEffect(string effectId)
        {
            if (ActiveInstances == null)
                return false;

            return ActiveInstances.Any(e => e.EffectId == effectId);
        }

        /// <summary>
        /// Remove all expired effects.
        /// </summary>
        public void RemoveExpired()
        {
            if (ActiveInstances == null)
                return;

            ActiveInstances.RemoveAll(e => e.RemainingDuration <= 0f && e.RemainingDuration != -1f);
        }
    }
}
