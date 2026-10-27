using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using RPGFramework.Crafting;
using RPGFramework.Tools.ItemDatabaseManager;

namespace RPGFramework.Tools.CraftingEditor
{
    /// <summary>
    /// A crafting recipe a designer fills in the inspector: what goes in, what comes out, where and how long.
    /// Ingredients and results point at item assets, so an item id can never be mistyped.
    /// Turns into the framework's <see cref="CraftingRecipeData"/> with <see cref="ToRecipeData"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCraftingRecipe", menuName = "RPG Framework/Crafting Recipe", order = 105)]
    public class CraftingRecipeAsset : ScriptableObject
    {
        [Header("Recipe Identity")]
        [Tooltip("Unique id the game uses to find this recipe, e.g. recipe_iron_bar")]
        public string recipeId = "recipe_new";

        [Tooltip("Name shown to the player")]
        public string recipeName = "New Recipe";

        [Tooltip("Text shown to the player about what this recipe makes")]
        [TextArea(2, 5)]
        public string description = "";

        [Header("Where and How Long")]
        [Tooltip("Kind of station the player must use, e.g. a forge")]
        public CraftingStationType stationType = CraftingStationType.Forge;

        [Tooltip("Lowest station level that can make this recipe (0 = any level)")]
        [Min(0)]
        public int minimumStationLevel = 0;

        [Tooltip("Seconds it takes to make (0 = instant)")]
        [Min(0f)]
        public float craftingTimeSeconds = 1f;

        [Header("Ingredients")]
        [Tooltip("Items used up to make this recipe")]
        public List<CraftingIngredientEntry> ingredients = new List<CraftingIngredientEntry>();

        [Header("Results")]
        [Tooltip("Items the player gets")]
        public List<CraftingResultEntry> results = new List<CraftingResultEntry>();

        [Header("Requirements")]
        [Tooltip("Extra conditions, e.g. a skill level or a tool; your game decides whether they are met")]
        public List<CraftingRequirementEntry> requirements = new List<CraftingRequirementEntry>();

        /// <summary>
        /// Turns this asset into the framework's recipe data.
        /// Throws <see cref="InvalidOperationException"/> listing the problems if <see cref="Validate()"/> finds any.
        /// </summary>
        public CraftingRecipeData ToRecipeData()
        {
            var errors = Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    $"Crafting recipe '{recipeId}' is not ready: {string.Join("; ", errors)}");

            CraftingIngredient[] ingredientData = FilledIngredients()
                .Select(i => new CraftingIngredient(i.item.itemId, i.quantity))
                .ToArray();

            CraftingResult[] resultData = FilledResults()
                .Select(r => new CraftingResult(r.item.itemId, r.quantity, r.successChance))
                .ToArray();

            CraftingRequirementData[] requirementData = (requirements ?? new List<CraftingRequirementEntry>())
                .Where(r => r != null)
                .Select(r => new CraftingRequirementData(r.type, r.parameterId, r.value))
                .ToArray();

            return new CraftingRecipeData(
                recipeId,
                recipeName,
                description,
                stationType,
                minimumStationLevel,
                craftingTimeSeconds,
                ingredientData,
                resultData,
                requirementData);
        }

        /// <summary>
        /// Turns this asset into a framework recipe ready to register.
        /// Throws <see cref="InvalidOperationException"/> if <see cref="Validate()"/> finds problems.
        /// </summary>
        public ICraftingRecipe ToRecipe()
        {
            return CraftingRecipe.FromData(ToRecipeData());
        }

