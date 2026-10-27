using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ECS;

namespace RPGFramework.Crafting.Tests
{
    /// <summary>
    /// A craft from start to finish with the real entity manager: time passes in <c>UpdateCrafting</c>, the craft
    /// completes with its results, several crafts on several entities each finish on time.
    /// </summary>
    public sealed class CraftingLifecycleTests
    {
        private const string IronBar = "iron_bar";

        private EntityManager _entities;
        private CraftingRecipeRegistry _recipes;
        private CraftingStation _forge;
        private CraftingManager _crafting;
        private readonly List<CraftingCompletedEvent> _completed = new List<CraftingCompletedEvent>();

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _recipes = new CraftingRecipeRegistry();
            _recipes.RegisterRecipe(new CraftingRecipe(IronBar, "Iron Bar", "Smelt ore", CraftingStationType.Forge, 1, 3f,
                new ICraftingIngredient[] { new CraftingIngredient("iron_ore", 2) },
                new ICraftingResult[] { new CraftingResult(IronBar, 1) }));
            _forge = new CraftingStation("forge", "Forge", CraftingStationType.Forge, 1, new[] { IronBar });
            _crafting = new CraftingManager(_entities, _recipes);
            _completed.Clear();
            _crafting.CraftingCompleted += (_, e) => _completed.Add(e);
        }

