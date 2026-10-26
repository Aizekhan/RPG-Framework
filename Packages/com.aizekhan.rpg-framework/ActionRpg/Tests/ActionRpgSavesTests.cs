using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Progress survives a restart: level, stats, experience, backpack, quest progress and finished quests come
    /// back, and finished quests do not pay their rewards twice.
    /// </summary>
    public sealed class ActionRpgSavesTests
    {
        private const string Goblin = "goblin";
        private const string Ear = "goblin_ear";
        private const string Medal = "medal";

        private string _directory;
        private readonly List<(GameHost Host, Logger Logger)> _sessions = new List<(GameHost, Logger)>();

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RPGFrameworkTests_ActionRpgSaves_" + Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var (host, logger) in _sessions)
            {
                host.Dispose();
                logger.Dispose();
            }
            _sessions.Clear();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private ActorWorld StartSession()
        {
            var settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f, HealthPerLevel = 20f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 3
            };
            settings.Enemies.Add(new CharacterData { Id = Goblin, DisplayName = "Goblin", MaxHealth = 10f, ExperienceReward = 60, LootTableId = Goblin });
            settings.StartingQuests.Add("first");
            settings.StartingQuests.Add("long");

            var content = new ActionRpgContent();
            content.Items.Add(new Item(Ear, "Goblin Ear", "", ItemType.Crafting, maxStackSize: 99));
            content.Items.Add(new Item(Medal, "Medal", "", ItemType.Quest));
            content.LootTables.Add(LootTableBuilder.Create(Goblin, "Goblin").AddGuaranteedDrop(Ear).WithDrops(0, 0).Build());
            content.Quests.Add(KillQuest("first", 1, new QuestReward(RewardType.Item, Medal, 1), new QuestReward(RewardType.Experience, null, 30)));
            content.Quests.Add(KillQuest("long", 10));

            var logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            var host = new GameHost(logger);
            _sessions.Add((host, logger));
            ActionRpgModule module = null;
            host.Start(builder => module = ActionRpgComposition.Configure(builder, settings, content));
            return module.World;
        }

        private static Quest KillQuest(string id, int count, params IQuestReward[] rewards) =>
            new Quest(id, id, "", new List<IQuestObjective> { new QuestObjective("kill", ObjectiveType.Kill, Goblin, count, "Kill") },
                rewards.ToList(), new List<string>(), minimumLevel: 1, isRepeatable: false);

        private static void Slay(ActorWorld world, int enemy)
        {
            for (int hit = 0; hit < 20 && world.Get(enemy) != null; hit++)
                world.Attack(world.PlayerId, enemy);
        }

        [Test]
        public void SaveThenLoad_InANewSession_RestoresTheProgress()
        {
            var first = StartSession();
            Slay(first, first.SpawnEnemy(Goblin));
            Slay(first, first.SpawnEnemy(Goblin));
            Assume.That(first.Level, Is.EqualTo(2));
            Assume.That(first.CompletedQuests, Is.EqualTo(new[] { "first" }));
            var saved = new { first.Level, first.TotalExperience, first.Player.MaxHealth, Items = first.Inventory.Count };
            Assert.That(new ActionRpgSaves(first, _directory).Save(), Is.Null);

            var second = StartSession();
            var saves = new ActionRpgSaves(second, _directory);
            Assert.That(saves.HasSave, Is.True);
            Assert.That(saves.Load(), Is.Null);

            Assert.That(second.Level, Is.EqualTo(saved.Level));
            Assert.That(second.TotalExperience, Is.EqualTo(saved.TotalExperience), "quest rewards are not paid twice");
            Assert.That(second.Player.MaxHealth, Is.EqualTo(saved.MaxHealth));
            Assert.That(second.Player.Health, Is.EqualTo(second.Player.MaxHealth));
            Assert.That(second.Inventory.Count, Is.EqualTo(saved.Items), "the medal is not given twice");
            Assert.That(second.Inventory.GetAllItems().Count(i => i.ItemId == Medal), Is.EqualTo(1));
            Assert.That(second.Kills, Is.EqualTo(2));
            Assert.That(second.CompletedQuests, Is.EqualTo(new[] { "first" }));
            var longQuest = second.ActiveQuests.Single();
            Assert.That(longQuest.Quest.Id, Is.EqualTo("long"));
            Assert.That(longQuest.Progress.ObjectiveProgress["kill"], Is.EqualTo(2));
        }

        [Test]
        public void Load_WithoutASave_ChangesNothing()
        {
            var world = StartSession();
            var saves = new ActionRpgSaves(world, _directory);

            Assert.That(saves.HasSave, Is.False);
            Assert.That(saves.Load(), Is.Null);
            Assert.That(world.Level, Is.EqualTo(1));
        }

        [Test]
        public void Delete_RemovesTheSave()
        {
            var world = StartSession();
            var saves = new ActionRpgSaves(world, _directory);
            saves.Save();

            Assert.That(saves.Delete(), Is.True);
            Assert.That(saves.HasSave, Is.False);
        }

        [Test]
        public void Save_AfterTheFolderWasDeleted_CreatesItAgain()
        {
            var world = StartSession();
            var saves = new ActionRpgSaves(world, _directory);
            Directory.Delete(_directory, true);

            Assert.That(saves.Save(), Is.Null);
            Assert.That(saves.HasSave, Is.True);
        }
    }
}
