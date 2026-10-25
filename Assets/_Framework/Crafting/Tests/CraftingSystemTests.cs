using NUnit.Framework;
using RPGFramework.Crafting;
using RPGFramework.ECS;
using System;
using System.Collections.Generic;
// using Moq; // TODO: Add Moq package for mocking tests

namespace RPGFramework.Crafting.Tests
{
    /// <summary>
    /// Comprehensive test suite for RPG Framework Crafting System.
    /// Tests recipes, stations, requirements, registry, and manager functionality.
    /// Covers 25+ test cases for complete Crafting System coverage.
    /// </summary>
    [TestFixture]
    public class CraftingSystemTests
    {
        #region Helper Methods

        /// <summary>
        /// Create a mock IEntityManager for ECS testing.
        /// </summary>
        // TODO: Uncomment when Moq package is added
        // private Mock<IEntityManager> CreateMockEntityManager()
        // {
        //     var mock = new Mock<IEntityManager>();
        //     return mock;
        // }

        /// <summary>
        /// Create a simple test ingredient.
        /// </summary>
        private ICraftingIngredient CreateTestIngredient(
            string itemId = "iron_ore",
            int quantity = 5)
        {
            return new CraftingIngredient(itemId, quantity);
        }

        /// <summary>
        /// Create a simple test result.
        /// </summary>
        private ICraftingResult CreateTestResult(
            string itemId = "iron_sword",
            int quantity = 1,
            float successChance = 100f)
        {
            return new CraftingResult(itemId, quantity, successChance);
        }

        /// <summary>
        /// Create a simple test requirement.
        /// </summary>
        private ICraftingRequirement CreateTestRequirement(
            RequirementType type = RequirementType.SkillLevel,
            string parameterId = "blacksmithing",
            int value = 10)
        {
            return new CraftingRequirement(type, parameterId, value);
        }

        /// <summary>
        /// Create a simple test recipe.
        /// </summary>
        private ICraftingRecipe CreateTestRecipe(
            string id = "recipe_iron_sword",
            string name = "Iron Sword",
            string description = "A simple iron sword",
            CraftingStationType stationType = CraftingStationType.Forge,
            int minimumStationLevel = 1,
            float craftingTimeSeconds = 30f,
            IEnumerable<ICraftingIngredient> ingredients = null,
            IEnumerable<ICraftingResult> results = null,
            IEnumerable<ICraftingRequirement> requirements = null)
        {
            ingredients = ingredients ?? new List<ICraftingIngredient> { CreateTestIngredient() };
            results = results ?? new List<ICraftingResult> { CreateTestResult() };
            requirements = requirements ?? new List<ICraftingRequirement>();

            return new CraftingRecipe(id, name, description, stationType, minimumStationLevel,
                craftingTimeSeconds, ingredients, results, requirements);
        }

        /// <summary>
        /// Create a simple test station.
        /// </summary>
        private ICraftingStation CreateTestStation(
            string stationId = "forge_001",
            string name = "Blacksmith Forge",
            CraftingStationType stationType = CraftingStationType.Forge,
            int level = 5)
        {
            return new CraftingStation(stationId, name, stationType, level);
        }

        #endregion

        #region Ingredient Tests (3 tests)

        [Test]
        public void CreateIngredient_WithValidParameters_SetsPropertiesCorrectly()
        {
            var ingredient = CreateTestIngredient("copper_ore", 10);
            Assert.AreEqual("copper_ore", ingredient.ItemId);
            Assert.AreEqual(10, ingredient.Quantity);
        }

