using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Crafting;
using RPGFramework.ECS;
using RPGFramework.Tools.ItemDatabaseManager;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.Tools.CraftingEditor.Tests
{
    /// <summary>
    /// A crafting recipe made as an asset becomes exactly the recipe the framework crafts with, and its checks catch
    /// what would break a game: a missing id, nothing going in or coming out, an empty slot, a bad amount, or a recipe
    /// that only uses up what it makes.
    /// </summary>
    public sealed class CraftingRecipeAssetTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _made)
                Object.DestroyImmediate(asset);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _made.Add(asset);
            return asset;
        }

        private ItemAsset Item(string id, string displayName)
        {
            var item = Make<ItemAsset>();
            item.itemId = id;
            item.itemName = displayName;
            return item;
        }

        private static CraftingIngredientEntry In(ItemAsset item, int quantity)
        {
            return new CraftingIngredientEntry { item = item, quantity = quantity };
        }

        private static CraftingResultEntry Out(ItemAsset item, int quantity, float successChance = 100f)
        {
            return new CraftingResultEntry { item = item, quantity = quantity, successChance = successChance };
        }

        // 2 x Iron Ore + 1 x Coal -> 1 x Iron Bar at a forge, 3 seconds
        private CraftingRecipeAsset IronBar(string id = "recipe_iron_bar")
        {
            var recipe = Make<CraftingRecipeAsset>();
            recipe.recipeId = id;
            recipe.recipeName = "Iron Bar";
            recipe.description = "Smelt ore into a bar";
            recipe.stationType = CraftingStationType.Forge;
            recipe.minimumStationLevel = 2;
            recipe.craftingTimeSeconds = 3f;
            recipe.ingredients = new List<CraftingIngredientEntry>
            {
                In(Item("iron_ore", "Iron Ore"), 2),
                In(Item("coal", "Coal"), 1)
            };
            recipe.results = new List<CraftingResultEntry> { Out(Item("iron_bar", "Iron Bar"), 1) };
            recipe.requirements = new List<CraftingRequirementEntry>
            {
                new CraftingRequirementEntry { type = RequirementType.SkillLevel, parameterId = "smithing", value = 5 }
            };
            return recipe;
        }

        private sealed class SmithingSkill : ICraftingRequirementChecker
        {
            private readonly int _level;

            public SmithingSkill(int level) => _level = level;

            public bool IsSatisfied(int entityId, ICraftingRequirement requirement) =>
                requirement.Type == RequirementType.SkillLevel && requirement.ParameterId == "smithing" && _level >= requirement.Value;
        }

        private static void AssertHasError(List<string> errors, string fragment)
        {
            Assert.That(errors.Any(e => e.Contains(fragment)), Is.True,
                $"Expected an error containing '{fragment}', got: {string.Join(" | ", errors)}");
        }

        // ---- A good recipe ----

        [Test]
        public void AGoodRecipe_HasNoErrors_AndStartsCraftingAtAMatchingStation()
        {
            var recipe = IronBar();
            Assert.That(recipe.Validate(), Is.Empty);

            var database = Make<CraftingRecipeDatabase>();
            database.recipes = new List<CraftingRecipeAsset> { recipe };
            Assert.That(database.Validate(), Is.Empty);

            var entities = new EntityManager();
            var smith = entities.CreateEntity();
            var registry = database.ToRecipeRegistry();
            var forge = new CraftingStation("forge_1", "Forge", CraftingStationType.Forge, 2, new[] { "recipe_iron_bar" });
            Assert.That(new CraftingManager(entities, registry).StartCrafting(smith, "recipe_iron_bar", forge), Is.EqualTo(-1),
                "the smithing requirement is never taken as met unchecked");
            var manager = new CraftingManager(entities, registry, requirements: new SmithingSkill(5));

            CraftingStartedEvent started = null;
            manager.CraftingStarted += (_, e) => started = e;

            var operationId = manager.StartCrafting(smith, "recipe_iron_bar", forge);

            Assert.That(operationId, Is.GreaterThan(0));
            Assert.That(started, Is.Not.Null);
            Assert.That(started.RecipeId, Is.EqualTo("recipe_iron_bar"));
            Assert.That(started.CraftingTimeSeconds, Is.EqualTo(3f));
            Assert.That(manager.IsCrafting(smith), Is.True);
            var active = manager.GetActiveCrafts(smith).Single();
            Assert.That(active.RecipeId, Is.EqualTo("recipe_iron_bar"));
            Assert.That(active.RequiredTimeSeconds, Is.EqualTo(3f));

            var registered = registry.GetRecipe("recipe_iron_bar");
            Assert.That(registered.Ingredients.Select(i => $"{i.Quantity} {i.ItemId}"),
                Is.EqualTo(new[] { "2 iron_ore", "1 coal" }));
            Assert.That(registered.Results.Select(r => $"{r.Quantity} {r.ItemId}"), Is.EqualTo(new[] { "1 iron_bar" }));
        }

        [Test]
        public void AStationTooLowLevel_CannotStartTheConvertedRecipe()
        {
            var database = Make<CraftingRecipeDatabase>();
            database.recipes = new List<CraftingRecipeAsset> { IronBar() };

            var entities = new EntityManager();
            var smith = entities.CreateEntity();
            var manager = new CraftingManager(entities, database.ToRecipeRegistry());
            var smallForge = new CraftingStation("forge_0", "Small Forge", CraftingStationType.Forge, 1, new[] { "recipe_iron_bar" });

            Assert.That(manager.StartCrafting(smith, "recipe_iron_bar", smallForge), Is.EqualTo(-1));
        }

        [Test]
        public void ConvertedData_CarriesEveryField()
        {
            var data = IronBar().ToRecipeData();

            Assert.That(data.Id, Is.EqualTo("recipe_iron_bar"));
            Assert.That(data.Name, Is.EqualTo("Iron Bar"));
            Assert.That(data.Description, Is.EqualTo("Smelt ore into a bar"));
            Assert.That(data.StationType, Is.EqualTo(CraftingStationType.Forge));
            Assert.That(data.MinimumStationLevel, Is.EqualTo(2));
            Assert.That(data.CraftingTimeSeconds, Is.EqualTo(3f));
            Assert.That(data.Ingredients.Length, Is.EqualTo(2));
            Assert.That(data.Results.Single().ItemId, Is.EqualTo("iron_bar"));
            Assert.That(data.Results.Single().SuccessChance, Is.EqualTo(100f));
            var requirement = data.Requirements.Single();
            Assert.That(requirement.Type, Is.EqualTo(RequirementType.SkillLevel));
            Assert.That(requirement.ParameterId, Is.EqualTo("smithing"));
            Assert.That(requirement.Value, Is.EqualTo(5));
        }

        [Test]
        public void ConvertingABrokenRecipe_ThrowsAndNamesTheProblem()
        {
            var recipe = IronBar();
            recipe.results.Clear();

            var error = Assert.Throws<InvalidOperationException>(() => recipe.ToRecipeData());
            Assert.That(error.Message, Does.Contain("at least one result"));
        }

        [Test]
        public void Summary_ReadsLikeTheRecipe()
        {
            Assert.That(IronBar().GetSummary(), Is.EqualTo("2 x Iron Ore + 1 x Coal → 1 x Iron Bar (3 s)"));
        }

        [Test]
        public void Summary_ShowsChanceBelowHundred_AndEmptySlots()
        {
            var recipe = IronBar();
            recipe.results[0].successChance = 75f;
            recipe.ingredients.Add(new CraftingIngredientEntry());

            Assert.That(recipe.GetSummary(),
                Is.EqualTo("2 x Iron Ore + 1 x Coal + 1 x (empty) → 1 x Iron Bar (75%) (3 s)"));
        }

        // ---- Recipe checks ----

        [Test]
        public void EmptyRecipeId_IsReported()
        {
            var recipe = IronBar();
            recipe.recipeId = " ";
            AssertHasError(recipe.Validate(), "Recipe ID cannot be empty");
        }

        [Test]
        public void EmptyRecipeName_IsReported()
        {
            var recipe = IronBar();
            recipe.recipeName = "";
            AssertHasError(recipe.Validate(), "Recipe Name cannot be empty");
        }

        [Test]
        public void NegativeStationLevel_IsReported()
        {
            var recipe = IronBar();
            recipe.minimumStationLevel = -1;
            AssertHasError(recipe.Validate(), "Minimum Station Level cannot be negative");
        }

        [Test]
        public void NegativeCraftingTime_IsReported()
        {
            var recipe = IronBar();
            recipe.craftingTimeSeconds = -0.5f;
            AssertHasError(recipe.Validate(), "Crafting Time cannot be negative");
        }

        [Test]
        public void NoIngredients_IsReported()
        {
            var recipe = IronBar();
            recipe.ingredients.Clear();
            AssertHasError(recipe.Validate(), "at least one ingredient");
        }

        [Test]
        public void NoResults_IsReported()
        {
            var recipe = IronBar();
            recipe.results = null;
            AssertHasError(recipe.Validate(), "at least one result");
        }

        [Test]
        public void EmptyIngredientSlot_IsReported_WhetherTheSlotOrItsItemIsMissing()
        {
            var recipe = IronBar();
            recipe.ingredients.Add(null);
            recipe.ingredients.Add(new CraftingIngredientEntry { quantity = 1 });

            var errors = recipe.Validate();

            AssertHasError(errors, "Ingredient 3 has no item");
            AssertHasError(errors, "Ingredient 4 has no item");
        }

        [Test]
        public void EmptyResultSlot_IsReported()
        {
            var recipe = IronBar();
            recipe.results.Add(new CraftingResultEntry());
            AssertHasError(recipe.Validate(), "Result 2 has no item");
        }

        [Test]
        public void ItemWithoutId_IsReported_ForIngredientsAndResults()
        {
            var recipe = IronBar();
            recipe.ingredients[0].item.itemId = "";
            recipe.results[0].item.itemId = "";

            var errors = recipe.Validate();

            AssertHasError(errors, "Ingredient 1 uses an item with no Item ID");
            AssertHasError(errors, "Result 1 uses an item with no Item ID");
        }

        [Test]
        public void QuantityBelowOne_IsReported_ForIngredientsAndResults()
        {
            var recipe = IronBar();
            recipe.ingredients[1].quantity = 0;
            recipe.results[0].quantity = -2;

            var errors = recipe.Validate();

            AssertHasError(errors, "Ingredient 2 (Coal) quantity must be at least 1");
            AssertHasError(errors, "Result 1 (Iron Bar) quantity must be at least 1");
        }

        [Test]
        public void SuccessChanceOutsideZeroToHundred_IsReported()
        {
            var recipe = IronBar();
            recipe.results[0].successChance = 150f;
            AssertHasError(recipe.Validate(), "success chance must be between 0 and 100");
        }

        [Test]
        public void ZeroSuccessChance_IsReported_BecauseItIsNeverMade()
        {
            var recipe = IronBar();
            recipe.results[0].successChance = 0f;
            AssertHasError(recipe.Validate(), "0% chance");
        }

        [Test]
        public void SameItemListedTwice_IsReported()
        {
            var recipe = IronBar();
            recipe.ingredients.Add(In(recipe.ingredients[0].item, 3));
            recipe.results.Add(Out(recipe.results[0].item, 1));

            var errors = recipe.Validate();

            AssertHasError(errors, "Ingredient 'iron_ore' is listed more than once");
            AssertHasError(errors, "Result 'iron_bar' is listed more than once");
        }

        [Test]
        public void RecipeThatOnlyUsesUpWhatItMakes_IsReported()
        {
            var recipe = IronBar();
            recipe.ingredients.Add(In(recipe.results[0].item, 1));
            AssertHasError(recipe.Validate(), "makes nothing new");
        }

        [Test]
        public void RecipeThatUsesSomeOfItsResultButMakesMore_IsFine()
        {
            var recipe = IronBar();
            recipe.ingredients.Add(In(recipe.results[0].item, 1));
            recipe.results[0].quantity = 3;
            Assert.That(recipe.Validate(), Is.Empty);
        }

        [Test]
        public void RequirementWithoutParameterId_IsReported()
        {
            var recipe = IronBar();
            recipe.requirements[0].parameterId = "";
            AssertHasError(recipe.Validate(), "Requirement 1 (SkillLevel) needs a Parameter ID");
        }

        [Test]
        public void RequirementWithNegativeValue_IsReported()
        {
            var recipe = IronBar();
            recipe.requirements[0].value = -1;
            AssertHasError(recipe.Validate(), "Requirement 1 (SkillLevel) value cannot be negative");
        }

        [Test]
        public void EmptyRequirementSlot_IsReported()
        {
            var recipe = IronBar();
            recipe.requirements.Add(null);
            AssertHasError(recipe.Validate(), "Requirement 2 is empty");
        }

        [Test]
        public void UnknownItems_AreReported_WhenKnownItemIdsAreGiven()
        {
            var recipe = IronBar();

            var errors = recipe.Validate(new HashSet<string> { "iron_ore" });

            AssertHasError(errors, "Ingredient 'coal' is not in the item database");
            AssertHasError(errors, "Result 'iron_bar' is not in the item database");
            Assert.That(errors.Any(e => e.Contains("iron_ore")), Is.False);
        }

        // ---- Database checks ----

        [Test]
        public void EmptyDatabase_IsReported()
        {
            var database = Make<CraftingRecipeDatabase>();
            AssertHasError(database.Validate(), "Database is empty");
        }

        [Test]
        public void EmptyDatabaseSlot_IsReported_AndSkippedWhenRegistering()
        {
            var database = Make<CraftingRecipeDatabase>();
            database.recipes = new List<CraftingRecipeAsset> { IronBar(), null };

            AssertHasError(database.Validate(), "1 empty recipe slot(s)");
            Assert.That(database.ToRecipeRegistry().GetAllRecipes().Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateRecipeIds_AreReported()
        {
            var database = Make<CraftingRecipeDatabase>();
            database.recipes = new List<CraftingRecipeAsset> { IronBar("recipe_bar"), IronBar("recipe_bar") };
            AssertHasError(database.Validate(), "Duplicate recipe IDs: recipe_bar");
        }

        [Test]
        public void ABrokenRecipe_IsReportedUnderItsName()
        {
            var broken = IronBar("recipe_broken");
            broken.ingredients.Clear();
            var database = Make<CraftingRecipeDatabase>();
            database.recipes = new List<CraftingRecipeAsset> { IronBar(), broken };

            var errors = database.Validate();

            AssertHasError(errors, "Recipe 'Iron Bar' (recipe_broken):");
            AssertHasError(errors, "  - Recipe needs at least one ingredient");
        }

        [Test]
        public void ItemsMissingFromTheLinkedItemDatabase_AreReported()
        {
            var recipe = IronBar();
            var items = Make<ItemDatabase>();
            items.items = new List<ItemAsset> { recipe.ingredients[0].item, recipe.ingredients[1].item };
            var database = Make<CraftingRecipeDatabase>();
            database.recipes = new List<CraftingRecipeAsset> { recipe };
            database.itemDatabase = items;

            var errors = database.Validate();
            AssertHasError(errors, "Result 'iron_bar' is not in the item database");

            items.items.Add(recipe.results[0].item);
            Assert.That(database.Validate(), Is.Empty);
        }

        [Test]
        public void RegisterInto_PutsEveryRecipeInTheRegistry()
        {
            var database = Make<CraftingRecipeDatabase>();
            database.recipes = new List<CraftingRecipeAsset> { IronBar("recipe_a"), IronBar("recipe_b") };
            var registry = new CraftingRecipeRegistry();

            database.RegisterInto(registry);

            Assert.That(registry.HasRecipe("recipe_a"), Is.True);
            Assert.That(registry.HasRecipe("recipe_b"), Is.True);
            Assert.That(database.GetRecipe("recipe_b"), Is.SameAs(database.recipes[1]));
        }
    }
}
