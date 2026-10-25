using System;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Event fired when a crafting operation has failed.
    /// </summary>
    public class CraftingFailedEvent : EventArgs
    {
        /// <summary>
        /// Entity ID whose crafting failed.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Recipe ID that failed.
        /// </summary>
        public string RecipeId { get; }

        /// <summary>
        /// Reason for crafting failure.
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// Time spent before failure in seconds.
        /// </summary>
        public float TimeBeforeFailureSeconds { get; }

        /// <summary>
        /// Create a new crafting failed event.
        /// </summary>
        public CraftingFailedEvent(int entityId, string recipeId, string failureReason, float timeBeforeFailureSeconds)
        {
            if (string.IsNullOrEmpty(recipeId))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(recipeId));
            if (string.IsNullOrEmpty(failureReason))
                throw new ArgumentException("Failure reason cannot be null or empty", nameof(failureReason));
            if (timeBeforeFailureSeconds < 0)
                throw new ArgumentException("Time cannot be negative", nameof(timeBeforeFailureSeconds));

            EntityId = entityId;
            RecipeId = recipeId;
            FailureReason = failureReason;
            TimeBeforeFailureSeconds = timeBeforeFailureSeconds;
        }
    }
}
