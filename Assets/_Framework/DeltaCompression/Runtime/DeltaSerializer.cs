using System;
using System.Collections.Generic;
using System.IO;
using RPGFramework.Serialization;

namespace RPGFramework.DeltaCompression
{
    /// <summary>
    /// Stateless delta compression serializer.
    /// Caller manages last-known-state via entityId.
    /// Framework implementation - no game/network dependencies.
    /// </summary>
    public class DeltaSerializer : IDeltaSerializer
    {
        private readonly Dictionary<int, byte[]> _lastState = new Dictionary<int, byte[]>();
        private readonly object _lock = new object();
        private readonly float _deltaThreshold;

        /// <summary>
        /// Creates a delta serializer.
        /// </summary>
        /// <param name="deltaThreshold">Threshold ratio (0-1) for using delta vs full state. Default: 0.8 (80%)</param>
        public DeltaSerializer(float deltaThreshold = 0.8f)
        {
            if (deltaThreshold < 0 || deltaThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(deltaThreshold), "Must be between 0 and 1");

            _deltaThreshold = deltaThreshold;
        }

        public byte[] CreateDelta<T>(int entityId, T currentState) where T : ISerializable
        {
            if (currentState == null)
                throw new ArgumentNullException(nameof(currentState));

            var fullState = currentState.Serialize();

            lock (_lock)
            {
                if (!_lastState.TryGetValue(entityId, out var previousState))
                {
                    // First update - store and send full state
                    _lastState[entityId] = fullState;
                    return CreateFullStatePacket(fullState);
                }

                // Create delta
                var delta = CreateStateDelta(previousState, fullState);

                // Update cached state
                _lastState[entityId] = fullState;

                // Use delta only if it's smaller than threshold
                return delta.Length < fullState.Length * _deltaThreshold
                    ? delta
                    : CreateFullStatePacket(fullState);
            }
        }

        public T ApplyDelta<T>(int entityId, byte[] delta) where T : ISerializable, new()
        {
            if (delta == null)
                throw new ArgumentNullException(nameof(delta));

            using (var stream = new MemoryStream(delta))
            using (var reader = new BinaryReader(stream))
            {
                var isFullState = reader.ReadBoolean();

                if (isFullState)
                {
                    // Full state packet
                    var length = reader.ReadInt32();
                    var fullState = reader.ReadBytes(length);

                    lock (_lock)
                    {
                        _lastState[entityId] = fullState;
                    }

                    var result = new T();
                    result.Deserialize(fullState);
                    return result;
                }
                else
                {
                    // Delta packet
                    byte[] baseState;
                    lock (_lock)
                    {
                        if (!_lastState.TryGetValue(entityId, out baseState))
                        {
                            throw new InvalidOperationException(
                                $"Cannot apply delta: no base state for entity {entityId}");
                        }
                    }

                    // Read and apply changes
                    var changeCount = reader.ReadInt32();
                    var newState = new byte[baseState.Length];
                    Array.Copy(baseState, newState, baseState.Length);

                    for (int i = 0; i < changeCount; i++)
                    {
                        var offset = reader.ReadInt32();
                        var value = reader.ReadByte();

                        if (offset < newState.Length)
                            newState[offset] = value;
                    }

                    lock (_lock)
                    {
                        _lastState[entityId] = newState;
                    }

                    var result = new T();
                    result.Deserialize(newState);
                    return result;
                }
            }
        }

        public void ClearState(int entityId)
        {
            lock (_lock)
            {
                _lastState.Remove(entityId);
            }
        }

        public void ClearAllStates()
        {
            lock (_lock)
            {
                _lastState.Clear();
            }
        }

        /// <summary>
        /// Creates a full state packet.
        /// Wire format: [IsFullState=true (bool)] [Length (int32)] [FullState (byte[])]
        /// </summary>
        private byte[] CreateFullStatePacket(byte[] state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(true); // isFullState
                writer.Write(state.Length);
                writer.Write(state);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Creates a delta packet.
        /// Wire format: [IsFullState=false (bool)] [ChangeCount (int32)] [Offset (int32), Value (byte)]...
        /// </summary>
        private byte[] CreateStateDelta(byte[] oldState, byte[] newState)
        {
            var changes = new List<KeyValuePair<int, byte>>();

            // Find byte-level differences
            var minLength = Math.Min(oldState.Length, newState.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (oldState[i] != newState[i])
                {
                    changes.Add(new KeyValuePair<int, byte>(i, newState[i]));
                }
            }

            // If new state is longer, add new bytes
            for (int i = minLength; i < newState.Length; i++)
            {
                changes.Add(new KeyValuePair<int, byte>(i, newState[i]));
            }

            // Create delta packet
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(false); // isFullState
                writer.Write(changes.Count);

                foreach (var change in changes)
                {
                    writer.Write(change.Key);   // offset
                    writer.Write(change.Value); // value
                }

                return stream.ToArray();
            }
        }
    }
}
