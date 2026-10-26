using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Default implementation of ICraftingRecipe.
    /// </summary>
    public class CraftingRecipe : ICraftingRecipe
    {
        /// <inheritdoc/>
        public string RecipeId { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <inheritdoc/>
        public CraftingStationType StationType { get; }

        /// <inheritdoc/>
        public int MinimumStationLevel { get; }

        /// <inheritdoc/>
        public float CraftingTimeSeconds { get; }

        /// <inheritdoc/>
        public IReadOnlyList<ICraftingIngredient> Ingredients { get; }

        /// <inheritdoc/>
        public IReadOnlyList<ICraftingResult> Results { get; }

        /// <inheritdoc/>
        public IReadOnlyList<ICraftingRequirement> Requirements { get; }

        /// <summary>
        /// Create a new crafting recipe.
        /// </summary>
        public CraftingRecipe(
            string recipeId,
            string name,
            string description,
            CraftingStationType stationType,
            int minimumStationLevel,
            float craftingTimeSeconds,
            IEnumerable<ICraftingIngredient> ingredients,
            IEnumerable<ICraftingResult> results,
            IEnumerable<ICraftingRequirement> requirements = null)
        {
            if (string.IsNullOrEmpty(recipeId))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(recipeId));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Recipe name cannot be null or empty", nameof(name));
            if (minimumStationLevel < 0)
                throw new ArgumentException("Minimum station level cannot be negative", nameof(minimumStationLevel));
            if (craftingTimeSeconds < 0)
                throw new ArgumentException("Crafting time cannot be negative", nameof(craftingTimeSeconds));
            if (ingredients == null)
                throw new ArgumentNullException(nameof(ingredients));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            var ingredientList = ingredients.ToList();
            var resultList = results.ToList();

            if (ingredientList.Count == 0)
                throw new ArgumentException("Recipe must have at least one ingredient", nameof(ingredients));
            if (resultList.Count == 0)
                throw new ArgumentException("Recipe must have at least one result", nameof(results));

            RecipeId = recipeId;
            Name = name;
            Description = description ?? string.Empty;
            StationType = stationType;
            MinimumStationLevel = minimumStationLevel;
            CraftingTimeSeconds = craftingTimeSeconds;
            Ingredients = ingredientList.AsReadOnly();
            Results = resultList.AsReadOnly();
            Requirements = (requirements?.ToList() ?? new List<ICraftingRequirement>()).AsReadOnly();
        }

        /// <inheritdoc/>
        public bool CanCraft(int entityId)
        {
            // Check if all requirements are satisfied
            return Requirements.All(req => req.IsSatisfied(entityId));
        }

        /// <summary>
        /// Create from serializable data.
        /// </summary>
        public static CraftingRecipe FromData(CraftingRecipeData data)
        {
            var ingredients = data.Ingredients
                .Select(ing => new CraftingIngredient(ing.ItemId, ing.Quantity))
                .Cast<ICraftingIngredient>()
                .ToList();

            var results = data.Results
                .Select(res => new CraftingResult(res.ItemId, res.Quantity, res.SuccessChance))
                .Cast<ICraftingResult>()
                .ToList();

            var requirements = data.Requirements
                .Select(req => new CraftingRequirement(req.Type, req.ParameterId, req.Value))
                .Cast<ICraftingRequirement>()
                .ToList();

            return new CraftingRecipe(
                data.Id,
                data.Name,
                data.Description,
                data.StationType,
                data.MinimumStationLevel,
                data.CraftingTimeSeconds,
                ingredients,
                results,
                requirements);
        }
    }
}
