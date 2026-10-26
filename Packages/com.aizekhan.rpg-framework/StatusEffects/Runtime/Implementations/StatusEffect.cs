using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Implementation of IStatusEffect.
    /// </summary>
    public class StatusEffect : IStatusEffect
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public EffectType Type { get; }
        public float Duration { get; }
        public bool IsStackable { get; }
        public int MaxStacks { get; }
        public float TickInterval { get; }
        public IReadOnlyList<IEffectModifier> Modifiers { get; }

        public StatusEffect(StatusEffectData data)
        {
            Id = data.Id;
            Name = data.Name;
            Description = data.Description;
            Type = data.Type;
            Duration = data.Duration;
            IsStackable = data.IsStackable;
            MaxStacks = data.MaxStacks;
            TickInterval = data.TickInterval;
            Modifiers = data.Modifiers?.Select(m => new EffectModifier(m)).ToList() ?? new List<EffectModifier>();
        }

        public StatusEffect(
            string id,
            string name,
            string description,
            EffectType type,
            float duration,
            bool isStackable,
            int maxStacks,
            float tickInterval,
            List<IEffectModifier> modifiers)
        {
            Id = id;
            Name = name;
            Description = description;
            Type = type;
            Duration = duration;
            IsStackable = isStackable;
            MaxStacks = maxStacks;
            TickInterval = tickInterval;
            Modifiers = modifiers ?? new List<IEffectModifier>();
        }
    }
}
