using System;
using System.Collections.Generic;

namespace RPGFramework.Events
{
    /// <summary>
    /// Collects many small events of one type and publishes a single batch event instead
    /// (e.g. 50 DamageDealt per frame → one DamageBatch for UI numbers).
    /// A batch is published when it reaches its size limit, when its maximum delay elapses
    /// (via <see cref="Update"/>), or on <see cref="FlushAll"/>.
    /// </summary>
    public sealed class EventBatcher
    {
        private readonly IEventPublisher _publisher;
        private readonly Dictionary<Type, Batch> _batches = new Dictionary<Type, Batch>();

        public EventBatcher(IEventPublisher publisher)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        public void Configure<TEvent, TBatchEvent>(
            Func<IReadOnlyList<TEvent>, TBatchEvent> createBatch,
            int maxBatchSize = 32,
            float maxDelaySeconds = 0.1f)
            where TEvent : IEvent
            where TBatchEvent : IEvent
        {
            if (createBatch == null)
                throw new ArgumentNullException(nameof(createBatch));
            if (maxBatchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBatchSize));
            if (maxDelaySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxDelaySeconds));

            _batches[typeof(TEvent)] = new Batch<TEvent, TBatchEvent>(createBatch, maxBatchSize, maxDelaySeconds);
        }

        /// <summary>
        /// Adds the event to its batch. Throws if the type was not configured.
        /// </summary>
        public void Add<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (!_batches.TryGetValue(typeof(TEvent), out var batch))
                throw new InvalidOperationException($"No batch configured for {typeof(TEvent).Name}");
            ((IBatch<TEvent>)batch).Add(eventData, _publisher);
        }

        public int PendingCount<TEvent>() where TEvent : IEvent =>
            _batches.TryGetValue(typeof(TEvent), out var batch) ? batch.Count : 0;

        public void Update(float deltaSeconds)
        {
            foreach (var batch in _batches.Values)
                batch.Tick(deltaSeconds, _publisher);
        }

        public void FlushAll()
        {
            foreach (var batch in _batches.Values)
                batch.Flush(_publisher);
        }

        private abstract class Batch
        {
            public abstract int Count { get; }
            public abstract void Tick(float deltaSeconds, IEventPublisher publisher);
            public abstract void Flush(IEventPublisher publisher);
        }

        private interface IBatch<in TEvent>
        {
            void Add(TEvent eventData, IEventPublisher publisher);
        }

        private sealed class Batch<TEvent, TBatchEvent> : Batch, IBatch<TEvent>
            where TEvent : IEvent
            where TBatchEvent : IEvent
        {
            private readonly Func<IReadOnlyList<TEvent>, TBatchEvent> _createBatch;
            private readonly int _maxSize;
            private readonly float _maxDelay;
            private List<TEvent> _items = new List<TEvent>();
            private float _age;

            public Batch(Func<IReadOnlyList<TEvent>, TBatchEvent> createBatch, int maxSize, float maxDelay)
            {
                _createBatch = createBatch;
                _maxSize = maxSize;
                _maxDelay = maxDelay;
            }

            public override int Count => _items.Count;

            public void Add(TEvent eventData, IEventPublisher publisher)
            {
                if (_items.Count == 0)
                    _age = 0f;
                _items.Add(eventData);
                if (_items.Count >= _maxSize)
                    Flush(publisher);
            }

            public override void Tick(float deltaSeconds, IEventPublisher publisher)
            {
                if (_items.Count == 0)
                    return;
                _age += deltaSeconds;
                if (_age >= _maxDelay)
                    Flush(publisher);
            }

            public override void Flush(IEventPublisher publisher)
            {
                if (_items.Count == 0)
                    return;
                // Hand the list to the batch event and start a fresh one, so handlers may keep the list
                var items = _items;
                _items = new List<TEvent>();
                publisher.Publish(_createBatch(items));
            }
        }
    }
}
