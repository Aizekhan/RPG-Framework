using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Core.Composition;
using RPGFramework.DI;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Systems;

namespace RPGFramework.Bootstrap.Tests
{
    public readonly struct DamageDealt : IEvent
    {
        public DamageDealt(int amount) => Amount = amount;
        public int Amount { get; }
    }

    public sealed class Journal
    {
        public List<string> Entries { get; } = new List<string>();
    }

    /// <summary>
    /// Sample gameplay system built only from DI: counts damage and emits one event per frame.
    /// </summary>
    public sealed class DamageSystem : SystemBase, IUpdateSystem
    {
        private readonly Journal _journal;

        public DamageSystem(ILogger logger, IEventBus eventBus, Journal journal) : base(logger, eventBus)
        {
            _journal = journal;
        }

        public int TotalDamage { get; private set; }

        public void Update(float deltaTime) => Enqueue(new DamageDealt(10));

        protected override void OnInitialize()
        {
            _journal.Entries.Add("damage.init");
            Subscribe<DamageDealt>(e => TotalDamage += e.Amount);
        }

        protected override void OnDispose() => _journal.Entries.Add("damage.dispose");
    }

    public sealed class CombatModule : GameFeatureModule
    {
        public override string Name => "Combat";

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<Journal>())
                Container.RegisterInstance(new Journal());
            AddSystem<DamageSystem>();
        }

        protected override void OnInitialize() => Container.Resolve<Journal>().Entries.Add("combat.init");

        protected override void OnShutdown() => Container.Resolve<Journal>().Entries.Add("combat.shutdown");
    }

    public sealed class UiModule : GameFeatureModule
    {
        public override string Name => "UI";

        public override IEnumerable<Type> Dependencies => base.Dependencies.Concat(new[] { typeof(CombatModule) });

        protected override void OnConfigure()
        {
        }

        protected override void OnInitialize() => Container.Resolve<Journal>().Entries.Add("ui.init");

        protected override void OnShutdown() => Container.Resolve<Journal>().Entries.Add("ui.shutdown");
    }

    public sealed class FailingModule : GameFeatureModule
    {
        public override string Name => "Failing";

        public override IEnumerable<Type> Dependencies => base.Dependencies.Concat(new[] { typeof(CombatModule) });

        protected override void OnConfigure()
        {
        }

        protected override void OnInitialize() => throw new InvalidOperationException("module init failed");
    }

    public sealed class NeedsUnregistered
    {
        public NeedsUnregistered(IDisposable missing)
        {
        }
    }

    public sealed class BrokenGraphModule : BaseFeatureModule
    {
        public bool Initialized;
        public override string Name => "Broken";

        protected override void OnConfigure() => Container.RegisterSingleton<NeedsUnregistered, NeedsUnregistered>();

        public override void Initialize() => Initialized = true;
    }

    public sealed class GameHostTests
    {
        private MemoryLogSink _sink;
        private GameHost _host;

        [SetUp]
        public void SetUp()
        {
            _sink = new MemoryLogSink();
            _host = new GameHost(new Logger(LogSeverity.Trace, _sink));
        }

        [TearDown]
        public void TearDown() => _host.Dispose();

        [Test]
        public void Start_RegistersFrameworkCoreServices()
        {
            _host.Start(null);

            var c = _host.Container;
            Assert.That(_host.State, Is.EqualTo(GameHostState.Running));
            Assert.That(c.Resolve<IEventPublisher>(), Is.SameAs(c.Resolve<IEventBus>()));
            Assert.That(c.Resolve<IPhaseProvider>(), Is.SameAs(c.Resolve<PhaseTracker>()));
            Assert.That(c.Resolve<IEntityManager>(), Is.SameAs(_host.World.Entities));
            Assert.That(c.Resolve<ISystemRegistry>(), Is.SameAs(_host.World.Systems));
            Assert.That(c.Resolve<ILogger>(), Is.SameAs(_host.Logger));
            Assert.That(_host.World.State, Is.EqualTo(WorldState.Initialized));
            Assert.That(_host.World.Systems.Get<EventPumpSystem>(), Is.Not.Null);
        }

        [Test]
        public void GameModule_SystemsAreBuiltByDI_AndTicked_EventsFlowThroughPump()
        {
            _host.Start(b => b.UseModule<CombatModule>());
            var damage = _host.World.Systems.Get<DamageSystem>();

            _host.Update(0.016f); // pump runs first (nothing queued), damage system enqueues
            Assert.That(damage.TotalDamage, Is.Zero);

            _host.Update(0.016f); // pump delivers last frame's event
            Assert.That(damage.TotalDamage, Is.EqualTo(10));
        }

        [Test]
        public void Modules_InitializeInDependencyOrder_AndShutDownInReverse_AfterSystems()
        {
            var container = new DIContainer();
            var journal = new Journal();
            container.RegisterInstance(journal);

            _host.Start(b => b.UseModule<UiModule>().UseModule<CombatModule>(), container);
            _host.Dispose();

            CollectionAssert.AreEqual(
                new[] { "combat.init", "ui.init", "damage.init", "damage.dispose", "ui.shutdown", "combat.shutdown" },
                journal.Entries);
            Assert.That(_host.State, Is.EqualTo(GameHostState.Stopped));
            Assert.That(_host.Composition.IsShutDown, Is.True);
        }

        [Test]
        public void FailingModule_RollsBackStartedModules_AndHostIsFailed()
        {
            var container = new DIContainer();
            var journal = new Journal();
            container.RegisterInstance(journal);

            Assert.Throws<InvalidOperationException>(() =>
                _host.Start(b => b.UseModule<CombatModule>().UseModule<FailingModule>(), container));

            Assert.That(_host.State, Is.EqualTo(GameHostState.Failed));
            CollectionAssert.AreEqual(new[] { "combat.init", "combat.shutdown" }, journal.Entries);
            Assert.That(_sink.Snapshot().Any(e => e.Severity == LogSeverity.Critical), Is.True);
        }

        [Test]
        public void BrokenObjectGraph_FailsBeforeAnyModuleInitializes()
        {
            var broken = new BrokenGraphModule();

            var ex = Assert.Throws<InvalidOperationException>(() => _host.Start(b => b.UseModule(broken)));

            StringAssert.Contains("IDisposable", ex.Message);
            Assert.That(broken.Initialized, Is.False);
            Assert.That(_host.State, Is.EqualTo(GameHostState.Failed));
        }

        [Test]
        public void ValidationCanBeDisabled()
        {
            var broken = new BrokenGraphModule();

            _host.Start(b =>
            {
                b.ValidateContainer = false;
                b.UseModule(broken);
            });

            Assert.That(broken.Initialized, Is.True);
        }

        [Test]
        public void StartTwice_Throws_TickWhenNotRunning_IsIgnored()
        {
            Assert.DoesNotThrow(() => _host.Update(1f));
            _host.Start(null);

            Assert.Throws<InvalidOperationException>(() => _host.Start(null));

            _host.Dispose();
            Assert.DoesNotThrow(() => _host.Update(1f));
            Assert.DoesNotThrow(() => _host.Dispose());
        }

        [Test]
        public void Options_AreApplied()
        {
            var host = new GameHost(options: new FrameworkCoreOptions
            {
                WorldName = "Arena",
                InitialPhaseId = "Combat",
                MaxConsecutiveSystemErrors = 3
            });

            host.Start(null);
            try
            {
                Assert.That(host.World.Name, Is.EqualTo("Arena"));
                Assert.That(host.Container.Resolve<IPhaseProvider>().CurrentPhaseId, Is.EqualTo("Combat"));
                Assert.That(((SystemGroup)host.World.Systems).MaxConsecutiveErrors, Is.EqualTo(3));
            }
            finally
            {
                host.Dispose();
            }
        }
    }

    public sealed class GameCompositionBuilderTests
    {
        [Test]
        public void MissingDependencyModule_Throws()
        {
            var builder = new GameCompositionBuilder(new DIContainer());
            builder.UseModule<CombatModule>();

            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            StringAssert.Contains("FrameworkCoreModule", ex.Message);
        }

        [Test]
        public void DuplicateModuleType_IsIgnored_AndBuildOnlyOnce()
        {
            var builder = new GameCompositionBuilder(new DIContainer());
            builder.UseModule<FrameworkCoreModule>().UseModule<FrameworkCoreModule>();

            Assert.That(builder.HasModule<FrameworkCoreModule>(), Is.True);
            var composition = builder.Build();

            Assert.That(composition.Modules, Has.Count.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => builder.Build());
            composition.Shutdown();
            Assert.DoesNotThrow(() => composition.Shutdown());
        }
    }
}
