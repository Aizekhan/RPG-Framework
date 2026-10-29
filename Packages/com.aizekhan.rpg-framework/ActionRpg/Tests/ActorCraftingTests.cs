using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Crafting;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Skills;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Crafting from the kit's backpacks: the ingredients go at the start, the result comes after the recipe's time,
    /// a station, a skill and a tool can be needed, a cancel gives the ingredients back and a save keeps them.
    /// </summary>
    public sealed class ActorCraftingTests
    {
        private const string Ore = "ore", Bar = "bar", Sword = "sword", Hammer = "hammer";
        private const string Smelt = "smelt", ForgeSword = "forge_sword", Smithing = "smithing";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private ActionRpgModule _module;

        private ActorWorld World => _module.World;
        private ActorCrafting Crafting => World.Crafting;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7,
                StartingSkillPoints = 1
            };
            _settings.Enemies.Add(new CharacterData { Id = "goblin", DisplayName = "Goblin" });

            _content = new ActionRpgContent();
            _content.Items.Add(new Item(Ore, "Iron Ore", "", ItemType.Crafting, maxStackSize: 99));
            _content.Items.Add(new Item(Bar, "Iron Bar", "", ItemType.Crafting, maxStackSize: 99));
            _content.Items.Add(new Item(Sword, "Iron Sword", "", ItemType.Equipment));
            _content.Items.Add(new Item(Hammer, "Hammer", "", ItemType.Crafting));
            _content.Recipes.Add(new CraftingRecipe(Smelt, "Smelt Iron", "", CraftingStationType.Forge, 0, 2f,
                new ICraftingIngredient[] { new CraftingIngredient(Ore, 2) }, new ICraftingResult[] { new CraftingResult(Bar, 1) }));
            _content.Recipes.Add(new CraftingRecipe(ForgeSword, "Forge a Sword", "", CraftingStationType.Forge, 1, 1f,
                new ICraftingIngredient[] { new CraftingIngredient(Bar, 1) }, new ICraftingResult[] { new CraftingResult(Sword, 1) },
                new ICraftingRequirement[]
                {
                    new CraftingRequirement(RequirementType.SkillLevel, Smithing, 1),
                    new CraftingRequirement(RequirementType.ToolRequired, Hammer, 1)
                }));
            var tree = new SkillTree("crafts", "Crafts", "");
            tree.AddSkill(new Skill(Smithing, "Smithing", "", 3, SkillType.Crafting));
            _content.SkillTree = tree;
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame() =>
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds - 0.001f; time += 0.1f)
                _host.Update(0.1f);
        }

        private int CountOf(string itemId) => World.InventoryOf(World.PlayerId).GetAllItems().Count(i => i != null && i.ItemId == itemId);

        [Test]
        public void ACraft_TakesItsIngredients_AndMakesTheResultInTime()
        {
            StartGame();
            var completed = new List<CraftingCompletedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<CraftingCompletedEvent>(completed.Add);
            int hero = World.PlayerId;
            Assert.That(Crafting.WhyNot(hero, Smelt), Is.EqualTo("Missing ingredients"));
            World.GiveItem(Ore, 3);
            Assert.That(Crafting.CraftableBy(hero).Select(r => r.RecipeId), Is.EqualTo(new[] { Smelt }));

            Assert.That(Crafting.Start(hero, Smelt), Is.GreaterThanOrEqualTo(0));
            Assert.That(CountOf(Ore), Is.EqualTo(1));
            Assert.That(Crafting.Start(hero, Smelt), Is.EqualTo(-1), "one ore is not enough");
            Tick(1f);
            Assert.That(Crafting.IsCrafting(hero), Is.True);
            Assert.That(CountOf(Bar), Is.EqualTo(0));

            Tick(1.1f);
            Assert.That(CountOf(Bar), Is.EqualTo(1));
            Assert.That(Crafting.IsCrafting(hero), Is.False);
            Assert.That(completed.Single().RecipeId, Is.EqualTo(Smelt));
        }

        [Test]
        public void AStationASkillAndATool_AreNeeded_AndACancelGivesTheIngredientsBack()
        {
            StartGame();
            int hero = World.PlayerId;
            var anvil = new CraftingStation("anvil", "Anvil", CraftingStationType.Forge, 1, new[] { ForgeSword });
            World.GiveItem(Bar, 1);

            Assert.That(Crafting.WhyNot(hero, ForgeSword), Is.EqualTo("Cannot be made here"));
            Assert.That(Crafting.WhyNot(hero, ForgeSword, anvil), Is.EqualTo("Its requirements are not met"));
            Assert.That(World.Skills.Learn(hero, Smithing), Is.True);
            Assert.That(Crafting.WhyNot(hero, ForgeSword, anvil), Is.EqualTo("Its requirements are not met"), "no hammer");
            World.GiveItem(Hammer, 1);
            Assert.That(Crafting.WhyNot(hero, ForgeSword, anvil), Is.Null);

            int craft = Crafting.Start(hero, ForgeSword, anvil);
            Assert.That(craft, Is.GreaterThanOrEqualTo(0));
            Assert.That(CountOf(Bar), Is.EqualTo(0));
            Assert.That(CountOf(Hammer), Is.EqualTo(1), "a tool is not used up");
            Assert.That(Crafting.Cancel(hero, craft), Is.True);
            Assert.That(CountOf(Bar), Is.EqualTo(1));
            Tick(2f);
            Assert.That(CountOf(Sword), Is.EqualTo(0));
            Assert.That(Crafting.WhyNot(hero, "nope"), Is.EqualTo("Unknown recipe"));
        }

        [Test]
        public void ASaveDuringACraft_KeepsItsIngredients()
        {
            StartGame();
            int hero = World.PlayerId;
            World.GiveItem(Ore, 3);
            Crafting.Start(hero, Smelt);
            var progress = World.CaptureProgress();
            Assert.That(progress.Items.Count(i => i == Ore), Is.EqualTo(3));

            World.RestoreProgress(progress);

            Assert.That(Crafting.IsCrafting(hero), Is.False);
            Assert.That(CountOf(Ore), Is.EqualTo(3));
            Tick(3f);
            Assert.That(CountOf(Bar), Is.EqualTo(0));
        }
    }
}
