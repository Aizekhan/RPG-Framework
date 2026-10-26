using System;
using RPGFramework.ActionRpg;
using RPGFramework.Events;

namespace __GAME__
{
    /// <summary>
    /// One play session of __GAME__: the place for rules the Game Setup cannot express. Plain C#: no UnityEngine
    /// (rule GA03). Rules react to framework events; this one keeps the best wave reached.
    /// </summary>
    public sealed class __GAME__Session : IDisposable
    {
        private readonly IEventBus _events;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        public __GAME__Session(IEventBus events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public bool IsStarted { get; private set; }

        /// <summary>
        /// Highest wave started in this session.
        /// </summary>
        public int BestWave { get; private set; }

        public void Start()
        {
            if (IsStarted)
                throw new InvalidOperationException("The session is already started");

            IsStarted = true;
            _events.Subscribe<WaveStartedEvent>(wave => BestWave = Math.Max(BestWave, wave.Wave)).AddTo(_subscriptions);
            _events.Publish(new __GAME__StartedEvent());
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
