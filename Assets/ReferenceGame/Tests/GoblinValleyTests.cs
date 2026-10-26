using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ActionRpg;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using UnityEditor;

namespace ReferenceGame.Tests
{
    /// <summary>
    /// Goblin Valley boots from its Game Setup asset the way the scene does, plays a wave, and runs its own rule:
    /// kills in a row without being hurt earn bonus loot.
    /// </summary>
    public sealed class GoblinValleyTests
    {
        public const string SetupPath = "Assets/ReferenceGame/Content/GoblinValleySetup.asset";

        private MemoryLogSink _log;
        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _actionRpg;
        private readonly List<KillStreakRewardedEvent> _rewards = new List<KillStreakRewardedEvent>();

        private ActorWorld World => _actionRpg.World;
        private KillStreak Streak => _host.Container.Resolve<KillStreak>();

        [SetUp]
        public void SetUp()
        {
            _log = new MemoryLogSink(1024);
            _logger = new Logger(LogSeverity.Debug, _log);
            _host = new GameHost(_logger);
            _rewards.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartFromSetup(KillStreakSettings killStreak = null)
        {
            var setup = AssetDatabase.LoadAssetAtPath<GameSetup>(SetupPath);
            Assert.That(setup, Is.Not.Null, $"{SetupPath} is missing; run RPG Framework > Reference Game > Rebuild Goblin Valley");
            Assert.That(setup.Validate(), Is.Empty, "the Game Setup has problems");
            var settings = setup.BuildSettings();
            settings.Seed = 11;
            _host.Start(builder => _actionRpg = GoblinValleyComposition.ConfigureWithActionRpg(builder, settings,
                setup.BuildContent(), killStreak));
            _host.Container.Resolve<IEventBus>().Subscribe<KillStreakRewardedEvent>(_rewards.Add);
        }

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds; time += 0.1f)
                _host.Update(0.1f);
        }

        private void Slay(int enemy)
        {
            for (int hit = 0; hit < 100 && World.Get(enemy) != null; hit++)
                World.Attack(World.PlayerId, enemy);
        }

        private int ItemCount(string itemId) =>
            World.Inventory.GetAllItems().Count(item => item != null && item.ItemId == itemId);

        [Test]
        public void Setup_HasTheHeroTwoKindsOfGoblinsAndTwoQuests()
        {
            var setup = AssetDatabase.LoadAssetAtPath<GameSetup>(SetupPath);

            Assert.That(setup.Player.Id, Is.EqualTo("hero"));
            Assert.That(setup.Enemies.Select(e => e.Id), Is.EquivalentTo(new[] { "goblin", "goblin_brute" }));
            Assert.That(setup.Characters.All(c => c.Prefab != null), Is.True, "every character has its model prefab");
            Assert.That(setup.StartingQuests.Select(q => q.questId), Is.EquivalentTo(new[] { "clear_the_valley", "brute_force" }));
        }

        [Test]
        public void Game_StartsAndAWaveComesAndDies()
        {
            StartFromSetup();
            Tick(World.Settings.Waves.FirstWaveDelay + 0.5f);
            Assert.That(World.LivingEnemies, Is.Not.Empty, "the first wave did not come");

            foreach (int enemy in World.LivingEnemies.ToList())
                Slay(enemy);

            Assert.That(_host.State, Is.EqualTo(GameHostState.Running));
            Assert.That(World.Kills, Is.GreaterThan(0));
            Assert.That(World.TotalExperience, Is.GreaterThan(0));
            Assert.That(ItemCount("goblin_ear") + ItemCount("brute_tusk"), Is.GreaterThan(0), "slain goblins drop loot");
            Assert.That(_log.Snapshot().Where(entry => entry.Severity >= LogSeverity.Error), Is.Empty);
        }

        [Test]
        public void ThreeKillsInARow_GiveBonusLoot()
        {
            StartFromSetup(new KillStreakSettings { KillsForBonus = 3, BonusItemId = "valley_medal", BonusQuantity = 1 });

            for (int i = 0; i < 3; i++)
                Slay(World.SpawnEnemy("goblin"));

            Assert.That(Streak.Current, Is.EqualTo(3));
            Assert.That(_rewards.Select(r => r.Streak), Is.EqualTo(new[] { 3 }));
            Assert.That(ItemCount("valley_medal"), Is.EqualTo(1));
            Assert.That(World.Log, Has.Some.Contains("Kill streak x3"));
        }

        [Test]
        public void BeingHurt_BreaksTheStreak()
        {
            StartFromSetup(new KillStreakSettings { KillsForBonus = 3, BonusItemId = "valley_medal" });
            Slay(World.SpawnEnemy("goblin"));
            Slay(World.SpawnEnemy("goblin"));

            int goblin = World.SpawnEnemy("goblin");
            World.Attack(goblin, World.PlayerId);
            Slay(goblin);

            Assert.That(Streak.Current, Is.EqualTo(1));
            Assert.That(Streak.Best, Is.EqualTo(2));
            Assert.That(_rewards, Is.Empty);
            Assert.That(ItemCount("valley_medal"), Is.Zero);
        }

        [Test]
        public void ZeroKillsForBonus_TurnsTheRuleOff()
        {
            StartFromSetup(new KillStreakSettings { KillsForBonus = 0 });

            for (int i = 0; i < 4; i++)
                Slay(World.SpawnEnemy("goblin"));

            Assert.That(Streak.Current, Is.EqualTo(4));
            Assert.That(_rewards, Is.Empty);
        }
    }
}