        /// <summary>
        /// One line describing the recipe, e.g. "2 x Iron Ore + 1 x Coal → 1 x Iron Bar (3 s)".
        /// </summary>
        public string GetSummary()
        {
            string inputs = string.Join(" + ", (ingredients ?? new List<CraftingIngredientEntry>())
                .Where(i => i != null)
                .Select(i => $"{i.quantity} x {DisplayName(i.item)}"));

            string outputs = string.Join(" + ", (results ?? new List<CraftingResultEntry>())
                .Where(r => r != null)
                .Select(r => r.successChance < 100f
                    ? $"{r.quantity} x {DisplayName(r.item)} ({r.successChance.ToString("0.##", CultureInfo.InvariantCulture)}%)"
                    : $"{r.quantity} x {DisplayName(r.item)}"));

            if (inputs.Length == 0)
                inputs = "(nothing)";
            if (outputs.Length == 0)
                outputs = "(nothing)";

            return $"{inputs} → {outputs} ({craftingTimeSeconds.ToString("0.##", CultureInfo.InvariantCulture)} s)";
        }

        /// <summary>
        /// Lists every problem that would stop this recipe from working. An empty list means it is ready.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(recipeId))
                errors.Add("Recipe ID cannot be empty");

            if (string.IsNullOrWhiteSpace(recipeName))
                errors.Add("Recipe Name cannot be empty");

            if (minimumStationLevel < 0)
                errors.Add("Minimum Station Level cannot be negative");

            if (craftingTimeSeconds < 0f)
                errors.Add("Crafting Time cannot be negative");

            ValidateIngredients(errors);
            ValidateResults(errors);
            ValidateRequirements(errors);
            ValidateMakesSomething(errors);

