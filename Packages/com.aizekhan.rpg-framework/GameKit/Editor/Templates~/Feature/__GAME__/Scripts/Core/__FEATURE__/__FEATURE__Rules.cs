using System;
using RPGFramework.Events;

namespace __GAME__
{
    /// <summary>
    /// Rules of __FEATURE__: own the feature's state, change it only through these methods and announce every
    /// change on the bus, so views and other features can react without knowing this class.
    /// The counter below is the generated example; replace it with the feature's real state and actions.
    /// </summary>
    public sealed class __FEATURE__Rules
    {
        private readonly IEventPublisher _events;

        public __FEATURE__Rules(IEventPublisher events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public int Value { get; private set; }

        public void Add(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Add a positive amount");

            int old = Value;
            Value += amount;
            _events.Publish(new __FEATURE__ChangedEvent(old, Value));
        }
    }
}
