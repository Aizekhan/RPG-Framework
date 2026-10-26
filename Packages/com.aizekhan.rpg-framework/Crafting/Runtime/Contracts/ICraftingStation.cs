using System.Collections.Generic;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Interface for crafting stations.
    /// A station is a physical location where crafting can occur.
    /// </summary>
    public interface ICraftingStation
    {
        /// <summary>
        /// Unique identifier for this station.
        /// </summary>
        string StationId { get; }

        /// <summary>
        /// Display name of the station.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Type of crafting this station performs.
        /// </summary>
        CraftingStationType StationType { get; }

        /// <summary>
        /// Level of the station (higher level = can craft better recipes).
        /// </summary>
        int Level { get; }

        /// <summary>
        /// Get all recipes available at this station.
        /// </summary>
        /// <returns>List of recipe IDs available</returns>
        IReadOnlyList<string> GetAvailableRecipes();

        /// <summary>
        /// Check if a recipe can be crafted at this station.
        /// </summary>
        /// <param name="recipe">Recipe to check</param>
        /// <returns>True if recipe can be crafted here</returns>
        bool CanCraft(ICraftingRecipe recipe);

        /// <summary>
        /// Check if this station is currently available for crafting.
        /// </summary>
        /// <returns>True if station is accessible</returns>
        bool IsAvailable();
    }
}
