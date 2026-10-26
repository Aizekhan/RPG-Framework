using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Logging;

namespace __GAME__.Tests
{
    /// <summary>
    /// __FEATURE__ inside the whole game: booted through __GAME__Composition, driven through its rules,
    /// observed through the bus.
    /// </summary>
    public sealed class __FEATURE__Tests
    {
        private MemoryLogSink _log;
        private Logger _logger;
        private GameHost _host;
        private __FEATURE__Rules _rules;
        private readonly List<__FEATURE__ChangedEvent> _changes = new List<__FEATURE__ChangedEvent>();

        [SetUp]
        public void SetUp()
        {
            _log = new MemoryLogSink(1024);
            _logger = new Logger(LogSeverity.Debug, _log);
            _host = new GameHost(_logger);
            _host.Start(__GAME__Composition.Configure);
            _rules = _host.Container.Resolve<__FEATURE__Rules>();
            _changes.Clear();
            _host.Container.Resolve<IEventBus>().Subscribe<__FEATURE__ChangedEvent>(_changes.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        [Test]
        public void Module_IsPartOfTheGame()
        {
            Assert.That(_host.Composition.Modules.Select(m => m.Name), Has.Member(__FEATURE__Module.ModuleName));
        }

        [Test]
        public void Add_ChangesTheValueAndAnnouncesIt()
        {
            _rules.Add(3);

            Assert.That(_rules.Value, Is.EqualTo(3));
            Assert.That(_changes.Select(c => (c.OldValue, c.NewValue)), Is.EqualTo(new[] { (0, 3) }));
            Assert.That(_log.Snapshot().Where(entry => entry.Severity >= LogSeverity.Error), Is.Empty);
        }

        [Test]
        public void Add_RejectsNonPositiveAmounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.Add(0));
            Assert.That(_changes, Is.Empty);
        }
    }
}
