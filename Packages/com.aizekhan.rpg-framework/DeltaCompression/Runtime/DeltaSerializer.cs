using System;
using System.Collections.Generic;
using System.IO;
using RPGFramework.Serialization;

namespace RPGFramework.DeltaCompression
{
    /// <summary>
    /// Delta compression serializer: after the first full state, sends only the byte runs that changed.
    /// The sender and the receiver each keep the last state per entityId.
    /// A delta names the state it was built on (its length and checksum); a receiver whose state differs (a packet
    /// was lost) refuses it with <see cref="InvalidOperationException"/>, and the sender calls <see cref="ClearState"/>
    /// so its next packet is a full state. Send over a reliable, ordered channel and resync on refusal.
    /// Broken packets are refused with <see cref="InvalidDataException"/> and leave the receiver's state as it was.
    /// Framework implementation - no game/network dependencies.
    /// </summary>
    public class DeltaSerializer : IDeltaSerializer
    {
        private const byte FullStateKind = 1;
        private const byte DeltaKind = 0;

        // Unchanged bytes between two changes are sent inside one run when that is cheaper than a new run header
        private const int RunHeaderSize = 8;

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

            var fullState = currentState.Serialize() ?? Array.Empty<byte>();

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
                var fullPacketSize = fullState.Length + 5;
                return delta.Length < fullPacketSize * _deltaThreshold
                    ? delta
                    : CreateFullStatePacket(fullState);
            }
        }

        public T ApplyDelta<T>(int entityId, byte[] delta) where T : ISerializable, new()
        {
            if (delta == null)
                throw new ArgumentNullException(nameof(delta));

            byte[] newState;
            try
            {
                using (var stream = new MemoryStream(delta))
                using (var reader = new BinaryReader(stream))
                {
                    var kind = reader.ReadByte();
                    if (kind == FullStateKind)
                        newState = ReadBytes(reader, reader.ReadInt32(), "full state");
                    else if (kind == DeltaKind)
                        newState = ReadDelta(entityId, reader);
                    else
                        throw new InvalidDataException($"Not a delta packet (kind {kind})");

                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("Delta packet has bytes after its end");
                }
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("Delta packet is cut short");
            }

            lock (_lock)
            {
                _lastState[entityId] = newState;
            }

            var result = new T();
            result.Deserialize(newState);
            return result;
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

        private byte[] ReadDelta(int entityId, BinaryReader reader)
        {
            byte[] baseState;
            lock (_lock)
            {
                if (!_lastState.TryGetValue(entityId, out baseState))
                {
                    throw new InvalidOperationException(
                        $"Cannot apply delta: no base state for entity {entityId}; the sender must send a full state (ClearState)");
                }
            }

            var baseLength = reader.ReadInt32();
            var baseChecksum = reader.ReadUInt32();
            var newLength = reader.ReadInt32();
            var runCount = reader.ReadInt32();
            if (newLength < 0 || runCount < 0)
                throw new InvalidDataException("Delta packet has a negative length");

            if (baseLength != baseState.Length || baseChecksum != Checksum(baseState))
            {
                throw new InvalidOperationException(
                    $"Cannot apply delta: it was built on a state entity {entityId} never received (a lost packet); the sender must send a full state (ClearState)");
            }

            var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (newLength > baseLength + remaining)
                throw new InvalidDataException("Delta packet claims more new bytes than it carries");

            var newState = new byte[newLength];
            Array.Copy(baseState, newState, Math.Min(baseLength, newLength));

            var end = -1;
            for (int i = 0; i < runCount; i++)
            {
                var offset = reader.ReadInt32();
                var length = reader.ReadInt32();
                if (offset <= end || length <= 0 || offset > newLength - length)
                    throw new InvalidDataException($"Delta packet has a change outside the state ({offset}+{length} of {newLength})");

                var bytes = ReadBytes(reader, length, "change");
                Array.Copy(bytes, 0, newState, offset, length);
                end = offset + length - 1;
            }

            return newState;
        }

        private static byte[] ReadBytes(BinaryReader reader, int length, string what)
        {
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException($"Delta packet is cut short ({what})");
            return reader.ReadBytes(length);
        }

        /// <summary>
        /// Creates a full state packet.
        /// Wire format: [Kind=1 (byte)] [Length (int32)] [FullState (byte[])]
        /// </summary>
        private byte[] CreateFullStatePacket(byte[] state)
        {
            using (var stream = new MemoryStream(state.Length + 5))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(FullStateKind);
                writer.Write(state.Length);
                writer.Write(state);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Creates a delta packet.
        /// Wire format: [Kind=0 (byte)] [BaseLength (int32)] [BaseChecksum (uint32)] [NewLength (int32)] [RunCount (int32)]
        /// then per run: [Offset (int32)] [Length (int32)] [Bytes (byte[])]. Bytes past the new length are dropped.
        /// </summary>
        private byte[] CreateStateDelta(byte[] oldState, byte[] newState)
        {
            var runs = new List<(int Offset, int Length)>();
            int i = 0;
            while (i < newState.Length)
            {
                if (i < oldState.Length && oldState[i] == newState[i])
                {
                    i++;
                    continue;
                }

                // A run starts at the first changed byte and ends where enough unchanged bytes follow
                int start = i;
                int last = i;
                for (i++; i < newState.Length; i++)
                {
                    bool changed = i >= oldState.Length || oldState[i] != newState[i];
                    if (changed)
                        last = i;
                    else if (i - last > RunHeaderSize)
                        break;
                }
                runs.Add((start, last - start + 1));
                i = last + 1;
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DeltaKind);
                writer.Write(oldState.Length);
                writer.Write(Checksum(oldState));
                writer.Write(newState.Length);
                writer.Write(runs.Count);

                foreach (var run in runs)
                {
                    writer.Write(run.Offset);
                    writer.Write(run.Length);
                    writer.Write(newState, run.Offset, run.Length);
                }

                return stream.ToArray();
            }
        }

        // FNV-1a: enough to tell two states apart, not a security check
        private static uint Checksum(byte[] data)
        {
            uint hash = 2166136261;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= 16777619;
            }
            return hash;
        }
    }
}
