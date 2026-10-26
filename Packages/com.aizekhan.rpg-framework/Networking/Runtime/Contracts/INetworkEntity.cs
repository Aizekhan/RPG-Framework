namespace RPGFramework.Networking
{
    /// <summary>
    /// Represents an entity that can be synchronized over the network.
    /// </summary>
    public interface INetworkEntity
    {
        /// <summary>
        /// Unique network identifier for this entity.
        /// </summary>
        int NetworkId { get; }

        /// <summary>
        /// Connection ID of the owner (authoritative peer).
        /// </summary>
        int OwnerId { get; }

        /// <summary>
        /// Replication mode for this entity.
        /// </summary>
        ReplicationMode ReplicationMode { get; }

        /// <summary>
        /// Is this entity owned by the local peer?
        /// </summary>
        bool IsLocalOwner { get; }

        /// <summary>
        /// Serialize the current state of this entity.
        /// </summary>
        byte[] SerializeState();

        /// <summary>
        /// Deserialize and apply state to this entity.
        /// </summary>
        void DeserializeState(byte[] data);

        /// <summary>
        /// Get delta state since last serialization (for delta compression).
        /// </summary>
        byte[] SerializeDelta();

        /// <summary>
        /// Apply a delta state update.
        /// </summary>
        void ApplyDelta(byte[] deltaData);
    }
}
