using System;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Event fired when a crafting operation has been initiated.
    /// </summary>
    public class CraftingStartedEvent : EventArgs
    {
        /// <summary>
        /// Entity ID that started the crafting.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Recipe ID being crafted.
        /// </summary>
        public string RecipeId { get; }

        /// <summary>
        /// Crafting station being used.
        /// </summary>
        public ICraftingStation Station { get; }

        /// <summary>
        /// Time in seconds for crafting to complete.
        /// </summary>
        public float CraftingTimeSeconds { get; }

        /// <summary>
        /// Create a new crafting started event.
        /// </summary>
        public CraftingStartedEvent(int entityId, string recipeId, ICraftingStation station, float craftingTimeSeconds)
        {
            if (string.IsNullOrEmpty(recipeId))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(recipeId));
            if (station == null)
                throw new ArgumentNullException(nameof(station));
            if (craftingTimeSeconds < 0)
                throw new ArgumentException("Crafting time cannot be negative", nameof(craftingTimeSeconds));

            EntityId = entityId;
            RecipeId = recipeId;
            Station = station;
            CraftingTimeSeconds = craftingTimeSeconds;
        }
    }
}
