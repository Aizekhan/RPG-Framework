using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.Events
{
    /// <summary>
    /// Marker for event payloads. Prefer small immutable structs; they are dispatched without boxing.
    /// </summary>
    public interface IEvent
    {
    }

    /// <summary>
    /// Handler order within one event type: higher priority runs first; equal priority keeps subscription order.
    /// </summary>
    public enum EventPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Publishing side of the bus. Depend on this when a class only raises events.
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Delivers the event to synchronous handlers immediately, on the calling thread.
        /// A throwing handler is logged and does not stop the others.
        /// </summary>
        void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;

        /// <summary>
        /// Like <see cref="Publish{TEvent}"/>, and the event is kept as the type's "sticky" value:
        /// later subscribers receive it immediately on subscription (e.g. GameReady, current scene).
        /// </summary>
        void PublishSticky<TEvent>(TEvent eventData) where TEvent : IEvent;

        /// <summary>
        /// Delivers to synchronous handlers, then awaits asynchronous handlers priority group by priority group
        /// (handlers of the same priority run concurrently). Handler exceptions are logged, not rethrown.
        /// </summary>
        UniTask PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : IEvent;

        /// <summary>
        /// Queues the event for the next <see cref="IEventBus.Flush"/>. Safe to call from any thread.
        /// </summary>
        void Enqueue<TEvent>(TEvent eventData) where TEvent : IEvent;
    }

    /// <summary>
    /// Type-safe publish/subscribe hub that decouples framework modules and game code.
    /// </summary>
    public interface IEventBus : IEventPublisher
    {
        /// <summary>
        /// Subscribes a synchronous handler. Dispose the returned token to unsubscribe.
        /// Subscribing the same delegate twice is ignored (logged as a warning).
        /// </summary>
        /// <param name="receiveSticky">Immediately receive the current sticky event of this type, if any.</param>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.Normal, bool receiveSticky = true)
            where TEvent : IEvent;

        IDisposable SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, UniTask> handler, EventPriority priority = EventPriority.Normal)
            where TEvent : IEvent;

        bool Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

        bool UnsubscribeAsync<TEvent>(Func<TEvent, CancellationToken, UniTask> handler) where TEvent : IEvent;

        /// <summary>
        /// Publishes events queued with <see cref="IEventPublisher.Enqueue{TEvent}"/>.
        /// Only events queued before the call are processed; events enqueued by handlers wait for the next flush.
        /// </summary>
        /// <returns>Number of events published.</returns>
        int Flush(int maxEvents = int.MaxValue);

        int PendingCount { get; }

        bool TryGetSticky<TEvent>(out TEvent eventData) where TEvent : IEvent;

        bool ClearSticky<TEvent>() where TEvent : IEvent;

        int GetSubscriberCount<TEvent>() where TEvent : IEvent;

        /// <summary>
        /// Receives every published event (boxed) after handlers ran. For diagnostics only.
        /// </summary>
        IDisposable AddObserver(Action<Type, object> observer);

        /// <summary>
        /// Removes all subscriptions, sticky events, observers and queued events.
        /// </summary>
        void Clear();
    }
}
