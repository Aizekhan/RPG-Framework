using System;
using System.Collections.Generic;

namespace RPGFramework.Events
{
    public enum ThrottleMode
    {
        /// <summary>
        /// Publish the first event of a window immediately and the latest dropped one when the window ends.
        /// </summary>
        LeadingAndTrailing,

        /// <summary>
        /// Publish only the first event of a window; drop the rest.
        /// </summary>
        Leading,

        /// <summary>
        /// Publish only the latest event of a window, when the window ends.
        /// </summary>
        Trailing
    }

    /// <summary>
    /// Rate-limits high-frequency events (health ticks, timers, cursor moves) per event type.
    /// Time advances only through <see cref="Update"/>, so behaviour is deterministic and testable.
    /// Types without a configured throttle pass straight through.
    /// </summary>
    public sealed class EventThrottler
    {
        private readonly IEventPublisher _publisher;
        private readonly Dictionary<Type, ThrottleState> _states = new Dictionary<Type, ThrottleState>();
        private double _now;

        public EventThrottler(IEventPublisher publisher)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        /// <summary>
        /// Seconds of throttler time elapsed (sum of Update deltas).
        /// </summary>
        public double Now => _now;

        public void Configure<TEvent>(float intervalSeconds, ThrottleMode mode = ThrottleMode.LeadingAndTrailing) where TEvent : IEvent
        {
            if (intervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            _states[typeof(TEvent)] = new ThrottleState<TEvent>(intervalSeconds, mode);
        }

        public bool Remove<TEvent>() where TEvent : IEvent => _states.Remove(typeof(TEvent));

        /// <summary>
        /// Publishes now, defers or drops the event according to its throttle.
        /// </summary>
        /// <returns>True if the event was published immediately.</returns>
        public bool Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (!_states.TryGetValue(typeof(TEvent), out var state))
            {
                _publisher.Publish(eventData);
                return true;
            }

            return ((ThrottleState<TEvent>)state).Offer(eventData, _now, _publisher);
        }

        /// <summary>
        /// Advances time and publishes trailing events whose window has closed.
        /// </summary>
        public void Update(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            _now += deltaSeconds;
            foreach (var state in _states.Values)
                state.Tick(_now, _publisher);
        }

        /// <summary>
        /// Publishes every pending trailing event now.
        /// </summary>
        public void FlushPending()
        {
            foreach (var state in _states.Values)
                state.Flush(_now, _publisher);
        }

        private abstract class ThrottleState
        {
            public abstract void Tick(double now, IEventPublisher publisher);
            public abstract void Flush(double now, IEventPublisher publisher);
        }

        private sealed class ThrottleState<TEvent> : ThrottleState where TEvent : IEvent
        {
            private readonly double _interval;
            private readonly ThrottleMode _mode;
            private double _windowStart = double.NegativeInfinity;
            private bool _hasPending;
            private TEvent _pending;

            public ThrottleState(double interval, ThrottleMode mode)
            {
                _interval = interval;
                _mode = mode;
            }

            public bool Offer(TEvent eventData, double now, IEventPublisher publisher)
            {
                bool windowOpen = now - _windowStart < _interval;

                if (!windowOpen)
                {
                    _windowStart = now;
                    if (_mode != ThrottleMode.Trailing)
                    {
                        publisher.Publish(eventData);
                        return true;
                    }
                }

                if (_mode != ThrottleMode.Leading)
                {
                    _pending = eventData;
                    _hasPending = true;
                }
                return false;
            }

            public override void Tick(double now, IEventPublisher publisher)
            {
                if (_hasPending && now - _windowStart >= _interval)
                    Flush(now, publisher);
            }

            public override void Flush(double now, IEventPublisher publisher)
            {
                if (!_hasPending)
                    return;
                var eventData = _pending;
                _pending = default;
                _hasPending = false;
                _windowStart = now;
                publisher.Publish(eventData);
            }
        }
    }
}
