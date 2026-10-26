using System.Collections.Generic;

namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Rules for spawning dynamic events.
    /// </summary>
    public readonly struct EventSpawnRule
    {
        /// <summary>
        /// Minimum time between spawn attempts (in seconds).
        /// </summary>
        public float MinSpawnInterval { get; }

        /// <summary>
        /// Maximum time between spawn attempts (in seconds).
        /// </summary>
        public float MaxSpawnInterval { get; }

        /// <summary>
        /// Minimum player count in the area required to spawn.
        /// </summary>
        public int MinPlayerCount { get; }

        /// <summary>
        /// Maximum player count in the area (0 for unlimited).
        /// </summary>
        public int MaxPlayerCount { get; }

        /// <summary>
        /// Probability of spawning when conditions are met (0.0 to 1.0).
        /// </summary>
        public float SpawnProbability { get; }

        /// <summary>
        /// Optional time of day requirement (0-24 hours, -1 for any time).
        /// </summary>
        public float RequiredTimeOfDay { get; }

        /// <summary>
        /// Optional weather requirement (empty for any weather).
        /// </summary>
        public string RequiredWeather { get; }

        /// <summary>
        /// Optional world state conditions (key-value pairs).
        /// </summary>
        public IReadOnlyDictionary<string, object> RequiredWorldState { get; }

        public EventSpawnRule(
            float minSpawnInterval,
            float maxSpawnInterval,
            int minPlayerCount = 0,
            int maxPlayerCount = 0,
            float spawnProbability = 1.0f,
            float requiredTimeOfDay = -1f,
            string requiredWeather = "",
            IReadOnlyDictionary<string, object>? requiredWorldState = null)
        {
            MinSpawnInterval = minSpawnInterval;
            MaxSpawnInterval = maxSpawnInterval;
            MinPlayerCount = minPlayerCount;
            MaxPlayerCount = maxPlayerCount;
            SpawnProbability = spawnProbability;
            RequiredTimeOfDay = requiredTimeOfDay;
            RequiredWeather = requiredWeather;
            RequiredWorldState = requiredWorldState ?? new Dictionary<string, object>();
        }
    }
}
