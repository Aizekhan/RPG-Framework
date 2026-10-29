using RPGFramework.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Event fired when a crafting operation has completed successfully.
    /// </summary>
    public class CraftingCompletedEvent : EventArgs, IEvent
    {
        /// <summary>
        /// Entity ID that completed the crafting.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Recipe ID that was crafted.
        /// </summary>
        public string RecipeId { get; }

        /// <summary>
        /// List of crafted results.
        /// </summary>
        public IReadOnlyList<ICraftingResult> Results { get; }

        /// <summary>
        /// Total time spent crafting in seconds.
        /// </summary>
        public float TotalTimeSeconds { get; }

        /// <summary>
        /// Create a new crafting completed event.
        /// </summary>
        public CraftingCompletedEvent(int entityId, string recipeId, IEnumerable<ICraftingResult> results, float totalTimeSeconds)
        {
            if (string.IsNullOrEmpty(recipeId))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(recipeId));
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (totalTimeSeconds < 0)
                throw new ArgumentException("Total time cannot be negative", nameof(totalTimeSeconds));

            EntityId = entityId;
            RecipeId = recipeId;
            Results = results.ToList();
            TotalTimeSeconds = totalTimeSeconds;
        }
    }
}
