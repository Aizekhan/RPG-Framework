using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.ECS;

namespace RPGFramework.Crafting.Tests
{
    /// <summary>
    /// A recipe's requirements are checked before the craft starts: stations by the manager, tools through the
    /// inventory, skills and quests through the game's checker, and nothing is taken as met unchecked.
    /// </summary>
    public sealed class CraftingRequirementTests
    {
        private const string Sword = "steel_sword";

        private EntityManager _entities;
        private CraftingRecipeRegistry _recipes;
        private CraftingStation _forge;

        private sealed class Checker : ICraftingRequirementChecker
        {
            public readonly Dictionary<string, int> Skills = new Dictionary<string, int>();
            public readonly HashSet<string> Quests = new HashSet<string>();

            public bool IsSatisfied(int entityId, ICraftingRequirement requirement)
            {
                switch (requirement.Type)
                {
                    case RequirementType.SkillLevel:
                        return Skills.TryGetValue(requirement.ParameterId, out int level) && level >= requirement.Value;
                    case RequirementType.QuestComplete:
                        return Quests.Contains(requirement.ParameterId);
                    default:
                        return false;
                }
            }
        }

        private sealed class Bag : ICraftingInventory
        {
            public readonly Dictionary<string, int> Items = new Dictionary<string, int>();

            public bool Has(int entityId, string itemId, int quantity) =>
                Items.TryGetValue(itemId, out int count) && count >= quantity;

            public bool Take(int entityId, string itemId, int quantity)
            {
                if (!Has(entityId, itemId, quantity))
                    return false;
                Items[itemId] -= quantity;
                return true;
            }

            public void Give(int entityId, string itemId, int quantity) =>
                Items[itemId] = (Items.TryGetValue(itemId, out int count) ? count : 0) + quantity;
        }

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _recipes = new CraftingRecipeRegistry();
            _forge = new CraftingStation("forge", "Forge", CraftingStationType.Forge, 2, new[] { Sword });
        }

        private void Recipe(params ICraftingRequirement[] requirements)
        {
            _recipes.RegisterRecipe(new CraftingRecipe(Sword, "Steel Sword", "", CraftingStationType.Forge, 1, 1f,
                new ICraftingIngredient[] { new CraftingIngredient("steel", 1) },
                new ICraftingResult[] { new CraftingResult(Sword, 1) },
                requirements));
        }

        [Test]
        public void ASkillRequirement_WithoutAChecker_RefusesTheCraft()
        {
            Recipe(new CraftingRequirement(RequirementType.SkillLevel, "smithing", 5));
            var crafting = new CraftingManager(_entities, _recipes);

            Assert.That(crafting.StartCrafting(_entities.CreateEntity(), Sword, _forge), Is.EqualTo(-1));
        }

        [Test]
        public void SkillAndQuestRequirements_AreAskedOfTheGame()
        {
            Recipe(new CraftingRequirement(RequirementType.SkillLevel, "smithing", 5),
                new CraftingRequirement(RequirementType.QuestComplete, "masters_test", 0));
            var checker = new Checker();
            var crafting = new CraftingManager(_entities, _recipes, requirements: checker);
            int smith = _entities.CreateEntity();

            checker.Skills["smithing"] = 4;
            checker.Quests.Add("masters_test");
            Assert.That(crafting.StartCrafting(smith, Sword, _forge), Is.EqualTo(-1), "skill too low");

            checker.Skills["smithing"] = 5;
            checker.Quests.Clear();
            Assert.That(crafting.StartCrafting(smith, Sword, _forge), Is.EqualTo(-1), "quest not done");

            checker.Quests.Add("masters_test");
            Assert.That(crafting.StartCrafting(smith, Sword, _forge), Is.GreaterThan(0));
        }

        [Test]
        public void AToolRequirement_IsCheckedInTheInventory_AndTheToolIsNotUsedUp()
        {
            Recipe(new CraftingRequirement(RequirementType.ToolRequired, "hammer", 1));
            var bag = new Bag();
            var crafting = new CraftingManager(_entities, _recipes, bag);
            int smith = _entities.CreateEntity();
            bag.Give(smith, "steel", 2);

            Assert.That(crafting.StartCrafting(smith, Sword, _forge), Is.EqualTo(-1), "no hammer");

            bag.Give(smith, "hammer", 1);
            Assert.That(crafting.StartCrafting(smith, Sword, _forge), Is.GreaterThan(0));
            Assert.That(bag.Items["hammer"], Is.EqualTo(1));
            Assert.That(bag.Items["steel"], Is.EqualTo(1));
        }

        [Test]
        public void AStationRequirement_NamesTheStationOrItsType_AndAMinimumLevel()
        {
            var crafting = new CraftingManager(_entities, _recipes);
            int smith = _entities.CreateEntity();
            var station = new CraftingRequirement(RequirementType.StationRequired, "forge", 2);

            Assert.That(crafting.IsRequirementMet(smith, station, _forge), Is.True, "by id, level 2");
            Assert.That(crafting.IsRequirementMet(smith, new CraftingRequirement(RequirementType.StationRequired, "Forge", 0), _forge),
                Is.True, "by type");
            Assert.That(crafting.IsRequirementMet(smith, new CraftingRequirement(RequirementType.StationRequired, "forge", 3), _forge),
                Is.False, "level too low");
            var lab = new CraftingStation("lab", "Lab", CraftingStationType.Alchemy, 5, new[] { Sword });
            Assert.That(crafting.IsRequirementMet(smith, station, lab), Is.False, "another station");

            Recipe(station);
            Assert.That(crafting.StartCrafting(smith, Sword, _forge), Is.GreaterThan(0), "no checker needed for stations");
        }
    }
}
