using System;
using System.Collections.Generic;

namespace RPGFramework.Telemetry
{
    /// <summary>
    /// One telemetry event: its name, where it happened (level, zone, entity, mechanic, quest, position) and up to
    /// <see cref="MaxProperties"/> properties of plain values. The client (<see cref="TelemetryClient"/>) adds the id,
    /// time, build, session, player and context when it is tracked; after that it can no longer change.
    /// </summary>
    public sealed class TelemetryEvent
    {
        /// <summary>
        /// The most properties one event carries.
        /// </summary>
        public const int MaxProperties = 20;

        private readonly List<KeyValuePair<string, object>> _properties = new List<KeyValuePair<string, object>>();
        private string _levelId;
        private string _zoneId;
        private string _entityId;
        private string _mechanicId;
        private string _questId;
        private double? _gameTime;
        private TelemetryPosition? _position;

        public TelemetryEvent(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An event needs a name", nameof(name));
            Name = name;
        }

        public string Name { get; }

        /// <summary>
        /// The client tracked it: it is in the queue and cannot change.
        /// </summary>
        public bool IsTracked { get; private set; }

        public string LevelId { get => _levelId; set => _levelId = Open(value); }
        public string ZoneId { get => _zoneId; set => _zoneId = Open(value); }
        public string EntityId { get => _entityId; set => _entityId = Open(value); }

        /// <summary>
        /// The design id of the mechanic it measures (the game's own ids).
        /// </summary>
        public string MechanicId { get => _mechanicId; set => _mechanicId = Open(value); }

        public string QuestId { get => _questId; set => _questId = Open(value); }

        /// <summary>
        /// Seconds of play (the game's clock); null when not given.
        /// </summary>
        public double? GameTime { get => _gameTime; set => _gameTime = Open(value); }

        public TelemetryPosition? Position { get => _position; set => _position = Open(value); }

        public IReadOnlyList<KeyValuePair<string, object>> Properties => _properties;

        // Filled by the client when tracked
        internal string EventId;
        internal long Timestamp;
        internal TelemetryIdentity Identity;
        internal TelemetryContext Context;

        public TelemetryEvent Set(string key, string value) => Add(key, value);
        public TelemetryEvent Set(string key, double value) => Add(key, value);
        public TelemetryEvent Set(string key, long value) => Add(key, value);
        public TelemetryEvent Set(string key, int value) => Add(key, (long)value);
        public TelemetryEvent Set(string key, bool value) => Add(key, value);

        /// <returns>The value, or null when the event has no such property.</returns>
        public object Get(string key)
        {
            foreach (var property in _properties)
            {
                if (property.Key == key)
                    return property.Value;
            }
            return null;
        }

        internal void Track(string eventId, long timestamp, TelemetryIdentity identity, TelemetryContext context)
        {
            EventId = eventId;
            Timestamp = timestamp;
            Identity = identity;
            Context = context;
            IsTracked = true;
        }

        private TelemetryEvent Add(string key, object value)
        {
            Open(key);
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("A property needs a key", nameof(key));
            if (value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A finite number");
            for (int i = 0; i < _properties.Count; i++)
            {
                if (_properties[i].Key != key)
                    continue;
                _properties[i] = new KeyValuePair<string, object>(key, value);
                return this;
            }
            if (_properties.Count >= MaxProperties)
                throw new InvalidOperationException($"An event carries at most {MaxProperties} properties");
            _properties.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        private T Open<T>(T value)
        {
            if (IsTracked)
                throw new InvalidOperationException($"'{Name}' is already tracked and cannot change");
            return value;
        }
    }

    /// <summary>
    /// A point in the world, in metres.
    /// </summary>
    public readonly struct TelemetryPosition
    {
        public TelemetryPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    // Who and what sent an event: kept per event, so a batch stays right when the player or the build changes
    internal sealed class TelemetryIdentity
    {
        public TelemetryIdentity(int schemaVersion, string buildId, string sessionId, string playerId)
        {
            SchemaVersion = schemaVersion;
            BuildId = buildId;
            SessionId = sessionId;
            PlayerId = playerId;
        }

        public int SchemaVersion { get; }
        public string BuildId { get; }
        public string SessionId { get; }
        public string PlayerId { get; }
    }

    /// <summary>
    /// What every event of a session shares: the platform, the game mode, the player's cohort and the A/B variant.
    /// </summary>
    public sealed class TelemetryContext
    {
        public TelemetryContext(string platform, string mode, string cohort, string variant)
        {
            Platform = platform;
            Mode = mode;
            Cohort = cohort;
            Variant = variant;
        }

        public string Platform { get; }
        public string Mode { get; }
        public string Cohort { get; }
        public string Variant { get; }
    }
}
