using System;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Abstract base implementation of IStatusEffect.
    /// Provides common functionality for all status effects.
    /// Subclass this to create custom effect types.
    /// </summary>
    public abstract class StatusEffect : IStatusEffect
    {
        public string EffectId { get; protected set; }
        public string Name { get; protected set; }
        public StatusEffectType EffectType { get; protected set; }
        public StackBehavior StackBehavior { get; protected set; }
        public float Duration { get; protected set; }
        public float RemainingDuration { get; protected set; }
        public bool IsActive { get; protected set; }
        public int StackCount { get; protected set; }
        public int MaxStacks { get; protected set; }

        /// <summary>
        /// Create a new status effect.
        /// </summary>
        /// <param name="effectId">Unique identifier for this effect type</param>
        /// <param name="name">Display name</param>
        /// <param name="effectType">Effect classification</param>
        /// <param name="duration">Duration in seconds (-1 for permanent)</param>
        /// <param name="stackBehavior">How effect stacks with itself</param>
        /// <param name="maxStacks">Maximum stack count (for StackCount behavior)</param>
        protected StatusEffect(
            string effectId,
            string name,
            StatusEffectType effectType,
            float duration = -1f,
            StackBehavior stackBehavior = StackBehavior.Replace,
            int maxStacks = 1)
        {
            if (string.IsNullOrEmpty(effectId))
                throw new ArgumentNullException(nameof(effectId), "Effect ID cannot be null or empty");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Effect name cannot be null or empty");
            if (maxStacks < 1)
                throw new ArgumentException("Max stacks must be at least 1", nameof(maxStacks));

            EffectId = effectId;
            Name = name;
            EffectType = effectType;
            Duration = duration;
            RemainingDuration = duration;
            StackBehavior = stackBehavior;
            MaxStacks = maxStacks;
            StackCount = 1;
            IsActive = false;
        }

        /// <summary>
        /// Called when effect is first applied to target.
        /// Override to implement custom application logic.
        /// </summary>
        public virtual void OnApplied(IStatusEffectTarget target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            IsActive = true;
        }

        /// <summary>
        /// Called each update while effect is active.
        /// Override to implement recurring behavior (e.g., DOT ticks).
        /// </summary>
        public virtual void OnUpdate(IStatusEffectTarget target, float deltaTime)
        {
            // Base implementation: no-op
            // Subclasses override for recurring effects
        }

        /// <summary>
        /// Called when effect is removed from target.
        /// Override to implement cleanup logic.
        /// </summary>
        public virtual void OnRemoved(IStatusEffectTarget target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            IsActive = false;
        }

        /// <summary>
        /// Update effect duration by deltaTime.
        /// Returns true if effect expired (duration reached 0).
        /// </summary>
        public bool UpdateDuration(float deltaTime)
        {
            // Permanent effects never expire
            if (Duration < 0f)
                return false;

            RemainingDuration -= deltaTime;

            // Effect expired
            if (RemainingDuration <= 0f)
            {
                RemainingDuration = 0f;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reset remaining duration to maximum.
        /// Used for Refresh stack behavior.
        /// </summary>
        public void RefreshDuration()
        {
            RemainingDuration = Duration;
        }

        /// <summary>
        /// Increment stack count (for StackCount behavior).
        /// Returns false if already at max stacks.
        /// </summary>
        public bool AddStack()
        {
            if (StackBehavior != StackBehavior.StackCount)
                return false;

            if (StackCount >= MaxStacks)
                return false;

            StackCount++;
            return true;
        }

        /// <summary>
        /// Decrement stack count (for StackCount behavior).
        /// Returns false if already at minimum (1).
        /// </summary>
        public bool RemoveStack()
        {
            if (StackBehavior != StackBehavior.StackCount)
                return false;

            if (StackCount <= 1)
                return false;

            StackCount--;
            return true;
        }
    }
}
