using System;
using System.Text;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Diagnostics.Unity;
using RPGFramework.Events;
using RPGFramework.Pooling;
using RPGFramework.Systems;
using RPGFramework.Timers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.Diagnostics.Tests
{
    internal readonly struct PingEvent : IEvent
    {
    }

    public sealed class DiagnosticsTests
    {
        private sealed class FailingSection : IDiagnosticsSection
        {
            public string Title => "Broken";

            public void Write(StringBuilder output)
            {
                output.Append("partial");
                throw new InvalidOperationException("no data");
            }
        }

        private sealed class FixedSection : IDiagnosticsSection
        {
            private readonly string _text;

            public FixedSection(string title, string text)
            {
                Title = title;
                _text = text;
            }

            public string Title { get; }

            public void Write(StringBuilder output) => output.AppendLine(_text);
        }

        [Test]
        public void Hub_RendersSections_AndIsolatesFailures()
        {
            var hub = new DiagnosticsHub()
                .Add(new FixedSection("A", "alpha"))
                .Add(new FailingSection())
                .Add(new FixedSection("C", "gamma"));

            string report = hub.Render();

            StringAssert.Contains("== A ==", report);
            StringAssert.Contains("alpha", report);
            StringAssert.Contains("<section failed: InvalidOperationException: no data>", report);
            StringAssert.DoesNotContain("partial", report);
            StringAssert.Contains("gamma", report);
            Assert.That(hub.Render("C"), Does.Contain("gamma").And.Not.Contain("alpha"));
            Assert.That(hub.Remove("Broken"), Is.True);
            Assert.That(hub.Sections.Count, Is.EqualTo(2));
        }

        [Test]
        public void PerformanceMonitor_ComputesRollingFrameStats()
        {
            var monitor = new PerformanceMonitor(windowFrames: 2);
            monitor.Initialize();

            monitor.Update(0.1f);
            monitor.Update(0.01f);
            monitor.Update(0.03f); // 0.1 leaves the window

            Assert.That(monitor.SampleCount, Is.EqualTo(2));
            Assert.That(monitor.AverageFrameMs, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(monitor.MaxFrameMs, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(monitor.AverageFps, Is.EqualTo(50f).Within(1e-2f));
            Assert.That(monitor.ManagedMemoryBytes, Is.GreaterThan(0));
        }

        [Test]
        public void Sections_DescribeTheirServices()
        {
            var bus = new EventBus();
            var recorder = new EventRecorder(bus);
            bus.Publish(new PingEvent());
            bus.Publish(new PingEvent());

            var timers = new TimerService();
            timers.Repeat(2f, () => { }, 3, "Regen");

            var pools = new PoolManager();
            var pool = new ObjectPool<StringBuilder>(() => new StringBuilder(), initialSize: 1, poolName: "Builders");
            pools.Register("Builders", pool);
            pool.Get();

            var registry = new SystemRegistry();
            var profiler = new SystemProfiler();
            registry.Profiler = profiler;
            registry.Register(new ActionSystem("Ai", _ => { }));
            registry.Initialize();
            registry.Update(0f);

            var hub = new DiagnosticsHub()
                .Add(new EventsSection(recorder, bus))
                .Add(new TimersSection(timers))
                .Add(new PoolsSection(pools) { LeakThresholdSeconds = 0 })
                .Add(new SystemsSection(registry, profiler));
            string report = hub.Render();

            StringAssert.Contains("PingEvent x2", report);
            StringAssert.Contains("Regen", report);
            StringAssert.Contains("Builders: 1 active", report);
            StringAssert.Contains("suspected leaks", report);
            StringAssert.Contains("ActionSystem:Ai.Update", report);
        }

        [Test]
        public void Module_WiresProfilingRecordingAndSectionsIntoTheHost()
        {
            var host = new GameHost();
            host.Start(b => b.UseModule(new DiagnosticsModule()));
            try
            {
                var container = host.Container;
                var bus = container.Resolve<IEventBus>();
                bus.Publish(new PingEvent());
                host.Update(0.02f);

                var group = (SystemGroup)container.Resolve<ISystemRegistry>();
                Assert.That(group.Profiler, Is.SameAs(container.Resolve<SystemProfiler>()));
                Assert.That(container.Resolve<PerformanceMonitor>().SampleCount, Is.EqualTo(1));

                string report = container.Resolve<DiagnosticsHub>().Render();
                StringAssert.Contains("== Performance ==", report);
                StringAssert.Contains("== Timers ==", report);
                StringAssert.Contains("PingEvent x1", report);
                StringAssert.Contains("EventPumpSystem.Update", report);
            }
            finally
            {
                host.Dispose();
            }
        }

        [Test]
        public void Overlay_RendersAssignedHub()
        {
            var go = new GameObject("Diagnostics");
            try
            {
                var overlay = go.AddComponent<DiagnosticsOverlay>();
                overlay.Refresh();
                StringAssert.Contains("No DiagnosticsHub", overlay.Text);

                overlay.Hub = new DiagnosticsHub().Add(new FixedSection("A", "alpha"));
                overlay.Refresh();
                StringAssert.Contains("alpha", overlay.Text);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Overlay_ShowsOnlyTheChosenSections_WithOrWithoutHeaders()
        {
            var go = new GameObject("Diagnostics");
            try
            {
                var overlay = go.AddComponent<DiagnosticsOverlay>();
                overlay.Hub = new DiagnosticsHub()
                    .Add(new FixedSection("A", "alpha"))
                    .Add(new FixedSection("B", "beta"))
                    .Add(new FixedSection("C", "gamma"));

                overlay.Sections = new[] { "C", "A" };
                overlay.Refresh();
                StringAssert.Contains("== A ==", overlay.Text);
                StringAssert.Contains("gamma", overlay.Text);
                StringAssert.DoesNotContain("beta", overlay.Text);
                Assert.That(overlay.Text.IndexOf("alpha", StringComparison.Ordinal),
                    Is.LessThan(overlay.Text.IndexOf("gamma", StringComparison.Ordinal)), "in the hub's order");

                overlay.Headers = false;
                overlay.Refresh();
                Assert.That(overlay.Text, Is.EqualTo("alpha" + Environment.NewLine + "gamma"));

                overlay.Sections = null;
                overlay.Refresh();
                StringAssert.Contains("beta", overlay.Text);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheCompactLayout_IsASmallSeeThroughPanel_OfShortLines_RefreshedEveryFrame()
        {
            var go = new GameObject("Diagnostics");
            var camera = new GameObject("DiagnosticsCamera").AddComponent<Camera>();
            camera.aspect = 1f;
            var render = new RenderSection(() => camera, RenderBudget.For(RenderPlatform.Mobile));
            try
            {
                var overlay = go.AddComponent<DiagnosticsOverlay>();
                var performance = new PerformanceSection(new PerformanceMonitor(windowFrames: 2));
                overlay.Hub = new DiagnosticsHub().Add(performance).Add(new FixedSection("Systems", "many lines")).Add(render);

                DiagnosticsPanelLayout.For(DiagnosticsPanelStyle.Compact).ApplyTo(overlay);

                Assert.That(overlay.Corner, Is.EqualTo(DiagnosticsCorner.TopRight));
                Assert.That(overlay.RefreshInterval, Is.EqualTo(0f), "every frame");
                Assert.That(overlay.Size, Is.EqualTo(Vector2.zero), "fitted to the text");
                Assert.That(overlay.Opacity, Is.LessThan(1f));
                Assert.That(performance.Compact && render.Compact, "short lines");
                Assert.That(render.TopModels, Is.EqualTo(3));
                overlay.Refresh();
                StringAssert.StartsWith("FPS ", overlay.Text);
                StringAssert.Contains("Mobile · tris ", overlay.Text);
                StringAssert.DoesNotContain("many lines", overlay.Text);
                StringAssert.DoesNotContain("==", overlay.Text);

                new DiagnosticsPanelLayout().ApplyTo(overlay);
                Assert.That(overlay.Corner, Is.EqualTo(DiagnosticsCorner.TopLeft), "Full is the old panel");
                Assert.That(overlay.Sections, Is.Empty);
                Assert.That(performance.Compact, Is.False);
                var custom = new DiagnosticsPanelLayout { Style = DiagnosticsPanelStyle.Custom, Opacity = 0f };
                Assert.That(custom.Resolved(), Is.SameAs(custom));
            }
            finally
            {
                render.Dispose();
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(go);
            }
        }
    }
}
