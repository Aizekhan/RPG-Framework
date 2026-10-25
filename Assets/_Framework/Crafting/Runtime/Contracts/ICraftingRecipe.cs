using System.Collections.Generic;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Interface for crafting recipe definitions.
    /// A recipe defines what ingredients are needed, what results are produced,
    /// and what requirements must be met to craft it.
    /// </summary>
    public interface ICraftingRecipe
    {
        /// <summary>
        /// Unique identifier for this recipe.
        /// Used for lookups in recipe registry and serialization.
        /// </summary>
        string RecipeId { get; }

        /// <summary>
        /// Display name of the recipe.
        /// Shown in UI and tooltips.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of what the recipe produces.
        /// Provides flavor text and mechanical information.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Type of crafting station required.
        /// </summary>
        CraftingStationType StationType { get; }

        /// <summary>
        /// Minimum level of the station needed to craft.
        /// </summary>
        int MinimumStationLevel { get; }

        /// <summary>
        /// Time to craft in seconds.
        /// </summary>
        float CraftingTimeSeconds { get; }

        /// <summary>
        /// Ingredients required for crafting.
        /// </summary>
        IReadOnlyList<ICraftingIngredient> Ingredients { get; }

        /// <summary>
        /// Results produced by crafting.
        /// </summary>
        IReadOnlyList<ICraftingResult> Results { get; }

        /// <summary>
        /// Requirements that must be met to craft this recipe.
        /// </summary>
        IReadOnlyList<ICraftingRequirement> Requirements { get; }

        /// <summary>
        /// Validate whether an entity can craft this recipe.
        /// </summary>
        /// <param name="entityId">Entity ID to validate</param>
        /// <returns>True if all requirements are met</returns>
        bool CanCraft(int entityId);
    }
}
