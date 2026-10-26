using System.Linq;
using NUnit.Framework;
using RPGFramework.ActionRpg;
using RPGFramework.Bootstrap;
using RPGFramework.Logging;
using UnityEditor;

namespace __GAME__.Tests
{
    /// <summary>
    /// Boots the whole game the way the scene does (the Game Setup asset + __GAME__Composition), without the scene.
    /// </summary>
    public sealed class __GAME__StartTests
    {
        public const string SetupPath = "Assets/__GAME__/Content/__GAME__Setup.asset";

        private MemoryLogSink _log;
        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _actionRpg;

        private ActorWorld World => _actionRpg.World;

        [SetUp]
        public void SetUp()
        {
            _log = new MemoryLogSink(1024);
            _logger = new Logger(LogSeverity.Debug, _log);
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
            var setup = AssetDatabase.LoadAssetAtPath<GameSetup>(SetupPath);
            Assert.That(setup, Is.Not.Null, $"{SetupPath} is missing");
            Assert.That(setup.Validate(), Is.Empty, "the Game Setup has problems");
            _host.Start(builder => _actionRpg = __GAME__Composition.ConfigureWithActionRpg(builder, setup.BuildSettings(), setup.BuildContent()));
        }

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds; time += 0.1f)
                _host.Update(0.1f);
        }

        [Test]
        public void Game_StartsAndRunsWithoutErrors()
        {
            StartGame();
            Tick(1f);

            Assert.That(_host.State, Is.EqualTo(GameHostState.Running));
            Assert.That(_host.Container.Resolve<__GAME__Session>().IsStarted, Is.True);
            Assert.That(World.Player, Is.Not.Null);
            Assert.That(_log.Snapshot().Where(entry => entry.Severity >= LogSeverity.Error), Is.Empty);
        }

        [Test]
        public void Waves_ComeAndSlainEnemiesGiveExperience()
        {
            StartGame();
            Tick(World.Settings.Waves.FirstWaveDelay + 0.5f);
            Assert.That(World.LivingEnemies, Is.Not.Empty, "the first wave did not come");

            foreach (int enemy in World.LivingEnemies.ToList())
            {
                for (int hit = 0; hit < 100 && World.Get(enemy) != null; hit++)
                    World.Attack(World.PlayerId, enemy);
            }

            Assert.That(World.Kills, Is.GreaterThan(0));
            Assert.That(World.TotalExperience, Is.GreaterThan(0));
            Assert.That(_host.Container.Resolve<__GAME__Session>().BestWave, Is.EqualTo(1));
        }
    }
}
