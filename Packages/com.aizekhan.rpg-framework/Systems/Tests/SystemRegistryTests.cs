using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Logging;

namespace RPGFramework.Systems.Tests
{
    internal sealed class RecordingSystem : IUpdateSystem, IFixedUpdateSystem, ILateUpdateSystem
    {
        private readonly string _name;
        private readonly List<string> _log;

        public RecordingSystem(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public Action OnUpdate { get; set; }
        public bool ThrowOnInitialize { get; set; }

        public void Initialize()
        {
            if (ThrowOnInitialize)
                throw new InvalidOperationException("init failed");
            _log.Add($"{_name}.init");
        }

        public void Update(float deltaTime)
        {
            _log.Add($"{_name}.update");
            OnUpdate?.Invoke();
        }

        public void FixedUpdate(float fixedDeltaTime) => _log.Add($"{_name}.fixed");
        public void LateUpdate(float deltaTime) => _log.Add($"{_name}.late");
        public void Dispose() => _log.Add($"{_name}.dispose");
    }

    internal sealed class InitOnlySystem : ISystem
    {
        public int InitCount;
        public void Initialize() => InitCount++;
        public void Dispose() { }
    }

    internal sealed class PhaseOnlySystem : IUpdateSystem, IPhaseFilteredSystem
    {
        private readonly string _phase;
        public int Updates;
        public PhaseOnlySystem(string phase) => _phase = phase;
        public bool IsActiveInPhase(string phaseId) => phaseId == _phase;
        public void Initialize() { }
        public void Update(float deltaTime) => Updates++;
        public void Dispose() { }
    }

    public sealed class SystemRegistryTests
    {
        private List<string> _log;
        private MemoryLogSink _sink;
        private SystemRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _sink = new MemoryLogSink();
            _registry = new SystemRegistry(new Logger(LogSeverity.Trace, _sink));
        }

        private RecordingSystem Add(string name, int priority = 0)
        {
            var system = new RecordingSystem(name, _log);
            _registry.Register(system, priority);
            return system;
        }

        [Test]
        public void Systems_RunByPriorityDescending_StableForEqualPriority()
        {
            Add("low", 0);
            Add("mid1", 50);
            Add("high", 100);
            Add("mid2", 50);
            _registry.Initialize();
            _log.Clear();

            _registry.Update(0.016f);

            CollectionAssert.AreEqual(new[] { "high.update", "mid1.update", "mid2.update", "low.update" }, _log);
        }

        [Test]
        public void Stages_CallOnlyTheirOwnMethods()
        {
            Add("a");
            _registry.Initialize();
            _log.Clear();

            _registry.FixedUpdate(0.02f);
            _registry.LateUpdate(0.016f);

            CollectionAssert.AreEqual(new[] { "a.fixed", "a.late" }, _log);
        }

        [Test]
        public void SystemsWithoutStageInterfaces_AreInitializedButNeverTicked()
        {
            var system = new InitOnlySystem();
            _registry.Register(system);
            _registry.Initialize();

            Assert.DoesNotThrow(() => _registry.Update(1f));
            Assert.That(system.InitCount, Is.EqualTo(1));
        }

        [Test]
        public void Initialize_IsIdempotent()
        {
            var system = new InitOnlySystem();
            _registry.Register(system);

            _registry.Initialize();
            _registry.Initialize();

            Assert.That(system.InitCount, Is.EqualTo(1));
        }

        [Test]
        public void Update_BeforeInitialize_SkipsSystems()
        {
            Add("a");

            _registry.Update(1f);

            Assert.That(_log, Is.Empty);
        }

        [Test]
        public void RegisterAfterInitialize_InitializesImmediately()
        {
            _registry.Initialize();

            Add("late");

            CollectionAssert.AreEqual(new[] { "late.init" }, _log);
        }

