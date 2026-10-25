using System;
using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Implementation of event chain with branching.
    /// </summary>
    public class EventChain : IEventChain
    {
        private readonly Dictionary<string, (string successEvent, string failureEvent, string description)> _chainMap = new();
        private string? _currentEventId;

        public string ChainId { get; }
        public string Name { get; }
        public int CurrentStep { get; private set; }
        public int TotalSteps { get; }
        public bool IsComplete { get; private set; }
        public bool IsSuccessful { get; private set; }
        public string? CurrentEventId => _currentEventId;

        public EventChain(string chainId, string name, int totalSteps)
        {
            ChainId = chainId ?? throw new ArgumentNullException(nameof(chainId));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TotalSteps = Math.Max(1, totalSteps);
            CurrentStep = 0;
            IsComplete = false;
            IsSuccessful = false;
        }

        public void AddChainLink(
            string eventTemplateId,
            string successNextEvent,
            string failureNextEvent,
            string description)
        {
            if (string.IsNullOrEmpty(eventTemplateId))
                throw new ArgumentException("Event template ID cannot be null or empty", nameof(eventTemplateId));

            _chainMap[eventTemplateId] = (successNextEvent, failureNextEvent, description);
        }

        public void SetInitialEvent(string eventTemplateId)
        {
            if (string.IsNullOrEmpty(eventTemplateId))
                throw new ArgumentException("Event template ID cannot be null or empty", nameof(eventTemplateId));

            _currentEventId = eventTemplateId;
            CurrentStep = 0;
            IsComplete = false;
            IsSuccessful = false;
        }

        public string? GetNextEvent(string currentEventId, bool success)
        {
            if (string.IsNullOrEmpty(currentEventId))
                throw new ArgumentException("Current event ID cannot be null or empty", nameof(currentEventId));

            if (!_chainMap.TryGetValue(currentEventId, out var chainLink))
                return null;

            return success ? chainLink.successEvent : chainLink.failureEvent;
        }

        public string? AdvanceChain(bool success)
        {
            if (IsComplete || _currentEventId == null)
                return null;

            var nextEvent = GetNextEvent(_currentEventId, success);

            if (string.IsNullOrEmpty(nextEvent))
            {
                // Chain is complete
                IsComplete = true;
                IsSuccessful = success;
                return null;
            }

            _currentEventId = nextEvent;
            CurrentStep++;

            if (CurrentStep >= TotalSteps)
            {
                IsComplete = true;
                IsSuccessful = success;
            }

            return nextEvent;
        }

        public void Reset()
        {
            CurrentStep = 0;
            IsComplete = false;
            IsSuccessful = false;
            _currentEventId = null;
        }

        public string GetCurrentStepDescription()
        {
            if (_currentEventId == null || !_chainMap.TryGetValue(_currentEventId, out var chainLink))
                return "No active step";

            return chainLink.description;
        }
    }
}
