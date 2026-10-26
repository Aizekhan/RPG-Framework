using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Bootstrap.Gameplay;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Progression;
using RPGFramework.Quests;

namespace ReferenceGame.Tests
{
    /// <summary>
    /// Boots the whole reference game through <see cref="GameHost"/> (the same composition the scene uses) and plays it,
    /// proving that the modules register, the systems tick, events are published and handled across modules,
    /// and that failures reach the log instead of disappearing.
    /// </summary>
    public sealed class GoblinValleyTests
    {
        private const int Seed = 1234;

        private MemoryLogSink _logSink;
        private Logger _logger;
        private GameHost _host;
        private GoblinValleyModule _module;
        private EventBus _bus;

        private GoblinValleyGame Game => _module.Game;

        private IEnumerable<LogEntry> Problems =>
            _logSink.Snapshot().Where(entry => entry.Severity >= LogSeverity.Error);

        [SetUp]
        public void SetUp()
        {
            _logSink = new MemoryLogSink(4096);
            _logger = new Logger(LogSeverity.Debug, _logSink);
            _host = new GameHost(_logger);
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame()
        {
            _host.Start(builder => _module = GoblinValleyComposition.Configure(builder, Seed));
            _bus = (EventBus)_host.Container.Resolve<IEventBus>();
        }

        private void KillEveryGoblin()
        {
            for (int round = 0; round < 200 && Game.LivingGoblins.Any(); round++)
            {
                Assert.That(Game.PlayerAttacksNearestGoblin(), Is.True, "the hero died before clearing the valley");
                _host.Update(0.1f);
            }
            Assert.That(Game.LivingGoblins, Is.Empty);
        }

        [Test]
        public void Start_RegistersEveryModuleAndStartsTheGame()
        {
            StartGame();

            Assert.That(_host.State, Is.EqualTo(GameHostState.Running));
            Assert.That(_host.Composition.Modules.Select(m => m.Name), Is.SupersetOf(new[]
            {
                FrameworkCoreModule.ModuleName, CombatModule.ModuleName, ProgressionModule.ModuleName, QuestsModule.ModuleName,
                ItemsModule.ModuleName, LootModule.ModuleName, CombatQuestsModule.ModuleName, GoblinValleyModule.ModuleName
            }));
            Assert.That(Game.IsStarted, Is.True);
            Assert.That(Game.Level, Is.EqualTo(1));
            Assert.That(Game.Player.MaxHealth, Is.EqualTo(100f));
            var quests = _host.Container.Resolve<IQuestManager>();
            Assert.That(quests.GetActiveQuests(Game.PlayerId).Select(q => q.QuestId), Has.Member(GoblinValleyContent.ClearTheValley));
            Assert.That(Problems, Is.Empty);
        }

        [Test]
        public void ClearingTheValley_RunsTheWholeEventChain()
        {
            StartGame();
            var deaths = new List<DeathEvent>();
            var progress = new List<ObjectiveProgressedEvent>();
            var completed = new List<QuestCompletedEvent>();
            var levelUps = new List<LevelUpEvent>();
            var statGrowth = new List<StatIncreasedEvent>();
            var itemsAdded = new List<ItemAddedEvent<IItem>>();
            _bus.Subscribe<DeathEvent>(deaths.Add);
            _bus.Subscribe<ObjectiveProgressedEvent>(progress.Add);
            _bus.Subscribe<QuestCompletedEvent>(completed.Add);
            _bus.Subscribe<LevelUpEvent>(levelUps.Add);
            _bus.Subscribe<StatIncreasedEvent>(statGrowth.Add);
            _bus.Subscribe<ItemAddedEvent<IItem>>(itemsAdded.Add);

            for (int i = 0; i < GoblinValleyContent.GoblinsToClear; i++)
                Game.SpawnGoblin();
            KillEveryGoblin();

            // Combat → bus
            Assert.That(deaths, Has.Count.EqualTo(GoblinValleyContent.GoblinsToClear));
            Assert.That(deaths.All(d => d.KillerEntityId == Game.PlayerId), Is.True);
            // Combat → KillObjectiveBridge → Quests
            Assert.That(progress.Select(p => p.CurrentProgress), Is.EqualTo(new[] { 1, 2, 3 }));
            // Objective done → the game turns the quest in → rewards
            Assert.That(completed.Select(c => c.QuestId), Is.EqualTo(new[] { GoblinValleyContent.ClearTheValley }));
            Assert.That(Game.QuestCompleted, Is.True);
            // Kill XP + quest XP → Progression level-up → StatGrowthPublisher → the game applies the stats
            Assert.That(Game.TotalXp, Is.EqualTo(GoblinValleyContent.GoblinKillXp * GoblinValleyContent.GoblinsToClear + GoblinValleyContent.QuestXp));
            Assert.That(levelUps, Is.Not.Empty);
            Assert.That(Game.Level, Is.EqualTo(levelUps.Last().NewLevel).And.GreaterThan(1));
            Assert.That(statGrowth.Select(s => s.StatId), Is.SupersetOf(new[] { "MaxHealth", "Attack", "Defense" }));
            Assert.That(Game.Player.MaxHealth, Is.EqualTo(100f + 20f * (Game.Level - 1)));
            // Loot and quest rewards → Inventory → bus
            var backpack = Game.Inventory.GetAllItems().Select(item => item.ItemId).ToList();
            Assert.That(backpack.Count(id => id == GoblinValleyContent.GoblinEar), Is.EqualTo(GoblinValleyContent.GoblinsToClear));
            Assert.That(backpack, Has.Member(GoblinValleyContent.ValleyMedal));
            Assert.That(itemsAdded, Has.Count.EqualTo(backpack.Count));

            Assert.That(_bus.HandlerErrorCount, Is.EqualTo(0));
            Assert.That(Problems, Is.Empty);
        }

        [Test]
        public void HostTicksTheWorldWithoutErrors()
        {
            StartGame();

            for (int frame = 0; frame < 30; frame++)
            {
                _host.Update(1f / 30f);
                _host.FixedUpdate(1f / 50f);
                _host.LateUpdate(1f / 30f);
            }

            Assert.That(_host.World.Systems.Systems.Count, Is.GreaterThan(0));
            Assert.That(Problems, Is.Empty);
        }

        [Test]
        public void FailingHandler_IsLoggedAndTheGameKeepsRunning()
        {
            StartGame();
            _bus.Subscribe<DeathEvent>(_ => throw new InvalidOperationException("broken listener"));
            Game.SpawnGoblin();

            KillEveryGoblin();

            Assert.That(_bus.HandlerErrorCount, Is.EqualTo(1));
            Assert.That(Problems.Single().Exception?.Message, Is.EqualTo("broken listener"));
            Assert.That(Game.GoblinsKilled, Is.EqualTo(1), "the game's own death rule still ran");
        }

        [Test]
        public void MissingModule_FailsTheStartLoudly()
        {
            Assert.Throws(Is.InstanceOf<Exception>(), () => _host.Start(builder => builder.UseModule(new GoblinValleyModule(new GoblinValleyOptions { Seed = Seed }))));

            Assert.That(_host.State, Is.EqualTo(GameHostState.Failed));
            Assert.That(_logSink.Snapshot().Any(entry => entry.Severity == LogSeverity.Critical), Is.True);
        }

        [Test]
        public void Shutdown_StopsTheGameCleanly()
        {
            StartGame();
            Game.SpawnGoblin();

            _host.Dispose();

            Assert.That(_host.State, Is.EqualTo(GameHostState.Stopped));
            Assert.That(_module.Game, Is.Null);
            Assert.That(Problems, Is.Empty);
        }
    }
}
