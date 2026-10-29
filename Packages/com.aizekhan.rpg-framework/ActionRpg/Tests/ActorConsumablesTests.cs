using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.StatusEffects;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Consumables in the kit: a potion heals and is used up, potions share their cooldown, a status-effect elixir
    /// puts its effect on, a scroll of the game's own only says it was read.
    /// </summary>
    public sealed class ActorConsumablesTests
    {
        private const string Potion = "potion", BigPotion = "big_potion", Elixir = "elixir", Scroll = "scroll", Rage = "rage";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private ActionRpgModule _module;

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
            _settings.Enemies.Add(new CharacterData { Id = "goblin", DisplayName = "Goblin" });
            _content = new ActionRpgContent();
            _content.Items.Add(new Consumable(Potion, "Potion", "", ConsumableEffects.RestoreHealth, 30f, cooldownCategory: "Potion", cooldownDuration: 5f));
            _content.Items.Add(new Consumable(BigPotion, "Big Potion", "", ConsumableEffects.RestoreHealthPercent, 0.5f, cooldownCategory: "Potion", cooldownDuration: 5f));
            _content.Items.Add(new Consumable(Elixir, "Elixir of Rage", "", Rage, 0f));
            _content.Items.Add(new Consumable(Scroll, "Scroll of Return", "", "town_portal", 0f, isConsumedOnUse: false));
            _content.StatusEffects.Add(StatusEffectData.CreateStatModifier(Rage, "Rage", EffectType.Buff, 10f, "Attack", 10f));
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

        private int CountOf(string itemId) => World.InventoryOf(World.PlayerId).GetAllItems().Count(i => i?.ItemId == itemId);

        [Test]
        public void APotion_Heals_IsUsedUp_AndPotionsShareACooldown()
        {
            StartGame();
            int hero = World.PlayerId;
            World.GiveItem(Potion, 2);
            World.GiveItem(BigPotion, 1);
            var consumables = World.Consumables;
            Assert.That(consumables.WhyNot(hero, Potion), Is.EqualTo("Already at full health"));

            World.Player.ApplyDamage(80f);
            Assert.That(consumables.Use(hero, Potion), Is.True);
            Assert.That(World.Player.Health, Is.EqualTo(50f));
            Assert.That(CountOf(Potion), Is.EqualTo(1));
            Assert.That(consumables.WhyNot(hero, BigPotion), Is.EqualTo("Not ready yet"), "the same category");
            Assert.That(consumables.CooldownLeft(hero, Potion), Is.EqualTo(5f).Within(1e-3f));

            Tick(5.05f);
            Assert.That(consumables.Use(hero, BigPotion), Is.True);
            Assert.That(World.Player.Health, Is.EqualTo(100f), "half of 100, up to the maximum");
            Assert.That(consumables.WhyNot(hero, "nothing"), Is.EqualTo("Not something to use"));
        }

        [Test]
        public void AnElixir_PutsItsEffectOn_AndAScrollOfTheGamesOwn_OnlySaysSo()
        {
            StartGame();
            int hero = World.PlayerId;
            var used = new List<ConsumableUsedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ConsumableUsedEvent>(used.Add);
            World.GiveItem(Elixir, 1);
            World.GiveItem(Scroll, 1);

            Assert.That(World.Consumables.Use(hero, Elixir), Is.True);
            Assert.That(World.Player.Attack, Is.EqualTo(60f));
            Assert.That(CountOf(Elixir), Is.EqualTo(0));
            Assert.That(World.Consumables.WhyNot(hero, Elixir), Is.EqualTo("None in the backpack"));

            Assert.That(World.Consumables.Use(hero, Scroll), Is.True);
            Assert.That(CountOf(Scroll), Is.EqualTo(1), "not consumed on use");
            Assert.That(used.Select(u => u.EffectId), Is.EqualTo(new[] { Rage, "town_portal" }));
        }
    }
}