        [Test]
        public void DuplicateInstance_IsRejected()
        {
            var system = Add("a");

            Assert.That(_registry.Register(system), Is.False);
            Assert.That(_registry.Systems, Has.Count.EqualTo(1));
        }

        [Test]
        public void DuplicateGroupName_IsRejected()
        {
            Assert.That(_registry.Register(new SystemGroup("Combat")), Is.True);
            Assert.That(_registry.Register(new SystemGroup("Combat")), Is.False);
        }

        [Test]
        public void ThrowingUpdate_IsIsolated_AndCounted()
        {
            var bad = Add("bad", 10);
            bad.OnUpdate = () => throw new InvalidOperationException("boom");
            Add("good");
            _registry.Initialize();
            _log.Clear();

            Assert.DoesNotThrow(() => _registry.Update(1f));

            CollectionAssert.AreEqual(new[] { "bad.update", "good.update" }, _log);
            Assert.That(_registry.GetSystemInfos().Single(i => i.System == bad).ErrorCount, Is.EqualTo(1));
            Assert.That(_sink.Snapshot().Any(e => e.Severity == LogSeverity.Error), Is.True);
        }

        [Test]
        public void MaxConsecutiveErrors_DisablesFailingSystem()
        {
            _registry.MaxConsecutiveErrors = 2;
            var bad = Add("bad");
            bad.OnUpdate = () => throw new InvalidOperationException("boom");
            _registry.Initialize();

            for (int i = 0; i < 5; i++)
                _registry.Update(1f);

            Assert.That(_log.Count(x => x == "bad.update"), Is.EqualTo(2));
            Assert.That(_registry.GetSystemInfos()[0].IsFaulted, Is.True);
        }

        [Test]
        public void FailedInitialize_MarksFaulted_SystemNeverRuns_OthersDo()
        {
            var bad = Add("bad", 10);
            bad.ThrowOnInitialize = true;
            Add("good");

            _registry.Initialize();
            _registry.Update(1f);

            CollectionAssert.AreEqual(new[] { "good.init", "good.update" }, _log);
            var info = _registry.GetSystemInfos().Single(i => i.System == bad);
            Assert.That(info.IsFaulted, Is.True);
            Assert.That(info.IsInitialized, Is.False);
        }

        [Test]
        public void SetActive_False_SkipsSystem()
        {
            var a = Add("a");
            _registry.Initialize();
            _log.Clear();

            Assert.That(_registry.SetActive(a, false), Is.True);
            _registry.Update(1f);
            Assert.That(_log, Is.Empty);
            Assert.That(_registry.IsActive(a), Is.False);

            _registry.SetActive(a, true);
            _registry.Update(1f);
            CollectionAssert.AreEqual(new[] { "a.update" }, _log);
        }

        [Test]
        public void UnregisterDuringUpdate_TakesEffectImmediately()
        {
            RecordingSystem second = null;
            var first = Add("first", 10);
            second = Add("second");
            first.OnUpdate = () => _registry.Unregister(second);
            _registry.Initialize();
            _log.Clear();

            _registry.Update(1f);

            CollectionAssert.AreEqual(new[] { "first.update", "second.dispose" }, _log);
        }

        [Test]
        public void RegisterDuringUpdate_RunsFromNextFrame()
        {
            var first = Add("first");
            bool added = false;
            first.OnUpdate = () =>
            {
                if (added)
                    return;
                added = true;
                Add("new", 100);
            };
            _registry.Initialize();
            _log.Clear();

            _registry.Update(1f);
            CollectionAssert.AreEqual(new[] { "first.update", "new.init" }, _log);

            _log.Clear();
            _registry.Update(1f);
            CollectionAssert.AreEqual(new[] { "new.update", "first.update" }, _log);
        }

        [Test]
        public void Unregister_WithoutDispose_DoesNotDispose()
        {
            var a = Add("a");
            _registry.Initialize();
            _log.Clear();

            Assert.That(_registry.Unregister(a, dispose: false), Is.True);
            Assert.That(_registry.Unregister(a), Is.False);
            Assert.That(_log, Is.Empty);
        }

