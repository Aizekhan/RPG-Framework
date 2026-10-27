using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGFramework.Crafting;
using RPGFramework.Tools.ItemDatabaseManager;

namespace RPGFramework.Tools.CraftingEditor
{
    /// <summary>
    /// The list of all crafting recipes in the game. Checks them together (no two with the same id) and
    /// loads them into the framework's recipe registry.
    /// </summary>
    [CreateAssetMenu(fileName = "CraftingRecipeDatabase", menuName = "RPG Framework/Crafting Recipe Database", order = 106)]
    public class CraftingRecipeDatabase : ScriptableObject
    {
        [Tooltip("All crafting recipes in the game")]
        public List<CraftingRecipeAsset> recipes = new List<CraftingRecipeAsset>();

        [Tooltip("Optional: when set, every ingredient and result must be an item in this database")]
        public ItemDatabase itemDatabase;

        /// <summary>
        /// Finds a recipe by its id, or null if there is none.
        /// </summary>
        public CraftingRecipeAsset GetRecipe(string recipeId)
        {
            return recipes?.FirstOrDefault(r => r != null && r.recipeId == recipeId);
        }

        /// <summary>
        /// Puts every recipe into <paramref name="registry"/>. Empty slots are skipped.
        /// Throws <see cref="InvalidOperationException"/> if a recipe is not ready (see <see cref="Validate()"/>).
        /// </summary>
        public void RegisterInto(ICraftingRecipeRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            foreach (var recipe in (recipes ?? new List<CraftingRecipeAsset>()).Where(r => r != null))
                registry.RegisterRecipe(recipe.ToRecipe());
        }

        /// <summary>
        /// Makes a new framework recipe registry holding every recipe in this database.
        /// Throws <see cref="InvalidOperationException"/> if a recipe is not ready (see <see cref="Validate()"/>).
        /// </summary>
        public ICraftingRecipeRegistry ToRecipeRegistry()
        {
            var registry = new CraftingRecipeRegistry();
            RegisterInto(registry);
            return registry;
        }

        /// <summary>
        /// Lists every problem in the database and in its recipes. An empty list means it is ready.
        /// If <see cref="itemDatabase"/> is set, also reports items that are not in it.
        /// </summary>
        public List<string> Validate()
        {
            ICollection<string> knownItemIds = null;
            if (itemDatabase != null)
            {
                knownItemIds = new HashSet<string>((itemDatabase.items ?? new List<ItemAsset>())
                    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.itemId))
                    .Select(i => i.itemId));
            }

            return Validate(knownItemIds);
        }

        /// <summary>
        /// Lists every problem in the database and in its recipes, and reports ingredients and results whose
        /// item id is not in <paramref name="knownItemIds"/> (pass null to skip that check).
        /// </summary>
        public List<string> Validate(ICollection<string> knownItemIds)
        {
            var errors = new List<string>();

            if (recipes == null || recipes.Count == 0)
            {
                errors.Add("Database is empty - no recipes added");
                return errors;
            }

            int emptySlots = recipes.Count(r => r == null);
            if (emptySlots > 0)
                errors.Add($"{emptySlots} empty recipe slot(s) in the database");

            var duplicateIds = recipes
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.recipeId))
                .GroupBy(r => r.recipeId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
                errors.Add($"Duplicate recipe IDs: {string.Join(", ", duplicateIds)}");

            foreach (var recipe in recipes.Where(r => r != null))
            {
                var recipeErrors = recipe.Validate(knownItemIds);
                if (recipeErrors.Count > 0)
                {
                    errors.Add($"Recipe '{recipe.recipeName}' ({recipe.recipeId}):");
                    errors.AddRange(recipeErrors.Select(e => $"  - {e}"));
                }
            }

            return errors;
        }
    }
}
