using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The action RPG rules without Unity, through the real framework modules: kills give experience, loot and quest
    /// progress; quests turn in and reward; level-ups grow stats; waves come, grow and wait for a dead player.
    /// </summary>
    public sealed class ActorWorldTests
    {
        private const string Goblin = "goblin";
        private const string Ear = "goblin_ear";
        private const string Medal = "medal";
        private const string HuntQuest = "hunt";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _module;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private readonly List<ActorSpawnedEvent> _spawned = new List<ActorSpawnedEvent>();
        private readonly List<ActorDespawnedEvent> _despawned = new List<ActorDespawnedEvent>();
        private readonly List<WaveStartedEvent> _waves = new List<WaveStartedEvent>();

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData
                {
                    Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f,
                    HealthPerLevel = 20f, AttackPerLevel = 3f, DefensePerLevel = 1f
                },
                Waves = new WaveSettings { FirstWaveDelay = 1f, DelayBetweenWaves = 2f, FirstWaveSize = 2, AddedPerWave = 1, MaxWaveSize = 3 },
                Seed = 7
            };
            _settings.Enemies.Add(new CharacterData
            {
                Id = Goblin, DisplayName = "Goblin", MaxHealth = 30f, Attack = 6f, Defense = 2f, ExperienceReward = 40,
                LootTableId = Goblin
            });
            _settings.StartingQuests.Add(HuntQuest);

            _content = new ActionRpgContent();
            _content.Items.Add(new Item(Ear, "Goblin Ear", "Proof", ItemType.Crafting, maxStackSize: 99));
            _content.Items.Add(new Item(Medal, "Medal", "For the hunt", ItemType.Quest));
            _content.LootTables.Add(LootTableBuilder.Create(Goblin, "Goblin").AddGuaranteedDrop(Ear).WithDrops(0, 0).Build());
            _content.Quests.Add(new Quest(HuntQuest, "The Hunt", "Slay 2 goblins",
                new List<IQuestObjective> { new QuestObjective("slay", ObjectiveType.Kill, Goblin, 2, "Slay goblins") },
                new List<IQuestReward> { new QuestReward(RewardType.Experience, null, 100), new QuestReward(RewardType.Item, Medal, 1) },
                new List<string>(), minimumLevel: 1, isRepeatable: false));

            _spawned.Clear();
            _despawned.Clear();
            _waves.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame()
        {
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));
            var bus = _host.Container.Resolve<IEventBus>();
            bus.Subscribe<ActorSpawnedEvent>(_spawned.Add);
            bus.Subscribe<ActorDespawnedEvent>(_despawned.Add);
            bus.Subscribe<WaveStartedEvent>(_waves.Add);
        }

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds; time += 0.1f)
                _host.Update(0.1f);
        }

        private void Slay(int enemy)
        {
            for (int round = 0; round < 50 && World.Get(enemy) != null; round++)
                World.Attack(World.PlayerId, enemy);
        }

        [Test]
        public void Start_CreatesThePlayerWithStartingQuests()
        {
            StartGame();

            Assert.That(World.Player, Is.Not.Null);
            Assert.That(World.Player.IsPlayer, Is.True);
            Assert.That(World.Player.MaxHealth, Is.EqualTo(100f));
            Assert.That(World.Level, Is.EqualTo(1));
            Assert.That(World.ActiveQuests.Select(q => q.Quest.Id), Is.EqualTo(new[] { HuntQuest }));
            Assert.That(World.QuestTargetOf(World.PlayerId), Is.EqualTo("hero"));
        }

        [Test]
        public void Kill_GivesExperienceLootAndQuestProgress()
        {
            StartGame();
            int goblin = World.SpawnEnemy(Goblin);

            Slay(goblin);

            Assert.That(World.Get(goblin), Is.Null);
            Assert.That(World.Kills, Is.EqualTo(1));
            Assert.That(World.TotalExperience, Is.EqualTo(40));
            Assert.That(World.Inventory.Count, Is.EqualTo(1));
            var progress = World.ActiveQuests.Single().Progress;
            Assert.That(progress.ObjectiveProgress["slay"], Is.EqualTo(1));
            Assert.That(_despawned.Select(d => d.CharacterId), Is.EqualTo(new[] { Goblin }));
        }

        [Test]
        public void Kill_IsAnnouncedWithTheCharacterAndKiller()
        {
            StartGame();
            var killed = new List<ActorKilledEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorKilledEvent>(killed.Add);
            int goblin = World.SpawnEnemy(Goblin);

            Slay(goblin);

            Assert.That(killed.Count, Is.EqualTo(1));
            Assert.That(killed[0].EntityId, Is.EqualTo(goblin));
            Assert.That(killed[0].CharacterId, Is.EqualTo(Goblin));
            Assert.That(killed[0].ByPlayer, Is.True);
        }

        [Test]
        public void GiveItem_FillsTheBackpackAndRefusesUnknownItems()
        {
            StartGame();

            Assert.That(World.GiveItem(Ear, 3), Is.True);
            Assert.That(World.Inventory.Count, Is.EqualTo(3));
            Assert.That(World.GiveItem("nothing"), Is.False);
            Assert.That(World.ExperienceForNextLevel, Is.GreaterThan(World.TotalExperience));
        }

        [Test]
        public void FinishedQuest_IsTurnedInAndRewarded()
        {
            StartGame();

            Slay(World.SpawnEnemy(Goblin));
            Slay(World.SpawnEnemy(Goblin));

            Assert.That(World.CompletedQuests, Is.EqualTo(new[] { HuntQuest }));
            Assert.That(World.ActiveQuests, Is.Empty);
            Assert.That(World.TotalExperience, Is.EqualTo(40 + 40 + 100));
            Assert.That(World.Inventory.GetAllItems().Any(i => i.ItemId == Medal), Is.True);
        }

        [Test]
        public void LevelUp_GrowsStatsAndHeals()
        {
            StartGame();
            World.SpawnEnemy(Goblin);
            World.Attack(World.LivingEnemies.First(), World.PlayerId);
            Assume.That(World.Player.Health, Is.LessThan(World.Player.MaxHealth));

            Slay(World.LivingEnemies.First());
            Slay(World.SpawnEnemy(Goblin));

            Assert.That(World.Level, Is.GreaterThan(1));
            float expected = 100f + 20f * (World.Level - 1);
            Assert.That(World.Player.MaxHealth, Is.EqualTo(expected));
            Assert.That(World.Player.Health, Is.EqualTo(World.Player.MaxHealth));
            Assert.That(World.Player.Attack, Is.EqualTo(50f + 3f * (World.Level - 1)));
        }

        [Test]
        public void Waves_StartGrowAndWaitForTheDeadPlayer()
        {
            StartGame();

            Tick(0.5f);
            Assert.That(World.LivingEnemies, Is.Empty);
            Tick(0.7f);
            Assert.That(World.CurrentWave, Is.EqualTo(1));
            Assert.That(World.LivingEnemies.Count(), Is.EqualTo(2));

            foreach (int enemy in World.LivingEnemies.ToList())
                Slay(enemy);
            Tick(2.2f);
            Assert.That(World.CurrentWave, Is.EqualTo(2));
            Assert.That(World.LivingEnemies.Count(), Is.EqualTo(3));

            foreach (int enemy in World.LivingEnemies.ToList())
                Slay(enemy);
            _host.Container.Resolve<ILifeStateManager>().Kill(World.PlayerId, -1, "test");
            Tick(5f);
            Assert.That(World.CurrentWave, Is.EqualTo(2), "no waves while the player is dead");

            Assert.That(World.RevivePlayer(), Is.True);
            Assert.That(World.Player.Health, Is.EqualTo(World.Player.MaxHealth));
            Tick(2.2f);
            Assert.That(World.CurrentWave, Is.EqualTo(3));
            Assert.That(_waves.Select(w => w.Enemies), Is.EqualTo(new[] { 2, 3, 3 }));
        }

        [Test]
        public void Spawns_AreAnnouncedAndShutdownDespawnsEveryone()
        {
            StartGame();
            World.SpawnEnemy(Goblin);

            _host.Dispose();

            Assert.That(_despawned.Count, Is.EqualTo(2));
            Assert.That(_despawned.Count(d => d.IsPlayer), Is.EqualTo(1));
        }

        [Test]
        public void DisabledWaves_SpawnNothing()
        {
            _settings.Waves.Enabled = false;
            StartGame();

            Tick(10f);

            Assert.That(World.CurrentWave, Is.Zero);
        }

        [Test]
        public void Validate_GoodSettings_HasNoProblems()
        {
            Assert.That(_settings.Validate(_content), Is.Empty);
        }

        [Test]
        public void Validate_ExplainsBrokenContent()
        {
            _settings.Enemies[0].LootTableId = "missing_table";
            _settings.StartingQuests.Add("missing_quest");
            _content.Items.RemoveAll(i => i.ItemId == Medal);
            _content.Quests.Add(new Quest("other", "Other", "",
                new List<IQuestObjective> { new QuestObjective("o", ObjectiveType.Kill, "dragon", 1, "Slay a dragon") },
                new List<IQuestReward>(), new List<string>(), minimumLevel: 1, isRepeatable: false));

            var problems = _settings.Validate(_content);

            Assert.That(problems, Has.Some.Contains("missing_table"));
            Assert.That(problems, Has.Some.Contains("missing_quest"));
            Assert.That(problems, Has.Some.Contains("'medal'"));
            Assert.That(problems, Has.Some.Contains("'dragon'"));
        }

        [Test]
        public void Validate_NoPlayerAndWavesWithoutEnemies()
        {
            _settings.Player = null;
            _settings.Enemies.Clear();

            var problems = _settings.Validate(_content);

            Assert.That(problems, Has.Some.Contains("No player"));
            Assert.That(problems, Has.Some.Contains("no enemies"));
        }
    }
}