        [Test]
        public void ACraft_CompletesWhenItsTimeHasPassed_WithItsResults()
        {
            int smith = _entities.CreateEntity();
            int operation = _crafting.StartCrafting(smith, IronBar, _forge);
            Assert.That(operation, Is.GreaterThan(0));

            _crafting.UpdateCrafting(2f);
            Assert.That(_crafting.IsCrafting(smith), Is.True);
            Assert.That(_crafting.GetCraftingProgress(smith, operation), Is.EqualTo(200f / 3f).Within(0.01f));
            Assert.That(_completed, Is.Empty);

            _crafting.UpdateCrafting(1.5f);
            Assert.That(_crafting.IsCrafting(smith), Is.False);
            var done = _completed.Single();
            Assert.That(done.EntityId, Is.EqualTo(smith));
            Assert.That(done.RecipeId, Is.EqualTo(IronBar));
            Assert.That(done.Results.Single().ItemId, Is.EqualTo(IronBar));
            Assert.That(done.Results.Single().Quantity, Is.EqualTo(1));
            Assert.That(done.TotalTimeSeconds, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void SeveralCraftsOnSeveralEntities_EachFinishOnTime()
        {
            int first = _entities.CreateEntity();
            int second = _entities.CreateEntity();
            _crafting.StartCrafting(first, IronBar, _forge);
            _crafting.UpdateCrafting(1f);
            _crafting.StartCrafting(second, IronBar, _forge);
            _crafting.StartCrafting(second, IronBar, _forge);

            _crafting.UpdateCrafting(2f);
            Assert.That(_completed.Select(c => c.EntityId), Is.EqualTo(new[] { first }));

            _crafting.UpdateCrafting(1f);
            Assert.That(_completed.Count(c => c.EntityId == second), Is.EqualTo(2));
            Assert.That(_crafting.IsCrafting(second), Is.False);
        }

        private sealed class Bag : ICraftingInventory
        {
            public readonly Dictionary<(int, string), int> Items = new Dictionary<(int, string), int>();

            public int Count(int entityId, string itemId) => Items.TryGetValue((entityId, itemId), out int count) ? count : 0;

            public bool Has(int entityId, string itemId, int quantity) => Count(entityId, itemId) >= quantity;

            public bool Take(int entityId, string itemId, int quantity)
            {
                if (!Has(entityId, itemId, quantity))
                    return false;
                Items[(entityId, itemId)] = Count(entityId, itemId) - quantity;
                return true;
            }

            public void Give(int entityId, string itemId, int quantity) =>
                Items[(entityId, itemId)] = Count(entityId, itemId) + quantity;
        }

        [Test]
        public void WithAnInventory_IngredientsAreTaken_ResultsGiven_AndMissingIngredientsRefuseTheCraft()
        {
            var bag = new Bag();
            var crafting = new CraftingManager(_entities, _recipes, bag);
            int smith = _entities.CreateEntity();
            bag.Give(smith, "iron_ore", 3);

            Assert.That(crafting.StartCrafting(smith, IronBar, _forge), Is.GreaterThan(0));
            Assert.That(bag.Count(smith, "iron_ore"), Is.EqualTo(1), "taken at the start");
            Assert.That(crafting.StartCrafting(smith, IronBar, _forge), Is.EqualTo(-1), "one ore is not enough");
            Assert.That(bag.Count(smith, "iron_ore"), Is.EqualTo(1), "a refused craft takes nothing");

            crafting.UpdateCrafting(3f);
            Assert.That(bag.Count(smith, IronBar), Is.EqualTo(1));
        }

        [Test]
        public void ACancelledCraft_GivesTheIngredientsBack()
        {
            var bag = new Bag();
            var crafting = new CraftingManager(_entities, _recipes, bag);
            int smith = _entities.CreateEntity();
            bag.Give(smith, "iron_ore", 2);
            int operation = crafting.StartCrafting(smith, IronBar, _forge);

            crafting.UpdateCrafting(1f);
            Assert.That(crafting.CancelCrafting(smith, operation), Is.True);
            crafting.UpdateCrafting(5f);

            Assert.That(bag.Count(smith, "iron_ore"), Is.EqualTo(2));
            Assert.That(bag.Count(smith, IronBar), Is.EqualTo(0));
        }

        [Test]
        public void SuccessChances_DecideWhatComesOut_AndNothingAtAllIsAFailure()
        {
            _recipes.RegisterRecipe(new CraftingRecipe("gamble", "Gamble", "Maybe", CraftingStationType.Alchemy, 0, 1f,
                new ICraftingIngredient[] { new CraftingIngredient("herb", 1) },
                new ICraftingResult[] { new CraftingResult("potion", 1, 50f), new CraftingResult("rare_potion", 1, 10f) }));
            var lab = new CraftingStation("lab", "Lab", CraftingStationType.Alchemy, 0, new[] { "gamble" });
            var crafting = new CraftingManager(_entities, _recipes, random: new System.Random(11));
            var made = new List<string>();
            int failures = 0;
            crafting.CraftingCompleted += (_, e) => made.AddRange(e.Results.Select(r => r.ItemId));
            crafting.CraftingFailed += (_, e) => failures++;
            int alchemist = _entities.CreateEntity();

            for (int i = 0; i < 400; i++)
            {
                crafting.StartCrafting(alchemist, "gamble", lab);
                crafting.UpdateCrafting(1f);
            }

            Assert.That(made.Count(id => id == "potion"), Is.InRange(160, 240), "about half");
            Assert.That(made.Count(id => id == "rare_potion"), Is.InRange(20, 65), "about a tenth");
            Assert.That(failures, Is.InRange(140, 220), "about 45% make nothing (0.5 x 0.9)");
            Assert.That(crafting.IsCrafting(alchemist), Is.False);
        }

        [Test]
        public void ARecipeRemovedWhileCrafting_FailsTheCraft()
        {
            int smith = _entities.CreateEntity();
            string reason = null;
            _crafting.CraftingFailed += (_, e) => reason = e.FailureReason;
            _crafting.StartCrafting(smith, IronBar, _forge);

            _recipes.UnregisterRecipe(IronBar);
            _crafting.UpdateCrafting(3f);

            Assert.That(reason, Does.Contain("no longer exists"));
            Assert.That(_completed, Is.Empty);
            Assert.That(_crafting.IsCrafting(smith), Is.False);
        }
    }
}
