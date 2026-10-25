using System;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Serializable crafting recipe definition data.
    /// </summary>
    [Serializable]
    public struct CraftingRecipeData
    {
        /// <summary>
        /// Unique identifier for this recipe.
        /// </summary>
        public string Id;

        /// <summary>
        /// Display name of the recipe.
        /// </summary>
        public string Name;

        /// <summary>
        /// Description of what the recipe produces.
        /// </summary>
        public string Description;

        /// <summary>
        /// Type of crafting station required.
        /// </summary>
        public CraftingStationType StationType;

        /// <summary>
        /// Minimum level of the station needed.
        /// </summary>
        public int MinimumStationLevel;

        /// <summary>
        /// Time to craft in seconds.
        /// </summary>
        public float CraftingTimeSeconds;

        /// <summary>
        /// Ingredients required for crafting.
        /// </summary>
        public CraftingIngredient[] Ingredients;

        /// <summary>
        /// Results produced by crafting.
        /// </summary>
        public CraftingResult[] Results;

        /// <summary>
        /// Requirements to craft this recipe.
        /// </summary>
        public CraftingRequirementData[] Requirements;

        /// <summary>
        /// Create a new crafting recipe data.
        /// </summary>
        public CraftingRecipeData(
            string id,
            string name,
            string description,
            CraftingStationType stationType,
            int minimumStationLevel,
            float craftingTimeSeconds,
            CraftingIngredient[] ingredients,
            CraftingResult[] results,
            CraftingRequirementData[] requirements)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Recipe ID cannot be null or empty", nameof(id));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Recipe name cannot be null or empty", nameof(name));
            if (minimumStationLevel < 0)
                throw new ArgumentException("Minimum station level cannot be negative", nameof(minimumStationLevel));
            if (craftingTimeSeconds < 0)
                throw new ArgumentException("Crafting time cannot be negative", nameof(craftingTimeSeconds));
            if (ingredients == null || ingredients.Length == 0)
                throw new ArgumentException("Recipe must have at least one ingredient", nameof(ingredients));
            if (results == null || results.Length == 0)
                throw new ArgumentException("Recipe must have at least one result", nameof(results));

            Id = id;
            Name = name;
            Description = description ?? string.Empty;
            StationType = stationType;
            MinimumStationLevel = minimumStationLevel;
            CraftingTimeSeconds = craftingTimeSeconds;
            Ingredients = ingredients;
            Results = results;
            Requirements = requirements ?? Array.Empty<CraftingRequirementData>();
        }
    }
}
