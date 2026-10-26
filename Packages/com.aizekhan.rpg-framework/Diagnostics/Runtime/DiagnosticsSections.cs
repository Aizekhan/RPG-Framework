using System;
using System.Text;
using RPGFramework.Events;
using RPGFramework.Pooling;
using RPGFramework.Systems;
using RPGFramework.Timers;

namespace RPGFramework.Diagnostics
{
    public sealed class PerformanceSection : IDiagnosticsSection
    {
        private readonly PerformanceMonitor _monitor;

        public PerformanceSection(PerformanceMonitor monitor)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        }

        public string Title => "Performance";

        public void Write(StringBuilder output)
        {
            output.AppendLine($"FPS {_monitor.AverageFps:F0} avg / {_monitor.WorstFps:F0} worst " +
                              $"(frame {_monitor.AverageFrameMs:F1} ms avg, {_monitor.MaxFrameMs:F1} ms max, {_monitor.SampleCount} frames)");
            output.AppendLine($"Managed memory {_monitor.ManagedMemoryBytes / (1024f * 1024f):F1} MB, GC gen0 {_monitor.GcCollections}");
        }
    }

    /// <summary>
    /// Registered systems (state and error counts) and, with a profiler, the slowest ones.
    /// </summary>
    public sealed class SystemsSection : IDiagnosticsSection
    {
        private readonly ISystemRegistry _systems;
        private readonly SystemProfiler _profiler;

        public SystemsSection(ISystemRegistry systems, SystemProfiler profiler = null)
        {
            _systems = systems ?? throw new ArgumentNullException(nameof(systems));
            _profiler = profiler;
        }

        public string Title => "Systems";

        /// <summary>
        /// Timings listed (slowest average first).
        /// </summary>
        public int TopTimings { get; set; } = 8;

        public void Write(StringBuilder output)
        {
            int faulted = 0;
            int inactive = 0;
            var infos = _systems.GetSystemInfos();
            foreach (var info in infos)
            {
                if (info.IsFaulted)
                    faulted++;
                else if (!info.IsActive)
                    inactive++;
            }
            output.AppendLine($"{infos.Count} systems in '{_systems.Name}', {inactive} inactive, {faulted} faulted");
            foreach (var info in infos)
            {
                if (info.IsFaulted || info.ErrorCount > 0)
                    output.AppendLine($"  ! {info}");
            }

            if (_profiler == null)
                return;
            var timings = _profiler.GetTimings();
            for (int i = 0; i < timings.Count && i < TopTimings; i++)
            {
                var t = timings[i];
                string slow = t.LastMs > _profiler.SlowThresholdMs ? " SLOW" : "";
                output.AppendLine($"  {t.SystemName}.{t.Stage}: {t.AverageMs:F2} ms avg, {t.MaxMs:F2} ms max{slow}");
            }
        }
    }

    /// <summary>
    /// Most frequent and most recent events from an <see cref="EventRecorder"/>, plus bus health when available.
    /// </summary>
    public sealed class EventsSection : IDiagnosticsSection
    {
        private readonly EventRecorder _recorder;
        private readonly EventBus _bus;

        public EventsSection(EventRecorder recorder, EventBus bus = null)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _bus = bus;
        }

        public string Title => "Events";

        public int TopTypes { get; set; } = 5;
        public int RecentEvents { get; set; } = 5;

        public void Write(StringBuilder output)
        {
            output.Append($"{_recorder.TotalRecorded} recorded");
            if (_bus != null)
                output.Append($", {_bus.PendingCount} queued, {_bus.HandlerErrorCount} handler errors");
            output.AppendLine();

            var counts = _recorder.GetTypeCounts();
            for (int i = 0; i < counts.Count && i < TopTypes; i++)
                output.AppendLine($"  {counts[i].Key.Name} x{counts[i].Value}");

            var recent = _recorder.Snapshot();
            for (int i = Math.Max(0, recent.Count - RecentEvents); i < recent.Count; i++)
                output.AppendLine($"  {recent[i]}");
        }
    }

    public sealed class PoolsSection : IDiagnosticsSection
    {
        private readonly IPoolManager _pools;

        public PoolsSection(IPoolManager pools)
        {
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
        }

        public string Title => "Pools";

        /// <summary>
        /// Objects active longer than this are listed as suspected leaks.
        /// </summary>
        public double LeakThresholdSeconds { get; set; } = 60;

        public void Write(StringBuilder output)
        {
            var statistics = _pools.GetAllStatistics();
            output.AppendLine($"{statistics.Count} pools, {_pools.TotalActive} objects in use");
            foreach (var pool in statistics)
                output.AppendLine($"  {pool.PoolKey}: {pool.ActiveCount} active / {pool.InactiveCount} idle, peak {pool.PeakActiveCount}");

            var leaks = _pools.FindLeaks(LeakThresholdSeconds);
            if (leaks.Count > 0)
                output.AppendLine($"  {leaks.Count} suspected leaks (active > {LeakThresholdSeconds:F0}s), oldest: {leaks[0]}");
        }
    }

    public sealed class TimersSection : IDiagnosticsSection
    {
        private readonly ITimerService _timers;

        public TimersSection(ITimerService timers)
        {
            _timers = timers ?? throw new ArgumentNullException(nameof(timers));
        }

        public string Title => "Timers";

        public int MaxListed { get; set; } = 8;

        public void Write(StringBuilder output)
        {
            var timers = _timers.GetTimers();
            output.AppendLine($"{timers.Count} active, time scale {_timers.TimeScale:F2}");
            for (int i = 0; i < timers.Count && i < MaxListed; i++)
            {
                var t = timers[i];
                string repeats = t.RemainingRepeats < 0 ? "repeating" : $"{t.RemainingRepeats} left";
                output.AppendLine($"  {t.Handle} {t.Category ?? "-"}: {t.Remaining:F2}s, {repeats}{(t.IsPaused ? ", paused" : "")}");
            }
        }
    }
}
