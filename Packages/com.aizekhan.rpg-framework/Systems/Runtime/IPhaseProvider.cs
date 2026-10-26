using System;
using RPGFramework.Events;

namespace RPGFramework.Systems
{
    /// <summary>
    /// Supplies the id of the current game phase (e.g. "Exploration", "Combat").
    /// Phases are plain strings so the framework never hard-codes a game's phase list.
    /// </summary>
    public interface IPhaseProvider
    {
        string CurrentPhaseId { get; }
    }

    public readonly struct PhaseChangedEvent : IEvent
    {
        public PhaseChangedEvent(string previousPhaseId, string currentPhaseId)
        {
            PreviousPhaseId = previousPhaseId;
            CurrentPhaseId = currentPhaseId;
        }

        public string PreviousPhaseId { get; }
        public string CurrentPhaseId { get; }
    }

    /// <summary>
    /// Minimal mutable <see cref="IPhaseProvider"/>; publishes <see cref="PhaseChangedEvent"/> as a sticky
    /// event so late subscribers learn the current phase.
    /// </summary>
    public sealed class PhaseTracker : IPhaseProvider
    {
        private readonly IEventPublisher _publisher;

        public PhaseTracker(IEventPublisher publisher = null, string initialPhaseId = null)
        {
            _publisher = publisher;
            CurrentPhaseId = initialPhaseId;
        }

        public string CurrentPhaseId { get; private set; }

        public event Action<string, string> PhaseChanged;

        /// <returns>False if the phase was already current.</returns>
        public bool SetPhase(string phaseId)
        {
            if (string.Equals(CurrentPhaseId, phaseId, StringComparison.Ordinal))
                return false;

            var previous = CurrentPhaseId;
            CurrentPhaseId = phaseId;
            PhaseChanged?.Invoke(previous, phaseId);
            _publisher?.PublishSticky(new PhaseChangedEvent(previous, phaseId));
            return true;
        }
    }
}