        [Test]
        public void CreateIngredient_WithNullItemId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new CraftingIngredient(null, 5));
            Assert.Throws<ArgumentException>(() => new CraftingIngredient("", 5));
        }

        [Test]
        public void CreateIngredient_WithZeroQuantity_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new CraftingIngredient("iron_ore", 0));
            Assert.Throws<ArgumentException>(() => new CraftingIngredient("iron_ore", -5));
        }

        #endregion

        #region Result Tests (3 tests)

        [Test]
        public void CreateResult_WithValidParameters_SetsPropertiesCorrectly()
        {
            var result = CreateTestResult("iron_sword", 1, 95f);
            Assert.AreEqual("iron_sword", result.ItemId);
            Assert.AreEqual(1, result.Quantity);
            Assert.AreEqual(95f, result.SuccessChance);
        }

        [Test]
        public void CreateResult_WithDefaultSuccessChance_Sets100Percent()
        {
            var result = new CraftingResult("iron_sword", 1);
            Assert.AreEqual(100f, result.SuccessChance);
        }

        [Test]
        public void CreateResult_WithInvalidSuccessChance_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new CraftingResult("item", 1, -10f));
            Assert.Throws<ArgumentException>(() => new CraftingResult("item", 1, 150f));
        }

        #endregion

        #region Requirement Tests (3 tests)

        [Test]
        public void CreateRequirement_WithValidParameters_SetsPropertiesCorrectly()
        {
            var requirement = CreateTestRequirement(RequirementType.ToolRequired, "hammer", 1);
            Assert.AreEqual(RequirementType.ToolRequired, requirement.Type);
            Assert.AreEqual("hammer", requirement.ParameterId);
            Assert.AreEqual(1, requirement.Value);
        }

        [Test]
        public void CreateRequirement_WithNullParameterId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new CraftingRequirement(RequirementType.SkillLevel, null, 5));
            Assert.Throws<ArgumentException>(() => new CraftingRequirement(RequirementType.SkillLevel, "", 5));
        }

        [Test]
        public void Requirement_IsSatisfied_ReturnsTrue()
        {
            var requirement = CreateTestRequirement();
            Assert.IsTrue(requirement.IsSatisfied(1));
        }

        #endregion

        #region Recipe Tests (6 tests)

        [Test]
        public void CreateRecipe_WithValidParameters_SetsPropertiesCorrectly()
        {
            var recipe = CreateTestRecipe();
            Assert.AreEqual("recipe_iron_sword", recipe.RecipeId);
            Assert.AreEqual("Iron Sword", recipe.Name);
            Assert.AreEqual(CraftingStationType.Forge, recipe.StationType);
            Assert.AreEqual(30f, recipe.CraftingTimeSeconds);
        }

        [Test]
        public void CreateRecipe_WithNullId_ThrowsArgumentException()
        {
            var ingredients = new List<ICraftingIngredient> { CreateTestIngredient() };
            var results = new List<ICraftingResult> { CreateTestResult() };
            Assert.Throws<ArgumentException>(() =>
                new CraftingRecipe(null, "Name", "Desc", CraftingStationType.Forge, 1, 30, ingredients, results));
        }

        [Test]
        public void CreateRecipe_WithNoIngredients_ThrowsArgumentException()
        {
            var results = new List<ICraftingResult> { CreateTestResult() };
            Assert.Throws<ArgumentException>(() =>
                new CraftingRecipe("id", "Name", "Desc", CraftingStationType.Forge, 1, 30,
                    new List<ICraftingIngredient>(), results));
        }

        [Test]
        public void CreateRecipe_WithNoResults_ThrowsArgumentException()
        {
            var ingredients = new List<ICraftingIngredient> { CreateTestIngredient() };
            Assert.Throws<ArgumentException>(() =>
                new CraftingRecipe("id", "Name", "Desc", CraftingStationType.Forge, 1, 30,
                    ingredients, new List<ICraftingResult>()));
        }

        [Test]
        public void Recipe_CanCraft_WithNoRequirements_ReturnsTrue()
        {
            var recipe = CreateTestRecipe();
            Assert.IsTrue(recipe.CanCraft(1));
        }

        [Test]
        public void Recipe_FromData_CreatesRecipeCorrectly()
        {
            var data = new CraftingRecipeData(
                "recipe_test",
                "Test Recipe",
                "A test recipe",
                CraftingStationType.Alchemy,
                2,
                45f,
                new[] { new CraftingIngredient("herb", 3) },
                new[] { new CraftingResult("potion", 1) },
                new[] { new CraftingRequirementData(RequirementType.SkillLevel, "alchemy", 15) });

            var recipe = CraftingRecipe.FromData(data);
            Assert.AreEqual("recipe_test", recipe.RecipeId);
            Assert.AreEqual("Test Recipe", recipe.Name);
            Assert.AreEqual(1, recipe.Ingredients.Count);
            Assert.AreEqual(1, recipe.Results.Count);
        }

        #endregion

        #region Station Tests (5 tests)

        [Test]
        public void CreateStation_WithValidParameters_SetsPropertiesCorrectly()
        {
            var station = CreateTestStation();
            Assert.AreEqual("forge_001", station.StationId);
            Assert.AreEqual("Blacksmith Forge", station.Name);
            Assert.AreEqual(CraftingStationType.Forge, station.StationType);
            Assert.AreEqual(5, station.Level);
        }

        [Test]
        public void CreateStation_WithNullStationId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new CraftingStation(null, "Name", CraftingStationType.Forge, 1));
        }

        [Test]
        public void Station_CanCraft_WithMatchingTypeAndLevel_ReturnsTrue()
        {
            var station = CreateTestStation("forge_1", "Forge", CraftingStationType.Forge, 5);
            var recipe = CreateTestRecipe(stationType: CraftingStationType.Forge, minimumStationLevel: 3);
            ((CraftingStation)station).AddRecipe(recipe.RecipeId);

            Assert.IsTrue(station.CanCraft(recipe));
        }

        [Test]
        public void Station_CanCraft_WithInsufficientLevel_ReturnsFalse()
        {
            var station = CreateTestStation("forge_1", "Forge", CraftingStationType.Forge, 2);
            var recipe = CreateTestRecipe(stationType: CraftingStationType.Forge, minimumStationLevel: 5);

            Assert.IsFalse(station.CanCraft(recipe));
        }

        [Test]
        public void Station_CanCraft_WithWrongType_ReturnsFalse()
        {
            var station = CreateTestStation("forge_1", "Forge", CraftingStationType.Forge, 5);
            var recipe = CreateTestRecipe(stationType: CraftingStationType.Alchemy);

            Assert.IsFalse(station.CanCraft(recipe));
        }

        #endregion

        #region Registry Tests (5 tests)

        [Test]
        public void CreateRegistry_InitializesEmpty()
        {
            var registry = new CraftingRecipeRegistry();
            Assert.AreEqual(0, registry.GetAllRecipes().Count);
        }

        [Test]
        public void Registry_RegisterRecipe_StoresRecipe()
        {
            var registry = new CraftingRecipeRegistry();
            var recipe = CreateTestRecipe();
            registry.RegisterRecipe(recipe);

            Assert.IsTrue(registry.HasRecipe(recipe.RecipeId));
            Assert.AreEqual(recipe.RecipeId, registry.GetRecipe(recipe.RecipeId).RecipeId);
        }

        [Test]
        public void Registry_UnregisterRecipe_RemovesRecipe()
        {
            var registry = new CraftingRecipeRegistry();
            var recipe = CreateTestRecipe();
            registry.RegisterRecipe(recipe);
            registry.UnregisterRecipe(recipe.RecipeId);

            Assert.IsFalse(registry.HasRecipe(recipe.RecipeId));
        }

        [Test]
        public void Registry_GetRecipesByStationType_FiltersCorrectly()
        {
            var registry = new CraftingRecipeRegistry();
            var forgeRecipe = CreateTestRecipe("forge_recipe", stationType: CraftingStationType.Forge);
            var alchemyRecipe = CreateTestRecipe("alchemy_recipe", stationType: CraftingStationType.Alchemy);

            registry.RegisterRecipe(forgeRecipe);
            registry.RegisterRecipe(alchemyRecipe);

            var forgeRecipes = registry.GetRecipesByStationType(CraftingStationType.Forge);
            Assert.AreEqual(1, forgeRecipes.Count);
            Assert.AreEqual(CraftingStationType.Forge, forgeRecipes[0].StationType);
        }

        [Test]
        public void Registry_Clear_RemovesAllRecipes()
        {
            var registry = new CraftingRecipeRegistry();
            registry.RegisterRecipe(CreateTestRecipe("recipe1"));
            registry.RegisterRecipe(CreateTestRecipe("recipe2"));
            registry.Clear();

            Assert.AreEqual(0, registry.GetAllRecipes().Count);
        }

        #endregion

        #region Manager Tests (4 tests)

        [Test]
        public void CreateManager_WithNullEntityManager_ThrowsArgumentNullException()
        {
            var registry = new CraftingRecipeRegistry();
            Assert.Throws<ArgumentNullException>(() => new CraftingManager(null, registry));
        }

        [Test]
        public void CreateManager_WithNullRegistry_ThrowsArgumentNullException()
        {
            var entityManager = CreateMockEntityManager();
            Assert.Throws<ArgumentNullException>(() => new CraftingManager(entityManager.Object, null));
        }

        [Test]
        public void Manager_StartCrafting_WithValidParameters_RaisesEvent()
        {
            var entityManager = CreateMockEntityManager();
            entityManager
                .Setup(m => m.GetComponent<ActiveCraftingData>(It.IsAny<int>()))
                .Returns(new ActiveCraftingData
                {
                    ActiveCrafts = new Dictionary<int, ActiveCraftingOperation>(),
                    AccessibleStations = new Dictionary<string, ICraftingStation>()
                });

            var registry = new CraftingRecipeRegistry();
            var recipe = CreateTestRecipe();
            registry.RegisterRecipe(recipe);

            var station = CreateTestStation();
            ((CraftingStation)station).AddRecipe(recipe.RecipeId);

            var manager = new CraftingManager(entityManager.Object, registry);

            CraftingStartedEvent eventRaised = null;
            manager.CraftingStarted += (s, e) => eventRaised = e;

            var operationId = manager.StartCrafting(1, recipe.RecipeId, station);

            Assert.Greater(operationId, 0);
            Assert.IsNotNull(eventRaised);
            Assert.AreEqual(1, eventRaised.EntityId);
            Assert.AreEqual(recipe.RecipeId, eventRaised.RecipeId);
        }

        [Test]
        public void Manager_CancelCrafting_WithValidOperation_RaisesEvent()
        {
            var entityManager = CreateMockEntityManager();
            var operation = new ActiveCraftingOperation
            {
                OperationId = 1,
                RecipeId = "recipe_test",
                StationId = "station_test",
                State = CraftingState.InProgress,
                ElapsedTimeSeconds = 5f,
                RequiredTimeSeconds = 30f
            };

            entityManager
                .Setup(m => m.GetComponent<ActiveCraftingData>(It.IsAny<int>()))
                .Returns(new ActiveCraftingData
                {
                    ActiveCrafts = new Dictionary<int, ActiveCraftingOperation> { { 1, operation } },
                    AccessibleStations = new Dictionary<string, ICraftingStation>()
                });

            var registry = new CraftingRecipeRegistry();
            var manager = new CraftingManager(entityManager.Object, registry);

            CraftingCancelledEvent eventRaised = null;
            manager.CraftingCancelled += (s, e) => eventRaised = e;

            var result = manager.CancelCrafting(1, 1);

            Assert.IsTrue(result);
            Assert.IsNotNull(eventRaised);
            Assert.AreEqual(1, eventRaised.EntityId);
        }

        #endregion

        #region Integration Tests (3 tests)

        [Test]
        public void FullRecipePipeline_CreationToRegistry()
        {
            var ingredients = new List<ICraftingIngredient> { CreateTestIngredient("wood", 10) };
            var results = new List<ICraftingResult> { CreateTestResult("wooden_bow", 1, 90f) };

            var recipe = new CraftingRecipe("bow_recipe", "Wooden Bow", "A simple wooden bow",
                CraftingStationType.Carpentry, 1, 45f, ingredients, results);

            var registry = new CraftingRecipeRegistry();
            registry.RegisterRecipe(recipe);

            Assert.IsTrue(registry.HasRecipe("bow_recipe"));
            var retrieved = registry.GetRecipe("bow_recipe");
            Assert.AreEqual("Wooden Bow", retrieved.Name);
            Assert.AreEqual(1, retrieved.Ingredients.Count);
            Assert.AreEqual(1, retrieved.Results.Count);
        }

        [Test]
        public void MultipleStations_MultipleRecipes()
        {
            var forge = CreateTestStation("forge_1", "Main Forge", CraftingStationType.Forge, 10);
            var alchemy = CreateTestStation("alch_1", "Alchemy Lab", CraftingStationType.Alchemy, 8);

            var forgeRecipe = CreateTestRecipe("sword", stationType: CraftingStationType.Forge);
            var alchemyRecipe = CreateTestRecipe("potion", stationType: CraftingStationType.Alchemy);

            ((CraftingStation)forge).AddRecipe(forgeRecipe.RecipeId);
            ((CraftingStation)alchemy).AddRecipe(alchemyRecipe.RecipeId);

            Assert.IsTrue(forge.CanCraft(forgeRecipe));
            Assert.IsFalse(forge.CanCraft(alchemyRecipe));
            Assert.IsTrue(alchemy.CanCraft(alchemyRecipe));
            Assert.IsFalse(alchemy.CanCraft(forgeRecipe));
        }

        [Test]
        public void DataSerialization_RecipeDataRoundTrip()
        {
            var originalData = new CraftingRecipeData(
                "recipe_enchant_ring",
                "Enchanted Ring",
                "A magically enhanced ring",
                CraftingStationType.Enchanting,
                5,
                60f,
                new[]
                {
                    new CraftingIngredient("gold", 5),
                    new CraftingIngredient("gemstone", 3)
                },
                new[]
                {
                    new CraftingResult("enchanted_ring", 1, 75f)
                },
                new[]
                {
                    new CraftingRequirementData(RequirementType.SkillLevel, "enchanting", 25)
                });

            var recipe = CraftingRecipe.FromData(originalData);

            Assert.AreEqual(originalData.Id, recipe.RecipeId);
            Assert.AreEqual(originalData.Name, recipe.Name);
            Assert.AreEqual(originalData.Ingredients.Length, recipe.Ingredients.Count);
            Assert.AreEqual(originalData.Results.Length, recipe.Results.Count);
            Assert.AreEqual(originalData.Requirements.Length, recipe.Requirements.Count);
        }

        #endregion

        #region Enum Tests (2 tests)

        [Test]
        public void CraftingStationType_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (int)CraftingStationType.Forge);
            Assert.AreEqual(1, (int)CraftingStationType.Alchemy);
            Assert.AreEqual(2, (int)CraftingStationType.Cooking);
            Assert.AreEqual(3, (int)CraftingStationType.Enchanting);
            Assert.AreEqual(4, (int)CraftingStationType.Carpentry);
        }

        [Test]
        public void RequirementType_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (int)RequirementType.SkillLevel);
            Assert.AreEqual(1, (int)RequirementType.ToolRequired);
            Assert.AreEqual(2, (int)RequirementType.StationRequired);
            Assert.AreEqual(3, (int)RequirementType.QuestComplete);
        }

        #endregion
    }
}