        [Test]
        public void Dispose_RunsInReverseOrder_AndIsIdempotent()
        {
            Add("low", 0);
            Add("high", 100);
            _registry.Initialize();
            _log.Clear();

            _registry.Dispose();
            _registry.Dispose();

            CollectionAssert.AreEqual(new[] { "low.dispose", "high.dispose" }, _log);
            Assert.That(_registry.Systems, Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => _registry.Register(new InitOnlySystem()));
        }

        [Test]
        public void NestedGroup_RunsChildrenInItsSlot_AndGetFindsNestedSystems()
        {
            var group = new SystemGroup("Combat");
            var inner = new RecordingSystem("inner", _log);
            group.Register(inner);
            Add("before", 100);
            _registry.Register(group, 50);
            Add("after", 0);
            _registry.Initialize();
            _log.Clear();

            _registry.Update(1f);

            CollectionAssert.AreEqual(new[] { "before.update", "inner.update", "after.update" }, _log);
            Assert.That(_registry.Get<SystemGroup>(), Is.SameAs(group));
            Assert.That(_registry.TryGet<InitOnlySystem>(out _), Is.False);
        }

        [Test]
        public void Get_FindsSystemInsideNestedGroup()
        {
            var group = new SystemGroup("G");
            var system = new InitOnlySystem();
            group.Register(system);
            _registry.Register(group);

            Assert.That(_registry.Get<InitOnlySystem>(), Is.SameAs(system));
        }

        [Test]
        public void PhaseFilteredSystem_RunsOnlyInItsPhase()
        {
            var phases = new PhaseTracker(initialPhaseId: "Exploration");
            _registry.PhaseProvider = phases;
            var combatOnly = new PhaseOnlySystem("Combat");
            _registry.Register(combatOnly);
            _registry.Initialize();

            _registry.Update(1f);
            phases.SetPhase("Combat");
            _registry.Update(1f);

            Assert.That(combatOnly.Updates, Is.EqualTo(1));
        }

        [Test]
        public void PhaseSystemGroup_FiltersWholeGroup_AndChildGroupInheritsProvider()
        {
            var phases = new PhaseTracker(initialPhaseId: "Menu");
            var combat = new PhaseSystemGroup("Combat", null, "Combat", "Boss");
            var nested = new PhaseOnlySystem("Boss");
            combat.Register(nested);
            _registry.PhaseProvider = phases;
            _registry.Register(combat);
            _registry.Initialize();

            _registry.Update(1f);
            phases.SetPhase("Combat");
            _registry.Update(1f);
            phases.SetPhase("Boss");
            _registry.Update(1f);

            Assert.That(nested.Updates, Is.EqualTo(1));
            Assert.That(combat.PhaseProvider, Is.SameAs(phases));
        }

        [Test]
        public void PhaseSystemGroup_WithoutPhases_IsAlwaysActive()
        {
            var group = new PhaseSystemGroup("Any");
            Assert.That(group.IsActiveInPhase("whatever"), Is.True);
            Assert.That(group.IsActiveInPhase(null), Is.True);
        }

        [Test]
        public void GroupCannotContainItself()
        {
            var group = new SystemGroup("G");
            Assert.Throws<ArgumentException>(() => group.Register(group));
        }
    }

    public sealed class PhaseTrackerTests
    {
        [Test]
        public void SetPhase_RaisesEventAndPublishesSticky_OnlyOnChange()
        {
            var bus = new EventBus();
            var tracker = new PhaseTracker(bus, "A");
            var changes = new List<string>();
            tracker.PhaseChanged += (from, to) => changes.Add($"{from}->{to}");

            Assert.That(tracker.SetPhase("B"), Is.True);
            Assert.That(tracker.SetPhase("B"), Is.False);

            CollectionAssert.AreEqual(new[] { "A->B" }, changes);
            Assert.That(bus.TryGetSticky<PhaseChangedEvent>(out var sticky), Is.True);
            Assert.That(sticky.PreviousPhaseId, Is.EqualTo("A"));
            Assert.That(sticky.CurrentPhaseId, Is.EqualTo("B"));
        }
    }

