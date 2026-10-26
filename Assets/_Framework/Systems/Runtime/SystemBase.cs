using System;
using RPGFramework.Events;
using RPGFramework.Logging;

namespace RPGFramework.Systems
{
    /// <summary>
    /// Convenience base for systems: idempotent Initialize/Dispose with template methods, a logger, and
    /// event helpers whose subscriptions are released automatically on Dispose (or when initialization fails).
    /// Opt into per-frame work by also implementing <see cref="IUpdateSystem"/> etc.
    /// </summary>
    public abstract class SystemBase : ISystem
    {
        private SubscriptionBag _subscriptions = new SubscriptionBag();

        protected SystemBase(ILogger logger = null, IEventBus eventBus = null)
        {
            Logger = logger;
            EventBus = eventBus;
        }

        public bool IsInitialized { get; private set; }

        public bool IsDisposed { get; private set; }

        protected ILogger Logger { get; }

        protected IEventBus EventBus { get; }

        protected string LogCategory => GetType().Name;

        public void Initialize()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            if (IsInitialized)
                return;

            try
            {
                OnInitialize();
            }
            catch
            {
                _subscriptions.Dispose();
                throw;
            }

            IsInitialized = true;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            _subscriptions.Dispose();
            if (IsInitialized)
            {
                IsInitialized = false;
                OnDispose();
            }
        }

        /// <summary>
        /// Subscribe to events and acquire resources here.
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// Release resources here; event subscriptions are already gone.
        /// </summary>
        protected virtual void OnDispose()
        {
        }

        protected void Subscribe<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.Normal) where TEvent : IEvent =>
            _subscriptions.Add(RequireEventBus().Subscribe(handler, priority));

        protected void Publish<TEvent>(TEvent eventData) where TEvent : IEvent => RequireEventBus().Publish(eventData);

        protected void Enqueue<TEvent>(TEvent eventData) where TEvent : IEvent => RequireEventBus().Enqueue(eventData);

        private IEventBus RequireEventBus() =>
            EventBus ?? throw new InvalidOperationException($"{GetType().Name} was constructed without an IEventBus");
    }

    /// <summary>
    /// Drives the time-based event helpers once per frame: advances the throttler and batcher and
    /// delivers events queued with <see cref="IEventPublisher.Enqueue{TEvent}"/>.
    /// Register it with a high priority so queued events are handled early in the frame.
    /// </summary>
    public sealed class EventPumpSystem : IUpdateSystem
    {
        private readonly IEventBus _bus;
        private readonly EventThrottler _throttler;
        private readonly EventBatcher _batcher;

        public EventPumpSystem(IEventBus bus, EventThrottler throttler = null, EventBatcher batcher = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _throttler = throttler;
            _batcher = batcher;
        }

        /// <summary>
        /// Upper bound of queued events delivered per frame (spreads bursts over frames).
        /// </summary>
        public int MaxEventsPerFrame { get; set; } = int.MaxValue;

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            _throttler?.Update(deltaTime);
            _batcher?.Update(deltaTime);
            _bus.Flush(MaxEventsPerFrame);
        }

        public void Dispose()
        {
            _throttler?.FlushPending();
            _batcher?.FlushAll();
        }
    }
}
