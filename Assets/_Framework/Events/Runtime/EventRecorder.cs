using System;
using System.Collections.Generic;

namespace RPGFramework.Events
{
    /// <summary>
    /// One event captured by <see cref="EventRecorder"/>.
    /// </summary>
    public readonly struct RecordedEvent
    {
        public RecordedEvent(long sequence, DateTime timestampUtc, Type eventType, object payload)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            EventType = eventType;
            Payload = payload;
        }

        public long Sequence { get; }
        public DateTime TimestampUtc { get; }
        public Type EventType { get; }
        public object Payload { get; }

        public override string ToString() => $"#{Sequence} {TimestampUtc:HH:mm:ss.fff} {EventType.Name} {Payload}";
    }

    /// <summary>
    /// Diagnostic ring buffer of recently published events (debug overlays, bug reports, replay research).
    /// Attaches as a bus observer, so it boxes struct events; keep it off in release builds.
    /// </summary>
    public sealed class EventRecorder : IDisposable
    {
        private readonly object _gate = new object();
        private readonly RecordedEvent[] _buffer;
        private readonly IDisposable _observer;
        private readonly Func<Type, bool> _filter;
        private int _start;
        private int _count;
        private long _sequence;

        /// <param name="filter">Optional predicate; only matching event types are recorded.</param>
        public EventRecorder(IEventBus bus, int capacity = 256, Func<Type, bool> filter = null)
        {
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _buffer = new RecordedEvent[capacity];
            _filter = filter;
            _observer = bus.AddObserver(Record);
        }

        public bool IsPaused { get; set; }

        /// <summary>
        /// Total events recorded since creation (including ones already evicted).
        /// </summary>
        public long TotalRecorded
        {
            get
            {
                lock (_gate)
                    return _sequence;
            }
        }

        public IReadOnlyList<RecordedEvent> Snapshot()
        {
            lock (_gate)
            {
                var result = new RecordedEvent[_count];
                for (int i = 0; i < _count; i++)
                    result[i] = _buffer[(_start + i) % _buffer.Length];
                return result;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _start = 0;
                _count = 0;
            }
        }

        public void Dispose() => _observer.Dispose();

        private void Record(Type eventType, object payload)
        {
            if (IsPaused || (_filter != null && !_filter(eventType)))
                return;

            lock (_gate)
            {
                var entry = new RecordedEvent(++_sequence, DateTime.UtcNow, eventType, payload);
                int index = (_start + _count) % _buffer.Length;
                _buffer[index] = entry;
                if (_count < _buffer.Length)
                    _count++;
                else
                    _start = (_start + 1) % _buffer.Length;
            }
        }
    }
}
