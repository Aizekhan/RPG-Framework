using System.Diagnostics;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Logging;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The action RPG rules with a thousand enemies in the world (the framework's 1000-entity target): a frame of
    /// systems plus a hundred hits stays well inside a 60 fps frame, and spawning them is quick. The benchmark prints
    /// its numbers to the test output (budgets are generous: they catch regressions, not jitter).
    /// </summary>
    public sealed class ActorWorldPerformanceTests
    {
        private const string Grunt = "grunt";
        private const int Enemies = 1000;

        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _module;

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            var settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 1e9f, Attack = 5f, Defense = 1000f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 3
            };
            settings.Enemies.Add(new CharacterData { Id = Grunt, DisplayName = "Grunt", MaxHealth = 1e6f, Attack = 4f, Defense = 1f });
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, settings, new ActionRpgContent()));
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        [Test]
        public void Benchmark_AThousandEnemies_FramesWithAHundredHits()
        {
            var spawnClock = Stopwatch.StartNew();
            var enemies = new int[Enemies];
            for (int i = 0; i < Enemies; i++)
                enemies[i] = World.SpawnEnemy(Grunt);
            spawnClock.Stop();

            const int frames = 120;
            int next = 0;
            var clock = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
            {
                _host.Update(1f / 60f);
                for (int hit = 0; hit < 100; hit++)
                {
                    int enemy = enemies[next++ % Enemies];
                    World.Attack(enemy, World.PlayerId);
                    World.Attack(World.PlayerId, enemy);
                }
            }
            clock.Stop();

            double perFrame = clock.Elapsed.TotalMilliseconds / frames;
            TestContext.WriteLine($"[Benchmark] ActorWorld: {Enemies} enemies, systems + 200 attacks: {perFrame:0.000} ms per frame; " +
                                  $"spawning {Enemies}: {spawnClock.Elapsed.TotalMilliseconds:0.0} ms");
            Assert.That(World.Player.IsAlive(), Is.True);
            Assert.That(perFrame, Is.LessThan(8.0), "half of a 60 fps frame");
        }
    }
}
