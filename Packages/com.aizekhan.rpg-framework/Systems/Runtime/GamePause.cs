using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Systems
{
    /// <summary>
    /// Stops the game's time for its systems: while paused the world still ticks them, with a delta of 0, so timers,
    /// effects, cooldowns, waves and movement stand still while events are still delivered. A rule asks for a pause
    /// with a reason ("vote", "cutscene", "menu") and lifts its own; the game runs again once no reason is left, so
    /// a menu and a vote do not undo each other.
    /// </summary>
    public interface IGamePause
    {
        bool IsPaused { get; }

        /// <summary>
        /// Why the game is paused now (empty when it runs).
        /// </summary>
        IReadOnlyCollection<string> Reasons { get; }

        /// <returns>False when the game was already paused for this reason.</returns>
        bool Pause(string reason);

        /// <returns>False when the game was not paused for this reason.</returns>
        bool Resume(string reason);

        /// <summary>
        /// Lifts every reason (a new run, leaving to the menu).
        /// </summary>
        void ResumeAll();
    }

    /// <summary>
    /// The game stopped (the first reason to pause). Published by <see cref="GamePause"/>.
    /// </summary>
    public readonly struct GamePausedEvent : IEvent
    {
        public GamePausedEvent(string reason) => Reason = reason;

        public string Reason { get; }
    }

    /// <summary>
    /// The game runs again (the last reason was lifted). Published by <see cref="GamePause"/>.
    /// </summary>
    public readonly struct GameResumedEvent : IEvent
    {
        public GameResumedEvent(string reason) => Reason = reason;

        /// <summary>
        /// The reason lifted last.
        /// </summary>
        public string Reason { get; }
    }

    public sealed class GamePause : IGamePause
    {
        private readonly HashSet<string> _reasons = new HashSet<string>(StringComparer.Ordinal);
        private readonly IEventPublisher _events;

        /// <param name="events">Where the paused and resumed events go; null publishes nothing.</param>
        public GamePause(IEventPublisher events = null)
        {
            _events = events;
        }

        public bool IsPaused => _reasons.Count > 0;

        public IReadOnlyCollection<string> Reasons => _reasons;

        public bool Pause(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Say why the game pauses, so the same rule can lift it", nameof(reason));
            if (!_reasons.Add(reason))
                return false;
            if (_reasons.Count == 1)
                _events?.Publish(new GamePausedEvent(reason));
            return true;
        }

        public bool Resume(string reason)
        {
            if (reason == null || !_reasons.Remove(reason))
                return false;
            if (_reasons.Count == 0)
                _events?.Publish(new GameResumedEvent(reason));
            return true;
        }

        public void ResumeAll()
        {
            if (_reasons.Count == 0)
                return;
            _reasons.Clear();
            _events?.Publish(new GameResumedEvent(null));
        }
    }
}
