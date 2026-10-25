using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Implementation of a dynamic world event.
    /// </summary>
    public class DynamicEvent : IDynamicEvent
    {
        private readonly List<IEventObjective> _objectives = new();
        private readonly List<string> _tags = new();

        public string EventId { get; }
        public EventType EventType { get; }
        public EventState State { get; private set; }
        public EventDifficulty Difficulty { get; private set; }
        public string Name { get; }
        public string Description { get; }
        public string LocationId { get; }
        public float ParticipationRadius { get; }
        public float TimeRemaining { get; private set; }
        public float MaxDuration { get; }
        public int ParticipantCount { get; private set; }
        public IReadOnlyList<IEventObjective> Objectives => _objectives.AsReadOnly();
        public string? NextEventId { get; set; }
        public IReadOnlyList<string> Tags => _tags.AsReadOnly();

        public DynamicEvent(
            string eventId,
            EventType eventType,
            EventDifficulty difficulty,
            string name,
            string description,
            string locationId,
            float participationRadius,
            float maxDuration,
            IEnumerable<IEventObjective>? objectives = null,
            IEnumerable<string>? tags = null)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            EventType = eventType;
            Difficulty = difficulty;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            LocationId = locationId ?? throw new ArgumentNullException(nameof(locationId));
            ParticipationRadius = participationRadius;
            MaxDuration = maxDuration;
            TimeRemaining = maxDuration;
            State = EventState.Pending;
            ParticipantCount = 0;

            if (objectives != null)
                _objectives.AddRange(objectives);

            if (tags != null)
                _tags.AddRange(tags);
        }

        public void Start()
        {
            if (State != EventState.Pending)
                throw new InvalidOperationException($"Cannot start event in state {State}");

            State = EventState.Active;
            TimeRemaining = MaxDuration;
        }

        public void Update(float deltaTime)
        {
            if (State != EventState.Active)
                return;

            if (MaxDuration > 0)
            {
                TimeRemaining -= deltaTime;

                if (TimeRemaining <= 0)
                {
                    End(false); // Time expired, event failed
                    return;
                }
            }

            // Check if objectives are complete
            var requiredObjectives = _objectives.Where(o => !o.IsOptional).ToList();
            if (requiredObjectives.Count > 0 && requiredObjectives.All(o => o.IsComplete))
            {
                End(true); // All required objectives complete
            }
        }

        public void End(bool success)
        {
            if (State != EventState.Active)
                return;

            State = success ? EventState.Success : EventState.Failed;
            TimeRemaining = 0;
        }

        public bool CanJoin(string participantId)
        {
            if (string.IsNullOrEmpty(participantId))
                return false;

            return State == EventState.Active;
        }

        public float GetProgress()
        {
            if (_objectives.Count == 0)
                return 0f;

            var requiredObjectives = _objectives.Where(o => !o.IsOptional).ToList();
            if (requiredObjectives.Count == 0)
                return 1.0f;

            var totalProgress = requiredObjectives.Sum(o => o.GetProgressPercentage());
            return totalProgress / requiredObjectives.Count;
        }

        public void SetParticipantCount(int count)
        {
            ParticipantCount = Math.Max(0, count);
        }

        public void SetDifficulty(EventDifficulty difficulty)
        {
            Difficulty = difficulty;
        }

        public void AddObjective(IEventObjective objective)
        {
            if (objective == null)
                throw new ArgumentNullException(nameof(objective));

            _objectives.Add(objective);
        }
    }
}
