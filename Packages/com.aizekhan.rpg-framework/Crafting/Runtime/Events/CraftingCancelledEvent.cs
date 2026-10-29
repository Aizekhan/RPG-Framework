using RPGFramework.Events;
using System;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Event fired when a crafting operation has been cancelled.
    /// </summary>
    public class CraftingCancelledEvent : EventArgs, IEvent
    {
        /// <summary>
        /// Entity ID that cancelled crafting.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Recipe ID that was cancelled.
        /// </summary>
        public string RecipeId { get; }

        /// <summary>
        /// Time spent crafting before cancellation in seconds.
        /// </summary>
        public float TimeSpentSeconds { get; }

        /// <summary>
        /// Create a new crafting cancelled event.
        /// </summary>
        public CraftingCancelledEvent(int entityId, string recipeId, float timeSpentSeconds)
        {
            if (string.IsNullOrEmpty(recipeId))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(recipeId));
            if (timeSpentSeconds < 0)
                throw new ArgumentException("Time spent cannot be negative", nameof(timeSpentSeconds));

            EntityId = entityId;
            RecipeId = recipeId;
            TimeSpentSeconds = timeSpentSeconds;
        }
    }
}
