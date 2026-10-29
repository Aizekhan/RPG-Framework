using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Stats;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Gear in the action RPG (MythRunner FG-17): items from the backpack go on in slots and come back off, their
    /// bonuses change the character, two slots of a kind take two rings, a player keeps a set per hero, and it is
    /// saved with the progress; what cannot be worn, is too high or has nowhere to go is refused.
    /// </summary>
    public sealed class ActorGearTests
    {
        private const string Helm = "helm", Sword = "sword", Saber = "saber", Ring = "ring", Axe = "axe", Tome = "tome";

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
            _settings = Settings();
            _content = Content();
        }

        private static ActionRpgSettings Settings()
        {
            var settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7
            };
            settings.GearSlots.Add(new GearSlot("head"));
            settings.GearSlots.Add(new GearSlot("weapon"));
            settings.GearSlots.Add(new GearSlot("ring_left", "ring"));
            settings.GearSlots.Add(new GearSlot("ring_right", "ring"));
            return settings;
        }

        private static ActionRpgContent Content()
        {
            var content = new ActionRpgContent();
            foreach (var id in new[] { Helm, Sword, Saber, Ring, Axe, Tome })
                content.Items.Add(new Item(id, id, id, id == Tome ? ItemType.Quest : ItemType.Equipment));
            content.Gear.Add(new GearData(Helm, "head", new GearBonus("Defense", 10f), new GearBonus("MaxHealth", 20f)));
            content.Gear.Add(new GearData(Sword, "weapon", new GearBonus("Attack", 5f), new GearBonus("Crit", 0.1f)));
            content.Gear.Add(new GearData(Saber, "weapon", new GearBonus("Attack", 8f)));
            content.Gear.Add(new GearData(Ring, "ring", new GearBonus("Attack", 1f)));
            content.Gear.Add(new GearData(Axe, "weapon", new GearBonus("Attack", 30f)) { MinimumLevel = 5 });
            return content;
        }

        private void StartGame()
        {
            _host = new GameHost(_logger);
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Dispose();
            _logger.Dispose();
        }

        private int CountInBackpack(string itemId) => World.Inventory.GetAllItems().Count(i => i != null && i.ItemId == itemId);

        [Test]
        public void Equip_TakesItFromTheBackpack_AndTheBonusesGoOn_UnequipPutsItBack()
        {
            StartGame();
            var changes = new List<GearChangedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<GearChangedEvent>(changes.Add);
            World.GiveItem(Helm);

            Assert.That(World.Equip(Helm), Is.True);

            Assert.That(World.WornIn("head")?.ItemId, Is.EqualTo(Helm));
            Assert.That(CountInBackpack(Helm), Is.EqualTo(0));
            Assert.That(World.Player.Defense, Is.EqualTo(15f));
            Assert.That(World.Player.MaxHealth, Is.EqualTo(120f));
            Assert.That(World.Player.Health, Is.EqualTo(120f), "more max health comes with the health");
            Assert.That(changes.Single().SlotId, Is.EqualTo("head"));
            Assert.That(changes.Single().PreviousItemId, Is.Empty);

            Assert.That(World.Unequip("head"), Is.True);

            Assert.That(World.WornIn("head"), Is.Null);
            Assert.That(CountInBackpack(Helm), Is.EqualTo(1));
            Assert.That(World.Player.Defense, Is.EqualTo(5f));
            Assert.That(World.Player.MaxHealth, Is.EqualTo(100f));
            Assert.That(changes.Last().ItemId, Is.Empty);
            Assert.That(changes.Last().PreviousItemId, Is.EqualTo(Helm));
        }

        [Test]
        public void ASecondWeapon_SwapsWithTheFirst_WhichGoesBack()
        {
            StartGame();
            World.GiveItem(Sword);
            World.GiveItem(Saber);
            World.Equip(Sword);

            Assert.That(World.Equip(Saber), Is.True);

            Assert.That(World.WornIn("weapon")?.ItemId, Is.EqualTo(Saber));
            Assert.That(CountInBackpack(Sword), Is.EqualTo(1));
            Assert.That(World.Player.Attack, Is.EqualTo(58f));
        }

        [Test]
        public void TwoRingSlots_TakeTwoRings_AThirdReplacesTheFirst()
        {
            StartGame();
            World.GiveItem(Ring, 3);

            Assert.That(World.Equip(Ring), Is.True);
            Assert.That(World.Equip(Ring), Is.True);
            Assert.That(World.WornIn("ring_left")?.ItemId, Is.EqualTo(Ring));
            Assert.That(World.WornIn("ring_right")?.ItemId, Is.EqualTo(Ring));
            Assert.That(World.Player.Attack, Is.EqualTo(52f));

            Assert.That(World.Equip(Ring, "ring_right"), Is.True);
            Assert.That(CountInBackpack(Ring), Is.EqualTo(1), "one on each hand, the swapped one back");
            Assert.That(World.Player.Attack, Is.EqualTo(52f));
        }

        [Test]
        public void WhatCannotBeWorn_IsRefused_AndNothingMoves()
        {
            StartGame();
            World.GiveItem(Tome);
            World.GiveItem(Axe);
            World.GiveItem(Helm);

            Assert.That(World.Equip(Sword), Is.False, "not in the backpack");
            Assert.That(World.Equip(Tome), Is.False, "not gear");
            Assert.That(World.Equip(Axe), Is.False, "level 5 and up");
            Assert.That(World.Equip(Helm, "weapon"), Is.False, "the weapon slot takes no helm");
            Assert.That(World.Equip(Helm, "nowhere"), Is.False);
            Assert.That(World.Unequip("head"), Is.False, "nothing on");
            Assert.That(World.Inventory.Count, Is.EqualTo(3));
            Assert.That(World.Player.Attack, Is.EqualTo(50f));
        }

        [Test]
        public void AFullBackpack_KeepsTheItemOn()
        {
            _settings.InventoryCapacity = 1;
            StartGame();
            World.GiveItem(Helm);
            World.Equip(Helm);
            World.GiveItem(Tome);

            Assert.That(World.Unequip("head"), Is.False);
            Assert.That(World.WornIn("head")?.ItemId, Is.EqualTo(Helm));
            Assert.That(World.Player.Defense, Is.EqualTo(15f));
        }

        [Test]
        public void EachHero_HasTheirOwnSet_OnlyTheSetInUseCounts()
        {
            StartGame();
            var switched = new List<LoadoutChangedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<LoadoutChangedEvent>(switched.Add);
            World.GiveItem(Helm);
            World.GiveItem(Ring);
            World.Equip(Helm);

            Assert.That(World.UseLoadout("archer"), Is.True);
            Assert.That(World.Loadout, Is.EqualTo("archer"));
            Assert.That(World.WornIn("head"), Is.Null);
            Assert.That(World.Player.Defense, Is.EqualTo(5f), "the knight's helm stays with the knight");
            Assert.That(World.Equip(Ring), Is.True);
            Assert.That(World.Player.Attack, Is.EqualTo(51f));
            Assert.That(CountInBackpack(Helm), Is.EqualTo(0), "the helm is still worn, by the other hero");

            Assert.That(World.UseLoadout(ActorGear.DefaultLoadout), Is.True);
            Assert.That(World.Player.Defense, Is.EqualTo(15f));
            Assert.That(World.Player.Attack, Is.EqualTo(50f));
            Assert.That(World.Gear.LoadoutsOf(World.PlayerId), Is.EquivalentTo(new[] { "", "archer" }));
            Assert.That(World.Gear.WornIn(World.PlayerId, "ring_left", "archer")?.ItemId, Is.EqualTo(Ring));
            Assert.That(switched.Select(s => s.Loadout), Is.EqualTo(new[] { "archer", "" }));
        }

        [Test]
        public void BonusesOf_ListsTheGamesOwnStatsToo()
        {
            StartGame();
            World.GiveItem(Sword);
            World.Equip(Sword);

            var bonuses = World.Gear.BonusesOf(World.PlayerId);

            Assert.That(bonuses.Select(b => b.Stat), Is.EquivalentTo(new[] { "Attack", "Crit" }));
            Assert.That(World.Player.Attack, Is.EqualTo(55f), "the kit's stat is changed, Crit is the game's");
        }

        [Test]
        public void Gear_IsSavedWithTheProgress_EverySet()
        {
            StartGame();
            World.GiveItem(Helm);
            World.GiveItem(Ring);
            World.Equip(Helm);
            World.UseLoadout("archer");
            World.Equip(Ring);
            var saved = World.CaptureProgress();
            _host.Dispose();

            StartGame();
            World.RestoreProgress(saved);

            Assert.That(saved.Items, Is.Empty, "worn items are not in the backpack");
            Assert.That(World.Loadout, Is.EqualTo("archer"));
            Assert.That(World.WornIn("ring_left")?.ItemId, Is.EqualTo(Ring));
            Assert.That(World.Gear.WornIn(World.PlayerId, "head", ActorGear.DefaultLoadout)?.ItemId, Is.EqualTo(Helm));
            Assert.That(World.Player.Attack, Is.EqualTo(51f));
            Assert.That(World.Player.Defense, Is.EqualTo(5f));
            Assert.That(World.Inventory.Count, Is.EqualTo(0));
        }

        [Test]
        public void DroppedGear_RollsAffixes_ThatAreWornSavedAndNamed()
        {
            _content.Affixes = new AffixPool()
                .Add(new AffixDefinition("brutal", AffixSlot.Prefix, "Brutal", "Attack", 10f, 10f).OnlyOn("weapon"))
                .Add(new AffixDefinition("warded", AffixSlot.Suffix, "of Warding", "Defense", 3f, 3f).OnlyOn("head"));
            _content.Rarities = new RarityTable().Set(ItemRarity.Rare, 1);
            _content.LootTables.Add(LootTableBuilder.Create("chest", "Chest").AddGuaranteedDrop(Sword).AddGuaranteedDrop(Tome)
                .WithDrops(0, 0).Build());
            StartGame();

            Assert.That(World.GiveLoot(World.PlayerId, "chest"), Is.True);
            var ids = World.Inventory.GetAllItems().Where(i => i != null).Select(i => i.ItemId).ToList();
            Assert.That(ids, Is.EquivalentTo(new[] { "sword~2~brutal=10", Tome }), "only a weapon affix fits a sword; the tome is not gear");
            var rolled = World.Inventory.GetAllItems().First(i => i != null && i.ItemId != Tome);
            Assert.That(rolled.Name, Is.EqualTo("Brutal sword"));
            Assert.That(rolled.Rarity, Is.EqualTo(ItemRarity.Rare));

            Assert.That(World.Equip(rolled.ItemId), Is.True);
            Assert.That(World.Player.Attack, Is.EqualTo(65f), "50 + the sword's 5 + Brutal's 10");

            var saved = World.CaptureProgress();
            _host.Dispose();
            StartGame();
            World.RestoreProgress(saved);
            Assert.That(World.WornIn("weapon")?.ItemId, Is.EqualTo("sword~2~brutal=10"));
            Assert.That(World.Player.Attack, Is.EqualTo(65f), "the affix comes back from the id");
            Assert.That(World.Unequip("weapon"), Is.True);
            Assert.That(World.Player.Attack, Is.EqualTo(50f));
            Assert.That(World.Inventory.GetAllItems().Count(i => i != null && i.Name == "Brutal sword"), Is.EqualTo(1));
        }

        [Test]
        public void SharedProgress_AFriendWhoLeaves_LeavesTheirGearInTheBackpack()
        {
            StartGame();
            int friend = World.AddPlayer();
            World.GiveItem(friend, Helm, 1);

            Assert.That(World.Gear.Equip(friend, Helm), Is.True);
            Assert.That(World.Get(friend).Defense, Is.EqualTo(15f));
            Assert.That(World.Player.Defense, Is.EqualTo(5f), "gear is the wearer's");

            World.RemovePlayer(friend);

            Assert.That(CountInBackpack(Helm), Is.EqualTo(1));
        }

        [Test]
        public void Validate_FindsGearWithoutAnItemOrASlot()
        {
            _content.Gear.Add(new GearData("ghost", "head"));
            _content.Items.Add(new Item("cape", "cape", "cape", ItemType.Equipment));
            _content.Gear.Add(new GearData("cape", "back"));
            _settings.GearSlots.Add(new GearSlot("head"));

            var problems = _settings.Validate(_content);

            Assert.That(problems, Has.Some.Contains("'ghost' is not an item"));
            Assert.That(problems, Has.Some.Contains("No gear slot takes 'cape'"));
            Assert.That(problems, Has.Some.Contains("Several gear slots use the id 'head'"));
        }
    }
}
