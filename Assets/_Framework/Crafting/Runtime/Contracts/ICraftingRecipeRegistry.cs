using System.Collections.Generic;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Registry for crafting recipes.
    /// Provides access to all available recipes in the game.
    /// </summary>
    public interface ICraftingRecipeRegistry
    {
        /// <summary>
        /// Get a recipe by ID.
        /// </summary>
        /// <param name="recipeId">Recipe ID to look up</param>
        /// <returns>Recipe or null if not found</returns>
        ICraftingRecipe GetRecipe(string recipeId);

        /// <summary>
        /// Check if a recipe exists.
        /// </summary>
        /// <param name="recipeId">Recipe ID to check</param>
        /// <returns>True if recipe exists</returns>
        bool HasRecipe(string recipeId);

        /// <summary>
        /// Get all recipes for a specific station type.
        /// </summary>
        /// <param name="stationType">Station type to filter by</param>
        /// <returns>List of recipes for that station type</returns>
        IReadOnlyList<ICraftingRecipe> GetRecipesByStationType(CraftingStationType stationType);

        /// <summary>
        /// Get all registered recipes.
        /// </summary>
        /// <returns>List of all recipes</returns>
        IReadOnlyList<ICraftingRecipe> GetAllRecipes();

        /// <summary>
        /// Register a new recipe.
        /// </summary>
        /// <param name="recipe">Recipe to register</param>
        void RegisterRecipe(ICraftingRecipe recipe);

        /// <summary>
        /// Unregister a recipe by ID.
        /// </summary>
        /// <param name="recipeId">Recipe ID to unregister</param>
        /// <returns>True if recipe was unregistered</returns>
        bool UnregisterRecipe(string recipeId);

        /// <summary>
        /// Clear all recipes from the registry.
        /// </summary>
        void Clear();
    }
}
