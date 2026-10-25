using System;
using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Implementation of event spawner.
    /// </summary>
    public class EventSpawner : IEventSpawner
    {
        private readonly Random _random = new();
        private readonly HashSet<string> _activeEventIds = new();

        public string SpawnerId { get; }
        public string EventTemplateId { get; }
        public string LocationId { get; }
        public EventSpawnRule SpawnRule { get; }
        public bool IsActive { get; private set; }
        public float TimeUntilNextSpawn { get; private set; }
        public int MaxConcurrentEvents { get; }
        public int ActiveEventCount => _activeEventIds.Count;

        public EventSpawner(
            string spawnerId,
            string eventTemplateId,
            string locationId,
            EventSpawnRule spawnRule,
            int maxConcurrentEvents = 1)
        {
            SpawnerId = spawnerId ?? throw new ArgumentNullException(nameof(spawnerId));
            EventTemplateId = eventTemplateId ?? throw new ArgumentNullException(nameof(eventTemplateId));
            LocationId = locationId ?? throw new ArgumentNullException(nameof(locationId));
            SpawnRule = spawnRule;
            MaxConcurrentEvents = Math.Max(1, maxConcurrentEvents);
            IsActive = true;
            TimeUntilNextSpawn = GetRandomSpawnTime();
        }

        public string? Update(float deltaTime, IReadOnlyDictionary<string, object> worldState)
        {
            if (!IsActive)
                return null;

            TimeUntilNextSpawn -= deltaTime;

            if (TimeUntilNextSpawn <= 0)
            {
                TimeUntilNextSpawn = GetRandomSpawnTime();

                if (CanSpawn(worldState))
                {
                    var eventId = $"{EventTemplateId}_{Guid.NewGuid():N}";
                    _activeEventIds.Add(eventId);
                    return eventId;
                }
            }

            return null;
        }

        public bool CanSpawn(IReadOnlyDictionary<string, object> worldState)
        {
            // Check max concurrent events
            if (_activeEventIds.Count >= MaxConcurrentEvents)
                return false;

            // Check spawn probability
            if (_random.NextDouble() > SpawnRule.SpawnProbability)
                return false;

            // Check player count
            if (worldState.TryGetValue($"PlayerCount_{LocationId}", out var playerCountObj))
            {
                if (playerCountObj is int playerCount)
                {
                    if (SpawnRule.MinPlayerCount > 0 && playerCount < SpawnRule.MinPlayerCount)
                        return false;

                    if (SpawnRule.MaxPlayerCount > 0 && playerCount > SpawnRule.MaxPlayerCount)
                        return false;
                }
            }

            // Check time of day
            if (SpawnRule.RequiredTimeOfDay >= 0)
            {
                if (worldState.TryGetValue("TimeOfDay", out var timeOfDayObj))
                {
                    if (timeOfDayObj is float timeOfDay)
                    {
                        if (Math.Abs(timeOfDay - SpawnRule.RequiredTimeOfDay) > 1.0f)
                            return false;
                    }
                }
            }

            // Check weather
            if (!string.IsNullOrEmpty(SpawnRule.RequiredWeather))
            {
                if (worldState.TryGetValue("Weather", out var weatherObj))
                {
                    if (weatherObj is string weather && weather != SpawnRule.RequiredWeather)
                        return false;
                }
            }

            // Check world state conditions
            foreach (var condition in SpawnRule.RequiredWorldState)
            {
                if (!worldState.TryGetValue(condition.Key, out var value) ||
                    !value.Equals(condition.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public void OnEventEnded(string eventId)
        {
            _activeEventIds.Remove(eventId);
        }

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        private float GetRandomSpawnTime()
        {
            return (float)(_random.NextDouble() *
                (SpawnRule.MaxSpawnInterval - SpawnRule.MinSpawnInterval) +
                SpawnRule.MinSpawnInterval);
        }
    }
}
