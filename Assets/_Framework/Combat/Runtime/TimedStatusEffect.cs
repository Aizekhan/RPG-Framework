using System;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Status effect that automatically expires after a duration.
    /// Use this for simple timed buffs/debuffs without recurring behavior.
    /// </summary>
    public class TimedStatusEffect : StatusEffect
    {
        /// <summary>
        /// Create a timed status effect.
        /// </summary>
        /// <param name="effectId">Unique identifier</param>
        /// <param name="name">Display name</param>
        /// <param name="effectType">Effect classification</param>
        /// <param name="duration">Duration in seconds (must be positive)</param>
        /// <param name="stackBehavior">How effect stacks with itself</param>
        /// <param name="maxStacks">Maximum stack count</param>
        public TimedStatusEffect(
            string effectId,
            string name,
            StatusEffectType effectType,
            float duration,
            StackBehavior stackBehavior = StackBehavior.Replace,
            int maxStacks = 1)
            : base(effectId, name, effectType, duration, stackBehavior, maxStacks)
        {
            if (duration <= 0f)
                throw new ArgumentException("Timed effects must have positive duration", nameof(duration));
        }
    }
}
