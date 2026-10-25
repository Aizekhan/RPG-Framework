using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Default implementation of ICraftingRecipeRegistry.
    /// </summary>
    public class CraftingRecipeRegistry : ICraftingRecipeRegistry
    {
        private readonly Dictionary<string, ICraftingRecipe> _recipes;

        /// <summary>
        /// Create a new crafting recipe registry.
        /// </summary>
        public CraftingRecipeRegistry()
        {
            _recipes = new Dictionary<string, ICraftingRecipe>();
        }

        /// <inheritdoc/>
        public ICraftingRecipe GetRecipe(string recipeId)
        {
            return _recipes.TryGetValue(recipeId, out var recipe) ? recipe : null;
        }

        /// <inheritdoc/>
        public bool HasRecipe(string recipeId)
        {
            return _recipes.ContainsKey(recipeId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<ICraftingRecipe> GetRecipesByStationType(CraftingStationType stationType)
        {
            return _recipes.Values
                .Where(recipe => recipe.StationType == stationType)
                .ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ICraftingRecipe> GetAllRecipes()
        {
            return _recipes.Values.ToList();
        }

        /// <inheritdoc/>
        public void RegisterRecipe(ICraftingRecipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (string.IsNullOrEmpty(recipe.RecipeId))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(recipe));

            _recipes[recipe.RecipeId] = recipe;
        }

        /// <inheritdoc/>
        public bool UnregisterRecipe(string recipeId)
        {
            return _recipes.Remove(recipeId);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _recipes.Clear();
        }
    }
}
