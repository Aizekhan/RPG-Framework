using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Logging;

namespace ReferenceGame.Tests
{
    /// <summary>
    /// The real-time loop of Goblin Valley without Unity: waves start when the valley is quiet, grow, pause while the
    /// hero is dead, and every combatant entering or leaving the valley is announced on the bus for the views.
    /// </summary>
    public sealed class GoblinValleyWaveTests
    {
        private Logger _logger;
        private GameHost _host;
        private GoblinValleyModule _module;
        private GoblinValleyOptions _options;
        private readonly List<CombatantSpawnedEvent> _spawned = new List<CombatantSpawnedEvent>();
        private readonly List<CombatantDespawnedEvent> _despawned = new List<CombatantDespawnedEvent>();
        private readonly List<WaveStartedEvent> _waves = new List<WaveStartedEvent>();

        private GoblinValleyGame Game => _module.Game;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _options = new GoblinValleyOptions
            {
                Seed = 99, FirstWaveDelay = 1f, DelayBetweenWaves = 2f, FirstWaveSize = 2, GoblinsAddedPerWave = 1,
                MaxWaveSize = 3
            };
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
            _host.Start(builder => _module = GoblinValleyComposition.Configure(builder, _options));
            var bus = _host.Container.Resolve<IEventBus>();
            bus.Subscribe<CombatantSpawnedEvent>(_spawned.Add);
            bus.Subscribe<CombatantDespawnedEvent>(_despawned.Add);
            bus.Subscribe<WaveStartedEvent>(_waves.Add);
        }

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds; time += 0.1f)
                _host.Update(0.1f);
        }

        private void KillEveryGoblin()
        {
            for (int round = 0; round < 200 && Game.LivingGoblins.Any(); round++)
                Game.Attack(Game.PlayerId, Game.LivingGoblins.First());
        }

        [Test]
        public void FirstWave_StartsAfterTheDelay()
        {
            StartGame();

            Tick(0.5f);
            Assert.That(Game.LivingGoblins, Is.Empty);

            Tick(0.7f);
            Assert.That(Game.CurrentWave, Is.EqualTo(1));
            Assert.That(Game.LivingGoblins.Count(), Is.EqualTo(2));
            Assert.That(_waves.Select(w => (w.Wave, w.Goblins)), Is.EqualTo(new[] { (1, 2) }));
        }

        [Test]
        public void Waves_GrowUpToTheMaximum()
        {
            StartGame();
            var sizes = new List<int>();

            for (int wave = 0; wave < 4; wave++)
            {
                Tick(2.2f);
                sizes.Add(Game.LivingGoblins.Count());
                KillEveryGoblin();
            }

            Assert.That(sizes, Is.EqualTo(new[] { 2, 3, 3, 3 }));
            Assert.That(Game.GoblinsKilled, Is.EqualTo(11));
        }

        [Test]
        public void NoWaves_WhileTheHeroIsDead()
        {
            StartGame();
            _host.Container.Resolve<RPGFramework.Combat.ILifeStateManager>().Kill(Game.PlayerId, -1, "test");

            Tick(5f);
            Assert.That(Game.CurrentWave, Is.EqualTo(0));

            Game.RevivePlayer();
            Tick(1.2f);
            Assert.That(Game.CurrentWave, Is.EqualTo(1));
        }

        [Test]
        public void DisabledWaves_LeaveTheValleyEmpty()
        {
            _options.WavesEnabled = false;
            StartGame();

            Tick(10f);

            Assert.That(Game.CurrentWave, Is.EqualTo(0));
            Assert.That(Game.LivingGoblins, Is.Empty);
        }

        [Test]
        public void CombatantsEnteringAndLeaving_AreAnnounced()
        {
            StartGame();

            // The hero spawned while the game started, before anyone could subscribe: views read Combatants
            Assert.That(Game.Combatants.Select(c => c.Kind), Is.EqualTo(new[] { GoblinValleyContent.PlayerKind }));

            int goblin = Game.SpawnGoblin();
            Assert.That(_spawned.Select(e => (e.EntityId, e.Kind)), Is.EqualTo(new[] { (goblin, GoblinValleyContent.GoblinKind) }));

            KillEveryGoblin();
            Assert.That(_despawned.Select(e => (e.EntityId, e.Kind)), Is.EqualTo(new[] { (goblin, GoblinValleyContent.GoblinKind) }));
            Assert.That(Game.Get(goblin), Is.Null);
        }

        [Test]
        public void Shutdown_AnnouncesEveryoneLeaving()
        {
            StartGame();
            Game.SpawnGoblin();
            var bus = _host.Container.Resolve<IEventBus>();
            var left = new List<string>();
            bus.Subscribe<CombatantDespawnedEvent>(e => left.Add(e.Kind));

            _module.Game.Dispose();

            Assert.That(left, Is.EquivalentTo(new[] { GoblinValleyContent.PlayerKind, GoblinValleyContent.GoblinKind }));
        }
    }
}
