using System;
using System.IO;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Base implementation of INetworkEntity.
    /// Game-specific entities should inherit from this or implement INetworkEntity.
    /// </summary>
    public abstract class NetworkEntity : INetworkEntity
    {
        private byte[] _lastState;

        public int NetworkId { get; set; }
        public int OwnerId { get; set; }
        public ReplicationMode ReplicationMode { get; set; }
        public bool IsLocalOwner { get; set; }

        protected NetworkEntity(int networkId, int ownerId, ReplicationMode replicationMode = ReplicationMode.FullState)
        {
            NetworkId = networkId;
            OwnerId = ownerId;
            ReplicationMode = replicationMode;
        }

        public abstract byte[] SerializeState();
        public abstract void DeserializeState(byte[] data);

        public virtual byte[] SerializeDelta()
        {
            // Simple delta: compare current state with last state
            byte[] currentState = SerializeState();

            if (_lastState == null || _lastState.Length != currentState.Length)
            {
                _lastState = currentState;
                return currentState; // Full state on first sync or size change
            }

            // Find differences
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                bool hasDifferences = false;

                for (int i = 0; i < currentState.Length; i++)
                {
                    if (currentState[i] != _lastState[i])
                    {
                        hasDifferences = true;
                        writer.Write(i); // Write index
                        writer.Write(currentState[i]); // Write new value
                    }
                }

                _lastState = currentState;

                if (!hasDifferences)
                    return null; // No changes

                return ms.ToArray();
            }
        }

        public virtual void ApplyDelta(byte[] deltaData)
        {
            if (deltaData == null || deltaData.Length == 0)
                return;

            if (_lastState == null)
            {
                // If we don't have a baseline, treat delta as full state
                DeserializeState(deltaData);
                return;
            }

            using (var ms = new MemoryStream(deltaData))
            using (var reader = new BinaryReader(ms))
            {
                byte[] currentState = new byte[_lastState.Length];
                Array.Copy(_lastState, currentState, _lastState.Length);

                while (ms.Position < ms.Length)
                {
                    int index = reader.ReadInt32();
                    byte value = reader.ReadByte();
                    currentState[index] = value;
                }

                DeserializeState(currentState);
                _lastState = currentState;
            }
        }
    }
}
