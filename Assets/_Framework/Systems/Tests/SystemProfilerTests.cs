using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace RPGFramework.Systems.Tests
{
    public sealed class SystemProfilerTests
    {
        private sealed class RecordingProfiler : ISystemProfiler
        {
            public readonly List<(string Name, SystemStage Stage)> Calls = new List<(string, SystemStage)>();
            public bool Throw;

            public void Record(string systemName, SystemStage stage, double milliseconds)
            {
                Calls.Add((systemName, stage));
                if (Throw)
                    throw new InvalidOperationException("profiler broke");
            }
        }

        [Test]
        public void Group_ReportsEveryStageCall_IncludingNestedAndFailingSystems()
        {
            var log = new List<string>();
            var registry = new SystemRegistry();
            var nested = new SystemGroup("Combat");
            var failing = new RecordingSystem("bad", log) { OnUpdate = () => throw new InvalidOperationException() };
            registry.Register(new RecordingSystem("a", log), 10);
            registry.Register(nested);
            nested.Register(failing);
            var profiler = new RecordingProfiler();
            registry.Profiler = profiler;
            registry.Initialize();

            registry.Update(0.016f);
            registry.FixedUpdate(0.02f);

            Assert.That(nested.Profiler, Is.SameAs(profiler), "nested groups inherit the profiler");
            CollectionAssert.AreEqual(new[]
            {
                ("RecordingSystem", SystemStage.Update),
                ("RecordingSystem", SystemStage.Update),
                ("SystemGroup:Combat", SystemStage.Update),
                ("RecordingSystem", SystemStage.FixedUpdate),
                ("RecordingSystem", SystemStage.FixedUpdate),
                ("SystemGroup:Combat", SystemStage.FixedUpdate)
            }, profiler.Calls);
        }

        [Test]
        public void BrokenProfiler_DoesNotAffectSystems()
        {
            var log = new List<string>();
            var registry = new SystemRegistry { Profiler = new RecordingProfiler { Throw = true } };
            registry.Register(new RecordingSystem("a", log));
            registry.Register(new RecordingSystem("b", log));
            registry.Initialize();
            log.Clear();

            Assert.DoesNotThrow(() => registry.Update(0f));
            CollectionAssert.AreEqual(new[] { "a.update", "b.update" }, log);
            Assert.That(registry.GetSystemInfos().All(i => i.ErrorCount == 0), Is.True);
        }

        [Test]
        public void Profiler_KeepsRollingWindow_AndSortsSlowestFirst()
        {
            var profiler = new SystemProfiler(windowSize: 3) { SlowThresholdMs = 10 };
            foreach (var ms in new[] { 100.0, 1, 2, 3 })
                profiler.Record("Ai", SystemStage.Update, ms);
            profiler.Record("Physics", SystemStage.FixedUpdate, 20);

            Assert.That(profiler.TryGetTiming("Ai", SystemStage.Update, out var ai), Is.True);
            Assert.That(ai.Samples, Is.EqualTo(3));
            Assert.That(ai.AverageMs, Is.EqualTo(2).Within(1e-9), "the 100 ms sample left the window");
            Assert.That(ai.MaxMs, Is.EqualTo(3));
            Assert.That(ai.LastMs, Is.EqualTo(3));

            Assert.That(profiler.GetTimings().Select(t => t.SystemName), Is.EqualTo(new[] { "Physics", "Ai" }));
            Assert.That(profiler.GetSlowSystems().Single().SystemName, Is.EqualTo("Physics"));

            profiler.IsEnabled = false;
            profiler.Record("Ui", SystemStage.LateUpdate, 1);
            Assert.That(profiler.TryGetTiming("Ui", SystemStage.LateUpdate, out _), Is.False);

            profiler.Reset();
            Assert.That(profiler.GetTimings(), Is.Empty);
        }

        [Test]
        public void MeasuredDuration_ReflectsWorkDone()
        {
            var profiler = new SystemProfiler();
            var registry = new SystemRegistry { Profiler = profiler };
            registry.Register(new ActionSystem("Sleepy", _ => Thread.Sleep(5)));
            registry.Initialize();

            registry.Update(0f);

            Assert.That(profiler.TryGetTiming("ActionSystem:Sleepy", SystemStage.Update, out var timing), Is.True);
            Assert.That(timing.LastMs, Is.GreaterThanOrEqualTo(4.0));
        }
    }
}
