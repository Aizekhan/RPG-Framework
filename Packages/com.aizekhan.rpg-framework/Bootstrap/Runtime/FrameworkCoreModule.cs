using RPGFramework.Core.Composition;
using RPGFramework.DI;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Systems;
using RPGFramework.Timers;

namespace RPGFramework.Bootstrap
{
    public sealed class FrameworkCoreOptions
    {
        public string WorldName { get; set; } = "Main";

        /// <summary>
        /// Phase reported by <see cref="IPhaseProvider"/> before the game sets one.
        /// </summary>
        public string InitialPhaseId { get; set; }

        /// <summary>
        /// Queued events delivered per frame by <see cref="EventPumpSystem"/>.
        /// </summary>
        public int MaxEventsPerFrame { get; set; } = int.MaxValue;

        /// <summary>
        /// See <see cref="SystemGroup.MaxConsecutiveErrors"/>; 0 never disables a failing system.
        /// </summary>
        public int MaxConsecutiveSystemErrors { get; set; }
    }

    /// <summary>
    /// Registers the game-agnostic runtime every game needs:
    /// <see cref="ILogger"/> (null logger unless the host registered one), <see cref="IEventBus"/>/<see cref="IEventPublisher"/>,
    /// <see cref="EventThrottler"/>, <see cref="EventBatcher"/>, <see cref="IEntityManager"/>,
    /// <see cref="PhaseTracker"/>/<see cref="IPhaseProvider"/>, <see cref="ITimerService"/>, <see cref="ISystemRegistry"/>
    /// and <see cref="IWorld"/>. On initialize it adds the <see cref="EventPumpSystem"/> ahead of all other systems,
    /// followed by the timer service.
    /// </summary>
    public sealed class FrameworkCoreModule : BaseFeatureModule
    {
        public const string ModuleName = "Framework.Core";

        /// <summary>
        /// Runs before every other system so queued events are delivered at the start of the frame.
        /// </summary>
        public const int EventPumpPriority = SystemPriorities.Core + 100;

        /// <summary>
        /// Timers fire right after queued events are delivered, before gameplay systems run.
        /// </summary>
        public const int TimerPriority = SystemPriorities.Core + 50;

        private readonly FrameworkCoreOptions _options;

        public FrameworkCoreModule() : this(null)
        {
        }

        public FrameworkCoreModule(FrameworkCoreOptions options)
        {
            _options = options ?? new FrameworkCoreOptions();
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<ILogger>())
                Container.RegisterInstance<ILogger>(NullLogger.Instance);

            Container.RegisterFactory<IEventBus>(c => new EventBus(c.Resolve<ILogger>()));
            Container.RegisterFactory<IEventPublisher>(c => c.Resolve<IEventBus>());
            Container.RegisterFactory(c => new EventThrottler(c.Resolve<IEventBus>()));
            Container.RegisterFactory(c => new EventBatcher(c.Resolve<IEventBus>()));

            Container.RegisterFactory<IEntityManager>(_ => new EntityManager());

            Container.RegisterFactory(c => new PhaseTracker(c.Resolve<IEventBus>(), _options.InitialPhaseId));
            Container.RegisterFactory<IPhaseProvider>(c => c.Resolve<PhaseTracker>());

            Container.RegisterFactory<ITimerService>(c => new TimerService(c.Resolve<ILogger>()));

            Container.RegisterFactory<ISystemRegistry>(c => new SystemRegistry(c.Resolve<ILogger>(), c.Resolve<IPhaseProvider>())
            {
                MaxConsecutiveErrors = _options.MaxConsecutiveSystemErrors
            });

            Container.RegisterFactory<IWorld>(c => new World(
                _options.WorldName,
                c.Resolve<IEntityManager>(),
                c.Resolve<ISystemRegistry>(),
                c.Resolve<ILogger>()));
        }

        public override void Initialize()
        {
            var pump = new EventPumpSystem(
                Container.Resolve<IEventBus>(),
                Container.Resolve<EventThrottler>(),
                Container.Resolve<EventBatcher>())
            {
                MaxEventsPerFrame = _options.MaxEventsPerFrame
            };
            var systems = Container.Resolve<ISystemRegistry>();
            systems.Register(pump, EventPumpPriority);
            // A game may replace ITimerService; tick it only if the implementation is itself a system
            if (Container.Resolve<ITimerService>() is IUpdateSystem timerSystem)
                systems.Register(timerSystem, TimerPriority);
        }

        /// <summary>
        /// Last module to shut down: disposes the world (idempotent if the host already did) and the bus.
        /// </summary>
        public override void Shutdown()
        {
            Container.Resolve<IWorld>().Dispose();
            if (Container.Resolve<IEventBus>() is EventBus bus)
                bus.Dispose();
        }
    }
}
