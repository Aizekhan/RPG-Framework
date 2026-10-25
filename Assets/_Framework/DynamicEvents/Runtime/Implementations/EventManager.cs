using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Central manager for dynamic events system.
    /// </summary>
    public class EventManager : IEventManager
    {
        private readonly Dictionary<string, IDynamicEvent> _activeEvents = new();
        private readonly Dictionary<string, Dictionary<string, IEventParticipant>> _eventParticipants = new();
        private readonly List<IEventSpawner> _spawners = new();
        private readonly Dictionary<string, Func<string, string, IDynamicEvent>> _eventFactories = new();

        public IReadOnlyDictionary<string, IDynamicEvent> ActiveEvents => _activeEvents;
        public IReadOnlyList<IEventSpawner> Spawners => _spawners.AsReadOnly();

        public IDynamicEvent CreateEvent(string templateId, string locationId)
        {
            if (string.IsNullOrEmpty(templateId))
                throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));

            if (string.IsNullOrEmpty(locationId))
                throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));

            if (!_eventFactories.TryGetValue(templateId, out var factory))
                throw new InvalidOperationException($"No factory registered for template: {templateId}");

            var eventId = $"{templateId}_{Guid.NewGuid():N}";
            var dynamicEvent = factory(eventId, locationId);

            _activeEvents[eventId] = dynamicEvent;
            _eventParticipants[eventId] = new Dictionary<string, IEventParticipant>();

            dynamicEvent.Start();

            return dynamicEvent;
        }

        public void RegisterEventFactory(string templateId, Func<string, string, IDynamicEvent> factory)
        {
            if (string.IsNullOrEmpty(templateId))
                throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _eventFactories[templateId] = factory;
        }

        public IDynamicEvent? GetEvent(string eventId)
        {
            return _activeEvents.TryGetValue(eventId, out var dynamicEvent) ? dynamicEvent : null;
        }

        public IReadOnlyList<IDynamicEvent> GetEventsAtLocation(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
                throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));

            return _activeEvents.Values
                .Where(e => e.LocationId == locationId && e.State == EventState.Active)
                .ToList();
        }

        public IEventParticipant? JoinEvent(string eventId, string participantId)
        {
            if (string.IsNullOrEmpty(eventId))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(eventId));

            if (string.IsNullOrEmpty(participantId))
                throw new ArgumentException("Participant ID cannot be null or empty", nameof(participantId));

            if (!_activeEvents.TryGetValue(eventId, out var dynamicEvent))
                return null;

            if (!dynamicEvent.CanJoin(participantId))
                return null;

            if (!_eventParticipants.TryGetValue(eventId, out var participants))
            {
                participants = new Dictionary<string, IEventParticipant>();
                _eventParticipants[eventId] = participants;
            }

            if (participants.ContainsKey(participantId))
                return participants[participantId];

            var participant = new EventParticipant(participantId, eventId, 0f);
            participants[participantId] = participant;

            // Update event participant count
            if (dynamicEvent is DynamicEvent de)
                de.SetParticipantCount(participants.Count);

            return participant;
        }

        public void LeaveEvent(string eventId, string participantId)
        {
            if (_eventParticipants.TryGetValue(eventId, out var participants))
            {
                if (participants.TryGetValue(participantId, out var participant))
                {
                    participant.Leave(0f);

                    // Update event participant count
                    if (_activeEvents.TryGetValue(eventId, out var dynamicEvent))
                    {
                        var activeCount = participants.Values.Count(p => p.IsActive);
                        if (dynamicEvent is DynamicEvent de)
                            de.SetParticipantCount(activeCount);
                    }
                }
            }
        }

        public IEventParticipant? GetParticipant(string eventId, string participantId)
        {
            if (_eventParticipants.TryGetValue(eventId, out var participants))
            {
                return participants.TryGetValue(participantId, out var participant)
                    ? participant
                    : null;
            }

            return null;
        }

        public IReadOnlyList<IEventParticipant> GetParticipants(string eventId)
        {
            if (_eventParticipants.TryGetValue(eventId, out var participants))
            {
                return participants.Values.ToList();
            }

            return Array.Empty<IEventParticipant>();
        }

        public void UpdateObjectiveProgress(string eventId, string objectiveId, int amount, string? contributorId = null)
        {
            if (!_activeEvents.TryGetValue(eventId, out var dynamicEvent))
                return;

            var objective = dynamicEvent.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId);
            if (objective == null)
                return;

            objective.UpdateProgress(amount, contributorId);

            // Add contribution to participant if specified
            if (!string.IsNullOrEmpty(contributorId))
            {
                var participant = GetParticipant(eventId, contributorId);
                participant?.AddContribution(amount * 10f); // Scale contribution
            }
        }

        public void ScaleEvent(string eventId)
        {
            if (!_activeEvents.TryGetValue(eventId, out var dynamicEvent))
                return;

            var participantCount = dynamicEvent.ParticipantCount;

            // Simple scaling based on participant count
            var newDifficulty = participantCount switch
            {
                <= 1 => EventDifficulty.Easy,
                <= 3 => EventDifficulty.Normal,
                <= 5 => EventDifficulty.Hard,
                <= 10 => EventDifficulty.Epic,
                _ => EventDifficulty.Legendary
            };

            if (dynamicEvent is DynamicEvent de)
                de.SetDifficulty(newDifficulty);
        }

        public IReadOnlyDictionary<string, object> DistributeRewards(string eventId)
        {
            var rewards = new Dictionary<string, object>();

            if (!_eventParticipants.TryGetValue(eventId, out var participants))
                return rewards;

            foreach (var participant in participants.Values.Where(p => p.IsEligibleForRewards))
            {
                var rewardTier = participant.ParticipationLevel switch
                {
                    ParticipationLevel.Helper => "bronze",
                    ParticipationLevel.Contributor => "silver",
                    ParticipationLevel.Hero => "gold",
                    ParticipationLevel.Legend => "legendary",
                    _ => "none"
                };

                rewards[participant.ParticipantId] = rewardTier;
            }

            return rewards;
        }

        public void RegisterSpawner(IEventSpawner spawner)
        {
            if (spawner == null)
                throw new ArgumentNullException(nameof(spawner));

            _spawners.Add(spawner);
        }

        public void UnregisterSpawner(string spawnerId)
        {
            var spawner = _spawners.FirstOrDefault(s => s.SpawnerId == spawnerId);
            if (spawner != null)
                _spawners.Remove(spawner);
        }

        public void Update(float deltaTime, IReadOnlyDictionary<string, object> worldState)
        {
            // Update spawners
            foreach (var spawner in _spawners.ToList())
            {
                var eventId = spawner.Update(deltaTime, worldState);
                if (!string.IsNullOrEmpty(eventId))
                {
                    // Spawner created an event, but we need the template registered
                    // In a real implementation, this would create the event from the template
                }
            }

            // Update active events
            var eventsToRemove = new List<string>();

            foreach (var kvp in _activeEvents.ToList())
            {
                var dynamicEvent = kvp.Value;
                dynamicEvent.Update(deltaTime);

                if (dynamicEvent.State == EventState.Success || dynamicEvent.State == EventState.Failed)
                {
                    // Notify spawners
                    var spawner = _spawners.FirstOrDefault(s => kvp.Key.StartsWith(s.EventTemplateId));
                    spawner?.OnEventEnded(kvp.Key);

                    eventsToRemove.Add(kvp.Key);
                }
            }

            // Remove completed events
            foreach (var eventId in eventsToRemove)
            {
                _activeEvents.Remove(eventId);
                _eventParticipants.Remove(eventId);
            }
        }
    }
}
