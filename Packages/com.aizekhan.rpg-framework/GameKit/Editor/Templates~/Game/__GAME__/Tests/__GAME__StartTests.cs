using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Logging;

namespace __GAME__.Tests
{
    /// <summary>
    /// Boots the whole game the way the scene does (GameHost + __GAME__Composition), without Unity.
    /// </summary>
    public sealed class __GAME__StartTests
    {
        private MemoryLogSink _log;
        private Logger _logger;
        private GameHost _host;

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

        [Test]
        public void Game_StartsAndRunsWithoutErrors()
        {
            _host.Start(__GAME__Composition.Configure);
            for (int frame = 0; frame < 10; frame++)
                _host.Update(1f / 30f);

            Assert.That(_host.State, Is.EqualTo(GameHostState.Running));
            Assert.That(_host.Container.Resolve<__GAME__Session>().IsStarted, Is.True);
            Assert.That(_log.Snapshot().Where(entry => entry.Severity >= LogSeverity.Error), Is.Empty);
        }
    }
}
