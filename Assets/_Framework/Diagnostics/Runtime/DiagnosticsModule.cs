using RPGFramework.Bootstrap;
using RPGFramework.DI;
using RPGFramework.Events;
using RPGFramework.Pooling;
using RPGFramework.Systems;
using RPGFramework.Timers;

namespace RPGFramework.Diagnostics
{
    public sealed class DiagnosticsOptions
    {
        public int EventHistory { get; set; } = 256;
        public int ProfilerWindow { get; set; } = 120;
        public int FrameWindow { get; set; } = 120;
        public double PoolLeakThresholdSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Opt-in diagnostics in one registration: profiles every system, records events, measures frame times and
    /// exposes a <see cref="DiagnosticsHub"/> with sections for whatever services the game registered (timers, pools).
    /// Add it only to development builds: recording boxes struct events and profiling adds a timestamp per system call.
    /// </summary>
    public sealed class DiagnosticsModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.Diagnostics";

        private readonly DiagnosticsOptions _options;
        private SystemGroup _profiledGroup;

        public DiagnosticsModule(DiagnosticsOptions options = null)
        {
            _options = options ?? new DiagnosticsOptions();
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory(_ => new SystemProfiler(_options.ProfilerWindow));
            Container.RegisterFactory(_ => new PerformanceMonitor(_options.FrameWindow));
            Container.RegisterFactory(c => new EventRecorder(c.Resolve<IEventBus>(), _options.EventHistory));
            Container.RegisterFactory(CreateHub);
            AddSystem<PerformanceMonitor>(SystemPriorities.Diagnostics);
        }

        protected override void OnInitialize()
        {
            if (Systems is SystemGroup group && group.Profiler == null)
            {
                group.Profiler = Container.Resolve<SystemProfiler>();
                _profiledGroup = group;
            }
            // Start recording from initialization on, not from the first time someone opens the report
            Container.Resolve<DiagnosticsHub>();
        }

        protected override void OnShutdown()
        {
            if (_profiledGroup != null)
            {
                _profiledGroup.Profiler = null;
                _profiledGroup = null;
            }
            Container.Resolve<EventRecorder>().Dispose();
        }

        private DiagnosticsHub CreateHub(IDIContainer c)
        {
            var hub = new DiagnosticsHub()
                .Add(new PerformanceSection(c.Resolve<PerformanceMonitor>()))
                .Add(new SystemsSection(c.Resolve<ISystemRegistry>(), c.Resolve<SystemProfiler>()))
                .Add(new EventsSection(c.Resolve<EventRecorder>(), c.Resolve<IEventBus>() as EventBus));
            if (c.IsRegistered<ITimerService>())
                hub.Add(new TimersSection(c.Resolve<ITimerService>()));
            if (c.IsRegistered<IPoolManager>())
                hub.Add(new PoolsSection(c.Resolve<IPoolManager>()) { LeakThresholdSeconds = _options.PoolLeakThresholdSeconds });
            return hub;
        }
    }
}
