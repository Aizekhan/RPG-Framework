using System;
using RPGFramework.Events;

namespace __GAME__
{
    /// <summary>
    /// One play session of __GAME__: the entry point of the rules. Plain C#: no UnityEngine (rule GA03).
    /// </summary>
    public sealed class __GAME__Session
    {
        private readonly IEventPublisher _events;

        public __GAME__Session(IEventPublisher events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public bool IsStarted { get; private set; }

        public void Start()
        {
            if (IsStarted)
                throw new InvalidOperationException("The session is already started");

            IsStarted = true;
            _events.Publish(new __GAME__StartedEvent());
        }
    }
}
