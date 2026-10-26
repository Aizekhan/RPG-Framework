using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Logging;

namespace RPGFramework.Events
{
    /// <summary>
    /// Default <see cref="IEventBus"/>.
    /// Dispatch is generic (no boxing for struct events unless observers are attached), handler lists are
    /// copy-on-write so handlers may subscribe/unsubscribe/publish while being dispatched, and subscription
    /// is thread-safe. Recursive publishing deeper than <see cref="MaxDispatchDepth"/> is refused to stop
    /// event ping-pong from overflowing the stack.
    /// </summary>
    public sealed class EventBus : IEventBus, IDisposable
    {
        private const string Category = "Events";

        [ThreadStatic]
        private static int _dispatchDepth;

        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private readonly Dictionary<Type, ChannelBase> _channels = new Dictionary<Type, ChannelBase>();
        private readonly ConcurrentQueue<ChannelBase.Pending> _queue = new ConcurrentQueue<ChannelBase.Pending>();
        private Action<Type, object>[] _observers = Array.Empty<Action<Type, object>>();
        private int _handlerErrorCount;
        private bool _disposed;

        public EventBus(ILogger logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Maximum nesting of Publish calls on one thread.
        /// </summary>
        public int MaxDispatchDepth { get; set; } = 32;

        /// <summary>
        /// Number of handler/observer exceptions caught so far.
        /// </summary>
        public int HandlerErrorCount => Volatile.Read(ref _handlerErrorCount);

        public int PendingCount => _queue.Count;

        // ---------------- subscription ----------------

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.Normal, bool receiveSticky = true)
            where TEvent : IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Channel<TEvent> channel;
            bool added;
            lock (_gate)
            {
                ThrowIfDisposed();
                channel = GetOrCreateChannel<TEvent>();
                added = channel.AddSync(handler, priority);
            }

            if (!added)
            {
                // The token of a duplicate must not remove the original subscription
                _logger.LogWarning($"Duplicate subscription to {typeof(TEvent).Name} ignored ({handler.Method.Name})", Category);
                return Subscription.Empty;
            }

            if (receiveSticky && channel.TryGetSticky(out var sticky))
                Invoke(handler, sticky);

            return new Subscription(() => Unsubscribe(handler));
        }

        public IDisposable SubscribeAsync<TEvent>(Func<TEvent, CancellationToken, UniTask> handler, EventPriority priority = EventPriority.Normal)
            where TEvent : IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            bool added;
            lock (_gate)
            {
                ThrowIfDisposed();
                added = GetOrCreateChannel<TEvent>().AddAsync(handler, priority);
            }

            if (!added)
            {
                _logger.LogWarning($"Duplicate async subscription to {typeof(TEvent).Name} ignored ({handler.Method.Name})", Category);
                return Subscription.Empty;
            }

