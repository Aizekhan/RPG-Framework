using System;
using System.Collections.Generic;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Data structure for defining a status effect.
    /// </summary>
    [Serializable]
    public class StatusEffectData
    {
        public string Id;
        public string Name;
        public string Description;
        public EffectType Type;
        public float Duration; // -1 = permanent, 0 = instant, >0 = timed
        public bool IsStackable;
        public int MaxStacks;
        public float TickInterval; // For DoT/HoT effects
        public List<EffectModifierData> Modifiers;

        /// <summary>
        /// Create a new StatusEffectData with basic properties.
        /// </summary>
        public static StatusEffectData Create(string id, string name, EffectType type)
        {
            return new StatusEffectData
            {
                Id = id,
                Name = name,
                Description = string.Empty,
                Type = type,
                Duration = 0f,
                IsStackable = false,
                MaxStacks = 1,
                TickInterval = 0f,
                Modifiers = new List<EffectModifierData>()
            };
        }

        /// <summary>
        /// Create a DoT (Damage over Time) effect.
        /// </summary>
        public static StatusEffectData CreateDoT(string id, string name, float duration, float tickInterval, float damagePerTick)
        {
            var data = Create(id, name, EffectType.DamageOverTime);
            data.Duration = duration;
            data.TickInterval = tickInterval;
            data.Modifiers.Add(EffectModifierData.Create(ModifierType.Damage, "Health", damagePerTick));
            return data;
        }

        /// <summary>
        /// Create a HoT (Heal over Time) effect.
        /// </summary>
        public static StatusEffectData CreateHoT(string id, string name, float duration, float tickInterval, float healingPerTick)
        {
            var data = Create(id, name, EffectType.HealOverTime);
            data.Duration = duration;
            data.TickInterval = tickInterval;
            data.Modifiers.Add(EffectModifierData.Create(ModifierType.Healing, "Health", healingPerTick));
            return data;
        }

        /// <summary>
        /// Create a stat modifier effect (buff or debuff).
        /// </summary>
        public static StatusEffectData CreateStatModifier(string id, string name, EffectType type, float duration, string statId, float value, ModifierOperation operation = ModifierOperation.Add)
        {
            var data = Create(id, name, type);
            data.Duration = duration;
            data.Modifiers.Add(new EffectModifierData
            {
                Type = ModifierType.Stat,
                TargetId = statId,
                Value = value,
                Operation = operation
            });
            return data;
        }
    }
}