            return errors;
        }

        /// <summary>
        /// Same as <see cref="Validate()"/>, and also reports ingredients and results whose item id is not in
        /// <paramref name="knownItemIds"/> (for example the ids in your item database).
        /// </summary>
        public List<string> Validate(ICollection<string> knownItemIds)
        {
            var errors = Validate();
            if (knownItemIds == null)
                return errors;

            foreach (var ingredient in FilledIngredients())
            {
                if (!knownItemIds.Contains(ingredient.item.itemId))
                    errors.Add($"Ingredient '{ingredient.item.itemId}' is not in the item database");
            }

            foreach (var result in FilledResults())
            {
                if (!knownItemIds.Contains(result.item.itemId))
                    errors.Add($"Result '{result.item.itemId}' is not in the item database");
            }

            return errors;
        }

        private void ValidateIngredients(List<string> errors)
        {
            if (ingredients == null || ingredients.Count == 0)
            {
                errors.Add("Recipe needs at least one ingredient");
                return;
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                var entry = ingredients[i];
                if (entry == null || entry.item == null)
                {
                    errors.Add($"Ingredient {i + 1} has no item");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.item.itemId))
                    errors.Add($"Ingredient {i + 1} uses an item with no Item ID");

                if (entry.quantity < 1)
                    errors.Add($"Ingredient {i + 1} ({DisplayName(entry.item)}) quantity must be at least 1");
            }

            foreach (var id in DuplicateIds(FilledIngredients().Select(i => i.item.itemId)))
                errors.Add($"Ingredient '{id}' is listed more than once; list it once with the total quantity");
        }

        private void ValidateResults(List<string> errors)
        {
            if (results == null || results.Count == 0)
            {
                errors.Add("Recipe needs at least one result");
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                var entry = results[i];
                if (entry == null || entry.item == null)
                {
                    errors.Add($"Result {i + 1} has no item");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.item.itemId))
                    errors.Add($"Result {i + 1} uses an item with no Item ID");

                if (entry.quantity < 1)
                    errors.Add($"Result {i + 1} ({DisplayName(entry.item)}) quantity must be at least 1");

                if (entry.successChance < 0f || entry.successChance > 100f)
                    errors.Add($"Result {i + 1} ({DisplayName(entry.item)}) success chance must be between 0 and 100");
                else if (entry.successChance == 0f)
                    errors.Add($"Result {i + 1} ({DisplayName(entry.item)}) has a 0% chance, so it is never made");
            }

            foreach (var id in DuplicateIds(FilledResults().Select(r => r.item.itemId)))
                errors.Add($"Result '{id}' is listed more than once; list it once with the total quantity");
        }

        private void ValidateRequirements(List<string> errors)
        {
            if (requirements == null)
                return;

            for (int i = 0; i < requirements.Count; i++)
            {
                var entry = requirements[i];
                if (entry == null)
                {
                    errors.Add($"Requirement {i + 1} is empty");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.parameterId))
                    errors.Add($"Requirement {i + 1} ({entry.type}) needs a Parameter ID, e.g. the skill or tool id");

                if (entry.value < 0)
                    errors.Add($"Requirement {i + 1} ({entry.type}) value cannot be negative");
            }
        }

        // A recipe whose every result is also an ingredient in the same or a larger amount only uses things up
        private void ValidateMakesSomething(List<string> errors)
        {
            var made = Totals(FilledResults().Select(r => (r.item.itemId, r.quantity)));
            if (made.Count == 0)
                return;

            var used = Totals(FilledIngredients().Select(i => (i.item.itemId, i.quantity)));
            bool makesNothingNew = made.All(m => used.TryGetValue(m.Key, out var usedCount) && usedCount >= m.Value);

            if (makesNothingNew)
                errors.Add("Recipe makes nothing new: every result is used up as an ingredient in the same or a larger amount");
        }

        private IEnumerable<CraftingIngredientEntry> FilledIngredients()
        {
            return (ingredients ?? new List<CraftingIngredientEntry>())
                .Where(i => i != null && i.item != null && !string.IsNullOrWhiteSpace(i.item.itemId));
        }

        private IEnumerable<CraftingResultEntry> FilledResults()
        {
            return (results ?? new List<CraftingResultEntry>())
                .Where(r => r != null && r.item != null && !string.IsNullOrWhiteSpace(r.item.itemId));
        }

        private static Dictionary<string, int> Totals(IEnumerable<(string itemId, int quantity)> entries)
        {
            var totals = new Dictionary<string, int>();
            foreach (var (itemId, quantity) in entries)
            {
                totals.TryGetValue(itemId, out var current);
                totals[itemId] = current + quantity;
            }
            return totals;
        }

        private static IEnumerable<string> DuplicateIds(IEnumerable<string> ids)
        {
            return ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key);
        }

        private static string DisplayName(ItemAsset item)
        {
            if (item == null)
                return "(empty)";
            return string.IsNullOrWhiteSpace(item.itemName) ? item.itemId : item.itemName;
        }
    }

    /// <summary>
    /// One ingredient slot of a crafting recipe: an item and how many are used up.
    /// </summary>
    [Serializable]
    public class CraftingIngredientEntry
    {
        [Tooltip("Item used up by the recipe")]
        public ItemAsset item;

        [Tooltip("How many of this item are used up")]
        [Min(1)]
        public int quantity = 1;
    }

    /// <summary>
    /// One result slot of a crafting recipe: an item, how many the player gets, and the chance to get it.
    /// </summary>
    [Serializable]
    public class CraftingResultEntry
    {
        [Tooltip("Item the player gets")]
        public ItemAsset item;

        [Tooltip("How many of this item the player gets")]
        [Min(1)]
        public int quantity = 1;

        [Tooltip("Chance in percent to get this item (100 = always)")]
        [Range(0f, 100f)]
        public float successChance = 100f;
    }

    /// <summary>
    /// One extra condition of a crafting recipe, such as a skill level or a tool the player must have.
    /// The framework only stores it; your game decides whether it is met.
    /// </summary>
    [Serializable]
    public class CraftingRequirementEntry
    {
        [Tooltip("Kind of condition: skill level, tool, station or finished quest")]
        public RequirementType type = RequirementType.SkillLevel;

        [Tooltip("What the condition is about: the skill id, tool item id, or quest id")]
        public string parameterId = "";

        [Tooltip("The number needed, e.g. the skill level (0 or more)")]
        [Min(0)]
        public int value = 1;
    }
}
