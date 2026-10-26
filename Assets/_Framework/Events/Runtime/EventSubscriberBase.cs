using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.Events
{
    /// <summary>
    /// Collects subscription tokens and disposes them together (composition-friendly alternative to
    /// <see cref="EventSubscriberBase"/>).
    /// </summary>
    public sealed class SubscriptionBag : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>();

        public int Count => _items.Count;

        public T Add<T>(T subscription) where T : IDisposable
        {
            if (subscription != null)
                _items.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Disposes every collected subscription (in reverse order) and empties the bag.
        /// </summary>
        public void Dispose()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                _items[i].Dispose();
            _items.Clear();
        }
    }

    public static class SubscriptionBagExtensions
    {
        public static IDisposable AddTo(this IDisposable subscription, SubscriptionBag bag) => bag.Add(subscription);
    }

    /// <summary>
    /// Base for services that listen to events: subscriptions made through the protected helpers are
    /// released by <see cref="UnsubscribeAll"/>/<see cref="Dispose"/>, so handlers cannot leak.
    /// </summary>
    public abstract class EventSubscriberBase : IDisposable
    {
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        protected EventSubscriberBase(IEventBus eventBus)
        {
            EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        protected IEventBus EventBus { get; }

        public bool IsDisposed { get; private set; }

        protected void Subscribe<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.Normal) where TEvent : IEvent =>
            _subscriptions.Add(EventBus.Subscribe(handler, priority));

        protected void SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, UniTask> handler, EventPriority priority = EventPriority.Normal)
            where TEvent : IEvent =>
            _subscriptions.Add(EventBus.SubscribeAsync(handler, priority));

        public void UnsubscribeAll() => _subscriptions.Dispose();

        public virtual void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;
            UnsubscribeAll();
        }
    }
}
