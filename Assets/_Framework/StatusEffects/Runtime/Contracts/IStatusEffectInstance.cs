namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Represents an active instance of a status effect on an entity.
    /// </summary>
    public interface IStatusEffectInstance
    {
        /// <summary>
        /// ID of the effect definition.
        /// </summary>
        string EffectId { get; }

        /// <summary>
        /// Entity this effect is applied to.
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// Time when this effect was applied.
        /// </summary>
        float StartTime { get; }

        /// <summary>
        /// Remaining duration in seconds.
        /// </summary>
        float RemainingDuration { get; }

        /// <summary>
        /// Current number of stacks.
        /// </summary>
        int CurrentStacks { get; }

        /// <summary>
        /// Entity or source that applied this effect.
        /// </summary>
        string SourceId { get; }

        /// <summary>
        /// Time of the last tick (for DoT/HoT effects).
        /// </summary>
        float LastTickTime { get; }

        /// <summary>
        /// Is this effect expired?
        /// </summary>
        bool IsExpired { get; }

        /// <summary>
        /// Update the effect's duration.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update</param>
        void Update(float deltaTime);

        /// <summary>
        /// Add a stack to this effect (if stackable).
        /// </summary>
        void AddStack();

        /// <summary>
        /// Refresh the effect's duration.
        /// </summary>
        /// <param name="newDuration">New duration value</param>
        void Refresh(float newDuration);
    }
}