            return new Subscription(() => UnsubscribeAsync(handler));
        }

        public bool Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            lock (_gate)
                return TryGetChannel<TEvent>(out var channel) && channel.RemoveSync(handler);
        }

        public bool UnsubscribeAsync<TEvent>(Func<TEvent, CancellationToken, UniTask> handler) where TEvent : IEvent
        {
            lock (_gate)
                return TryGetChannel<TEvent>(out var channel) && channel.RemoveAsync(handler);
        }

        public int GetSubscriberCount<TEvent>() where TEvent : IEvent
        {
            lock (_gate)
                return TryGetChannel<TEvent>(out var channel) ? channel.SyncHandlers.Length + channel.AsyncHandlers.Length : 0;
        }

        public IDisposable AddObserver(Action<Type, object> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            lock (_gate)
            {
                var copy = new Action<Type, object>[_observers.Length + 1];
                _observers.CopyTo(copy, 0);
                copy[_observers.Length] = observer;
                _observers = copy;
            }

            return new Subscription(() =>
            {
                lock (_gate)
                {
                    var list = new List<Action<Type, object>>(_observers);
                    list.Remove(observer);
                    _observers = list.ToArray();
                }
            });
        }

        // ---------------- publishing ----------------

        public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (_disposed)
                return;

            Channel<TEvent> channel;
            lock (_gate)
                TryGetChannel(out channel);

            Dispatch(channel, eventData);
        }

        public void PublishSticky<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (_disposed)
                return;

            Channel<TEvent> channel;
            lock (_gate)
            {
                channel = GetOrCreateChannel<TEvent>();
                channel.SetSticky(eventData);
            }

            Dispatch(channel, eventData);
        }

        public async UniTask PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : IEvent
        {
            if (_disposed)
                return;

            Channel<TEvent> channel;
            lock (_gate)
                TryGetChannel(out channel);

            Dispatch(channel, eventData);

            var handlers = channel?.AsyncHandlers;
            if (handlers == null || handlers.Length == 0)
                return;

            var group = new List<UniTask>();
            int index = 0;
            while (index < handlers.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var priority = handlers[index].Priority;
                group.Clear();
                for (; index < handlers.Length && handlers[index].Priority == priority; index++)
                    group.Add(InvokeAsync(handlers[index].Handler, eventData, cancellationToken));

                await UniTask.WhenAll(group);
            }
        }

        public void Enqueue<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (_disposed)
                return;
            _queue.Enqueue(new ChannelBase.Pending<TEvent>(this, eventData));
        }

        public int Flush(int maxEvents = int.MaxValue)
        {
            int toProcess = Math.Min(_queue.Count, maxEvents);
            int processed = 0;
            while (processed < toProcess && _queue.TryDequeue(out var pending))
            {
                pending.Publish();
                processed++;
            }
            return processed;
        }

        // ---------------- sticky ----------------

        public bool TryGetSticky<TEvent>(out TEvent eventData) where TEvent : IEvent
        {
            lock (_gate)
            {
                if (TryGetChannel<TEvent>(out var channel) && channel.TryGetSticky(out eventData))
                    return true;
            }
            eventData = default;
            return false;
        }

        public bool ClearSticky<TEvent>() where TEvent : IEvent
        {
            lock (_gate)
                return TryGetChannel<TEvent>(out var channel) && channel.ClearSticky();
        }

        // ---------------- lifetime ----------------

        public void Clear()
        {
            lock (_gate)
            {
                _channels.Clear();
                _observers = Array.Empty<Action<Type, object>>();
            }
            while (_queue.TryDequeue(out _))
            {
            }
        }

        public void Dispose()
        {
            Clear();
            _disposed = true;
        }

        // ---------------- internals ----------------

        private void Dispatch<TEvent>(Channel<TEvent> channel, TEvent eventData) where TEvent : IEvent
        {
            if (_dispatchDepth >= MaxDispatchDepth)
            {
                _logger.LogError($"Publishing {typeof(TEvent).Name} refused: dispatch depth {MaxDispatchDepth} exceeded (recursive events?)", Category);
                Interlocked.Increment(ref _handlerErrorCount);
                return;
            }

            _dispatchDepth++;
            try
            {
                if (channel != null)
                {
                    foreach (var entry in channel.SyncHandlers)
                        Invoke(entry.Handler, eventData);
                }

                var observers = _observers;
                if (observers.Length > 0)
                {
                    object boxed = eventData;
                    foreach (var observer in observers)
                    {
                        try
                        {
                            observer(typeof(TEvent), boxed);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref _handlerErrorCount);
                            _logger.LogError($"Event observer failed for {typeof(TEvent).Name}", Category, ex);
                        }
                    }
                }
            }
            finally
            {
                _dispatchDepth--;
            }
        }

        private void Invoke<TEvent>(Action<TEvent> handler, TEvent eventData)
        {
            try
            {
                handler(eventData);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _handlerErrorCount);
                _logger.LogError($"Handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name} failed for {typeof(TEvent).Name}", Category, ex);
            }
        }

        private async UniTask InvokeAsync<TEvent>(Func<TEvent, CancellationToken, UniTask> handler, TEvent eventData, CancellationToken token)
        {
            try
            {
                await handler(eventData, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _handlerErrorCount);
                _logger.LogError($"Async handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name} failed for {typeof(TEvent).Name}", Category, ex);
            }
        }

        private Channel<TEvent> GetOrCreateChannel<TEvent>() where TEvent : IEvent
        {
            if (!_channels.TryGetValue(typeof(TEvent), out var channel))
            {
                channel = new Channel<TEvent>();
                _channels.Add(typeof(TEvent), channel);
            }
            return (Channel<TEvent>)channel;
        }

        private bool TryGetChannel<TEvent>(out Channel<TEvent> channel) where TEvent : IEvent
        {
            if (_channels.TryGetValue(typeof(TEvent), out var found))
            {
                channel = (Channel<TEvent>)found;
                return true;
            }
            channel = null;
            return false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventBus));
        }

        private abstract class ChannelBase
        {
            internal abstract class Pending
            {
                public abstract void Publish();
            }

            internal sealed class Pending<TEvent> : Pending where TEvent : IEvent
            {
                private readonly EventBus _bus;
                private readonly TEvent _eventData;

                public Pending(EventBus bus, TEvent eventData)
                {
                    _bus = bus;
                    _eventData = eventData;
                }

                public override void Publish() => _bus.Publish(_eventData);
            }
        }

        private sealed class Channel<TEvent> : ChannelBase
        {
            private static readonly HandlerEntry<Action<TEvent>>[] NoSync = Array.Empty<HandlerEntry<Action<TEvent>>>();
            private static readonly HandlerEntry<Func<TEvent, CancellationToken, UniTask>>[] NoAsync =
                Array.Empty<HandlerEntry<Func<TEvent, CancellationToken, UniTask>>>();

            private bool _hasSticky;
            private TEvent _sticky;

            public HandlerEntry<Action<TEvent>>[] SyncHandlers = NoSync;
            public HandlerEntry<Func<TEvent, CancellationToken, UniTask>>[] AsyncHandlers = NoAsync;

            public bool AddSync(Action<TEvent> handler, EventPriority priority) => Add(ref SyncHandlers, handler, priority);

            public bool AddAsync(Func<TEvent, CancellationToken, UniTask> handler, EventPriority priority) => Add(ref AsyncHandlers, handler, priority);

            public bool RemoveSync(Action<TEvent> handler) => Remove(ref SyncHandlers, handler);

            public bool RemoveAsync(Func<TEvent, CancellationToken, UniTask> handler) => Remove(ref AsyncHandlers, handler);

            public void SetSticky(TEvent value)
            {
                _sticky = value;
                _hasSticky = true;
            }

            public bool TryGetSticky(out TEvent value)
            {
                value = _sticky;
                return _hasSticky;
            }

            public bool ClearSticky()
            {
                bool had = _hasSticky;
                _hasSticky = false;
                _sticky = default;
                return had;
            }

            private static bool Add<THandler>(ref HandlerEntry<THandler>[] handlers, THandler handler, EventPriority priority)
                where THandler : Delegate
            {
                foreach (var entry in handlers)
                {
                    if (entry.Handler.Equals(handler))
                        return false;
                }

                // Insert after the last handler with priority >= new priority (stable, descending)
                int insertAt = handlers.Length;
                for (int i = 0; i < handlers.Length; i++)
                {
                    if (handlers[i].Priority < priority)
                    {
                        insertAt = i;
                        break;
                    }
                }

                var copy = new HandlerEntry<THandler>[handlers.Length + 1];
                Array.Copy(handlers, 0, copy, 0, insertAt);
                copy[insertAt] = new HandlerEntry<THandler>(handler, priority);
                Array.Copy(handlers, insertAt, copy, insertAt + 1, handlers.Length - insertAt);
                handlers = copy;
                return true;
            }

            private static bool Remove<THandler>(ref HandlerEntry<THandler>[] handlers, THandler handler) where THandler : Delegate
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    if (handlers[i].Handler.Equals(handler))
                    {
                        var copy = new HandlerEntry<THandler>[handlers.Length - 1];
                        Array.Copy(handlers, 0, copy, 0, i);
                        Array.Copy(handlers, i + 1, copy, i, handlers.Length - i - 1);
                        handlers = copy;
                        return true;
                    }
                }
                return false;
            }
        }

        private readonly struct HandlerEntry<THandler>
        {
            public HandlerEntry(THandler handler, EventPriority priority)
            {
                Handler = handler;
                Priority = priority;
            }

            public THandler Handler { get; }
            public EventPriority Priority { get; }
        }

        private sealed class Subscription : IDisposable
        {
            public static readonly Subscription Empty = new Subscription(null);

            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
