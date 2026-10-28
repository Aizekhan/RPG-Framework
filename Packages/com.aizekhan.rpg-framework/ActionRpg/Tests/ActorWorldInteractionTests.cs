using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Interaction;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Trading;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Places in the action RPG, through the real modules: the world's places are the container's
    /// <see cref="IInteractionService"/>, the module hands it the game's distance, a chest opened once pays out of a
    /// loot table in the game's own rule, and only a living party member may use a place.
    /// </summary>
    public sealed class ActorWorldInteractionTests
    {
        private const string Chest = "chest";
        private const string Pelt = "pelt";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _module;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7
            };
            _content = new ActionRpgContent();
            _content.Items.Add(new Item(Pelt, "Pelt", "Soft", ItemType.Crafting, maxStackSize: 10));
            // The chest holds 10 gold and a pelt
            _content.LootTables.Add(LootTableBuilder.Create(Chest, "Chest").AddEntry(Currencies.Gold, minQuantity: 10, maxQuantity: 10)
                .AddGuaranteedDrop(Pelt).WithDrops(1, 1).Build());
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        [Test]
        public void AChestOpenedOnce_PaysFromItsLootTable_InTheGamesRule()
        {
            float standsAt = 5f;
            _host.Start(builder =>
            {
                _module = ActionRpgComposition.Configure(builder, _settings, _content);
                // A runner's track: distance is how far along it the player is from the place
                _module.InteractionDistance = (player, point) => System.Math.Abs(point.X - standsAt);
            });
            var places = _host.Container.Resolve<IInteractionService>();
            Assert.That(World.Interactions, Is.SameAs(places));

            // The game's rule, where its rules run: an opened chest pays out of its table
            _host.Container.Resolve<IEventBus>().Subscribe<InteractedEvent>(used =>
            {
                if (used.Kind == Chest)
                    World.GiveLoot(used.PlayerId, used.PointId);
            });
            places.Add(new InteractionPoint(Chest, Chest, 1f, "Open the chest", once: true).At(8f, 0, 0));

            Assert.That(World.NearestInteraction, Is.Null, "too far along the track");
            Assert.That(World.Interact(), Is.False);

            standsAt = 7.5f;
            Assert.That(World.NearestInteraction.Prompt, Is.EqualTo("Open the chest"));
            Assert.That(World.Interact(), Is.True);
            Assert.That(World.CurrencyOf(), Is.EqualTo(10));
            Assert.That(World.Inventory.GetAllItems().Select(i => i.ItemId), Is.EqualTo(new[] { Pelt }));

            Assert.That(World.Interact(Chest), Is.False, "it is open now");
            Assert.That(World.Interact(World.PlayerId, Chest), Is.EqualTo(InteractionOutcome.Closed));
            Assert.That(World.CurrencyOf(), Is.EqualTo(10));
            Assert.That(World.GiveLoot(World.PlayerId, "no_such_table"), Is.False);
            Assert.That(World.GiveLoot(12345, Chest), Is.False, "not in the party");
        }

        [Test]
        public void OnlyALivingPartyMember_MayUseAPlace()
        {
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));
            var used = new List<InteractedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<InteractedEvent>(used.Add);
            World.Interactions.Add(new InteractionPoint("lever", "lever", 1f));

            Assert.That(World.Interact(12345, "lever"), Is.EqualTo(InteractionOutcome.Refused), "not in the party");
            World.DealDamage(-1, World.PlayerId, 1000f);
            Assert.That(World.Player.IsAlive(), Is.False);
            Assert.That(World.Interact("lever"), Is.False, "a fallen player uses nothing");
            Assert.That(used, Is.Empty);

            World.RevivePlayer();
            Assert.That(World.Interact("lever"), Is.True);
            Assert.That(used.Single().PlayerId, Is.EqualTo(World.PlayerId));
        }
    }
}
