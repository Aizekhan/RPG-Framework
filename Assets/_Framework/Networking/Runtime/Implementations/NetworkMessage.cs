using System;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Base implementation of INetworkMessage.
    /// </summary>
    public abstract class NetworkMessage : INetworkMessage
    {
        public abstract int MessageTypeId { get; }
        public long Timestamp { get; set; }

        protected NetworkMessage()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Ping message for latency measurement.
    /// </summary>
    public class PingMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.Ping;
        public long ClientTimestamp;
    }

    /// <summary>
    /// Pong response to ping.
    /// </summary>
    public class PongMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.Pong;
        public long ClientTimestamp;
        public long ServerTimestamp;
    }

    /// <summary>
    /// Entity spawn message.
    /// </summary>
    public class EntitySpawnMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.EntitySpawn;
        public int NetworkId;
        public int OwnerId;
        public string PrefabId;
        public byte[] InitialState;
    }

    /// <summary>
    /// Entity despawn message.
    /// </summary>
    public class EntityDespawnMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.EntityDespawn;
        public int NetworkId;
    }

    /// <summary>
    /// Entity state update message (full state).
    /// </summary>
    public class EntityStateUpdateMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.EntityStateUpdate;
        public int NetworkId;
        public byte[] StateData;
    }

    /// <summary>
    /// Entity delta update message (only changes).
    /// </summary>
    public class EntityDeltaUpdateMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.EntityDeltaUpdate;
        public int NetworkId;
        public byte[] DeltaData;
    }

    /// <summary>
    /// RPC invocation message.
    /// </summary>
    public class RpcInvokeMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.RpcInvoke;
        public int NetworkId;
        public string MethodName;
        public byte[] Parameters;
    }

    /// <summary>
    /// Transform synchronization message.
    /// </summary>
    public class TransformSyncMessage : NetworkMessage
    {
        public override int MessageTypeId => MessageType.TransformSync;
        public int NetworkId;
        public Vector3Network Position;
        public QuaternionNetwork Rotation;
        public Vector3Network Velocity;
    }
}