    internal readonly struct PingEvent : IEvent
    {
    }

    internal sealed class PingCounterSystem : SystemBase, IUpdateSystem
    {
        public int Pings;
        public bool FailInit;
        public int DisposeCalls;

        public PingCounterSystem(IEventBus bus) : base(null, bus)
        {
        }

        public void Update(float deltaTime) => Publish(new PingEvent());

        protected override void OnInitialize()
        {
            Subscribe<PingEvent>(_ => Pings++);
            if (FailInit)
                throw new InvalidOperationException("init failed");
        }

        protected override void OnDispose() => DisposeCalls++;
    }

    public sealed class SystemBaseTests
    {
        [Test]
        public void Subscriptions_AreReleasedOnDispose()
        {
            var bus = new EventBus();
            var system = new PingCounterSystem(bus);
            system.Initialize();

            system.Update(0f);
            system.Dispose();
            bus.Publish(new PingEvent());

            Assert.That(system.Pings, Is.EqualTo(1));
            Assert.That(bus.GetSubscriberCount<PingEvent>(), Is.Zero);
            Assert.That(system.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void FailedInitialize_ReleasesSubscriptions_AndSkipsOnDispose()
        {
            var bus = new EventBus();
            var system = new PingCounterSystem(bus) { FailInit = true };

            Assert.Throws<InvalidOperationException>(() => system.Initialize());
            system.Dispose();

            Assert.That(system.IsInitialized, Is.False);
            Assert.That(bus.GetSubscriberCount<PingEvent>(), Is.Zero);
            Assert.That(system.DisposeCalls, Is.Zero);
        }

        [Test]
        public void InitializeAfterDispose_Throws()
        {
            var system = new PingCounterSystem(new EventBus());
            system.Dispose();

            Assert.Throws<ObjectDisposedException>(() => system.Initialize());
        }

        [Test]
        public void EventHelpers_WithoutBus_ThrowClearError()
        {
            var system = new PingCounterSystem(null);
            var ex = Assert.Throws<InvalidOperationException>(() => system.Initialize());
            StringAssert.Contains("IEventBus", ex.Message);
        }
    }

    public sealed class EventPumpSystemTests
    {
        [Test]
        public void Update_FlushesQueue_AdvancesThrottlerAndBatcher()
        {
            var bus = new EventBus();
            var received = new List<int>();
            bus.Subscribe<CountEvent>(e => received.Add(e.Value));
            var throttler = new EventThrottler(bus);
            throttler.Configure<CountEvent>(1f, ThrottleMode.Trailing);
            var pump = new EventPumpSystem(bus, throttler) { MaxEventsPerFrame = 1 };

            bus.Enqueue(new CountEvent(1));
            bus.Enqueue(new CountEvent(2));
            throttler.Publish(new CountEvent(99));

            pump.Update(1f);
            CollectionAssert.AreEqual(new[] { 99, 1 }, received);

            pump.Update(0f);
            CollectionAssert.AreEqual(new[] { 99, 1, 2 }, received);
        }

        private readonly struct CountEvent : IEvent
        {
            public CountEvent(int value) => Value = value;
            public int Value { get; }
        }
    }

    public sealed class WorldTests
    {
        [Test]
        public void Lifecycle_DrivesSystems_AndTracksTime()
        {
            var log = new List<string>();
            var registry = new SystemRegistry();
            registry.Register(new RecordingSystem("s", log));
            var world = new World("Main", new EntityManager(), registry);

            world.Initialize();
            world.Update(0.5f);
            world.FixedUpdate(0.02f);
            world.LateUpdate(0.5f);
            world.Update(0.25f);
            world.Dispose();

            CollectionAssert.AreEqual(new[] { "s.init", "s.update", "s.fixed", "s.late", "s.update", "s.dispose" }, log);
            Assert.That(world.Time, Is.EqualTo(0.75).Within(1e-9));
            Assert.That(world.FrameCount, Is.EqualTo(2));
            Assert.That(world.State, Is.EqualTo(WorldState.Disposed));
        }

        [Test]
        public void UpdateBeforeInitialize_Throws_UpdateAfterDispose_IsIgnored()
        {
            var world = new World("W", new EntityManager(), new SystemRegistry());

            Assert.Throws<InvalidOperationException>(() => world.Update(1f));

            world.Initialize();
            world.Dispose();
            Assert.DoesNotThrow(() => world.Update(1f));
            Assert.Throws<ObjectDisposedException>(() => world.Initialize());
        }

        [Test]
        public void Paused_SystemsStillTick_WithNoTimePassing()
        {
            var ticks = new List<float>();
            var registry = new SystemRegistry();
            registry.Register(new DeltaSystem(ticks));
            var pause = new GamePause();
            var world = new World("W", new EntityManager(), registry, pause: pause);
            world.Initialize();

            world.Update(0.5f);
            pause.Pause("vote");
            world.Update(0.5f);
            pause.Resume("vote");
            world.Update(0.25f);

            Assert.That(ticks, Is.EqualTo(new[] { 0.5f, 0f, 0.25f }));
            Assert.That(world.Time, Is.EqualTo(0.75).Within(1e-9));
            Assert.That(world.FrameCount, Is.EqualTo(3));
        }

        [Test]
        public void GamePause_CountsReasons_AndAnnouncesOnlyTheEdges()
        {
            var log = new List<string>();
            var bus = new EventBus();
            bus.Subscribe<GamePausedEvent>(e => log.Add("paused:" + e.Reason));
            bus.Subscribe<GameResumedEvent>(e => log.Add("resumed:" + e.Reason));
            var pause = new GamePause(bus);

            Assert.That(pause.Pause("vote"), Is.True);
            Assert.That(pause.Pause("vote"), Is.False);
            Assert.That(pause.Pause("menu"), Is.True);
            Assert.That(pause.Resume("vote"), Is.True);
            Assert.That(pause.Resume("vote"), Is.False);
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(pause.Resume("menu"), Is.True);
            pause.Pause("cutscene");
            pause.Pause("menu");
            pause.ResumeAll();

            Assert.That(pause.IsPaused, Is.False);
            Assert.That(pause.Reasons, Is.Empty);
            Assert.That(log, Is.EqualTo(new[] { "paused:vote", "resumed:menu", "paused:cutscene", "resumed:" }),
                "ResumeAll lifts no single reason");
            Assert.Throws<ArgumentException>(() => pause.Pause(" "));
            bus.Dispose();
        }

        private sealed class DeltaSystem : IUpdateSystem
        {
            private readonly List<float> _ticks;

            public DeltaSystem(List<float> ticks) => _ticks = ticks;

            public void Initialize() { }
            public void Update(float deltaTime) => _ticks.Add(deltaTime);
            public void Dispose() { }
        }

        [Test]
        public void SeveralWorlds_AreIndependent()
        {
            var log = new List<string>();
            var a = new SystemRegistry();
            a.Register(new RecordingSystem("a", log));
            var b = new SystemRegistry();
            b.Register(new RecordingSystem("b", log));
            var worldA = new World("A", new EntityManager(), a);
            var worldB = new World("B", new EntityManager(), b);
            worldA.Initialize();
            worldB.Initialize();
            log.Clear();

            worldA.Update(1f);

            CollectionAssert.AreEqual(new[] { "a.update" }, log);
            Assert.That(worldA.Entities, Is.Not.SameAs(worldB.Entities));
        }
    }
}
