namespace RPGFramework.Combat
{
    /// <summary>
    /// Base interface for all status effects in RPG systems.
    /// Represents temporal modifications to entities (buffs, debuffs, DOTs, etc.).
    /// </summary>
    public interface IStatusEffect
    {
        /// <summary>
        /// Unique identifier for this effect type.
        /// Used to identify same effect for stacking purposes.
        /// </summary>
        string EffectId { get; }

        /// <summary>
        /// Human-readable display name of the effect.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Classification of this effect (Buff, Debuff, DOT, etc.).
        /// </summary>
        StatusEffectType EffectType { get; }

        /// <summary>
        /// How this effect stacks when applied multiple times.
        /// </summary>
        StackBehavior StackBehavior { get; }

        /// <summary>
        /// Total duration in seconds (-1 for permanent effects).
        /// </summary>
        float Duration { get; }

        /// <summary>
        /// Remaining duration in seconds (-1 if permanent).
        /// </summary>
        float RemainingDuration { get; }

        /// <summary>
        /// Is this effect currently active?
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Current number of stacks (for StackBehavior.StackCount).
        /// </summary>
        int StackCount { get; }

        /// <summary>
        /// Maximum number of stacks allowed.
        /// </summary>
        int MaxStacks { get; }

        /// <summary>
        /// Called when effect is applied to target for first time.
        /// Use this to apply initial stat modifications or trigger effects.
        /// </summary>
        /// <param name="target">The target entity receiving the effect</param>
        void OnApplied(IStatusEffectTarget target);

        /// <summary>
        /// Called each frame/tick while effect is active.
        /// Use this for recurring effects (DOT, HOT, etc.).
        /// </summary>
        /// <param name="target">The target entity with this effect</param>
        /// <param name="deltaTime">Time since last update in seconds</param>
        void OnUpdate(IStatusEffectTarget target, float deltaTime);

        /// <summary>
        /// Called when effect expires or is manually removed.
        /// Use this to revert stat modifications or cleanup.
        /// </summary>
        /// <param name="target">The target entity losing the effect</param>
        void OnRemoved(IStatusEffectTarget target);

        /// <summary>
        /// Update effect duration by deltaTime.
        /// Returns true if effect has expired (duration reached 0).
        /// </summary>
        /// <param name="deltaTime">Time delta in seconds</param>
        /// <returns>True if effect expired this update</returns>
        bool UpdateDuration(float deltaTime);

        /// <summary>
        /// Reset remaining duration to maximum (for Refresh stack behavior).
        /// </summary>
        void RefreshDuration();

        /// <summary>
        /// Increment stack count (for StackBehavior.StackCount).
        /// Returns false if max stacks reached.
        /// </summary>
        /// <returns>True if stack was added successfully</returns>
        bool AddStack();

        /// <summary>
        /// Decrement stack count (for StackBehavior.StackCount).
        /// Returns false if already at minimum stacks (1).
        /// </summary>
        /// <returns>True if stack was removed successfully</returns>
        bool RemoveStack();
    }
}
