using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Default implementation of ICraftingStation.
    /// </summary>
    public class CraftingStation : ICraftingStation
    {
        private readonly List<string> _availableRecipes;

        /// <inheritdoc/>
        public string StationId { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public CraftingStationType StationType { get; }

        /// <inheritdoc/>
        public int Level { get; }

        private bool _isAvailable = true;

        /// <summary>
        /// Create a new crafting station.
        /// </summary>
        public CraftingStation(
            string stationId,
            string name,
            CraftingStationType stationType,
            int level,
            IEnumerable<string> availableRecipes = null)
        {
            if (string.IsNullOrEmpty(stationId))
                throw new ArgumentException("Station ID cannot be null or empty", nameof(stationId));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Station name cannot be null or empty", nameof(name));
            if (level < 0)
                throw new ArgumentException("Station level cannot be negative", nameof(level));

            StationId = stationId;
            Name = name;
            StationType = stationType;
            Level = level;
            _availableRecipes = new List<string>(availableRecipes ?? Enumerable.Empty<string>());
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetAvailableRecipes()
        {
            return _availableRecipes.AsReadOnly();
        }

        /// <inheritdoc/>
        public bool CanCraft(ICraftingRecipe recipe)
        {
            if (recipe == null)
                return false;

            // Check if station type matches
            if (recipe.StationType != StationType)
                return false;

            // Check if station level is sufficient
            if (Level < recipe.MinimumStationLevel)
                return false;

            // Check if recipe is available at this station
            if (!_availableRecipes.Contains(recipe.RecipeId))
                return false;

            return true;
        }

        /// <inheritdoc/>
        public bool IsAvailable()
        {
            return _isAvailable;
        }

        /// <summary>
        /// Set the availability of this station.
        /// </summary>
        public void SetAvailable(bool available)
        {
            _isAvailable = available;
        }

        /// <summary>
        /// Add a recipe to the available recipes list.
        /// </summary>
        public void AddRecipe(string recipeId)
        {
            if (!string.IsNullOrEmpty(recipeId) && !_availableRecipes.Contains(recipeId))
                _availableRecipes.Add(recipeId);
        }

        /// <summary>
        /// Remove a recipe from the available recipes list.
        /// </summary>
        public bool RemoveRecipe(string recipeId)
        {
            return _availableRecipes.Remove(recipeId);
        }
    }
}
