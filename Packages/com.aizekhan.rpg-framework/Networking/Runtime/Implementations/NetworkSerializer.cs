using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Simple binary serializer for network messages.
    /// For production, consider using a robust serialization library.
    /// </summary>
    public class NetworkSerializer : INetworkSerializer
    {
        private readonly Dictionary<int, Type> _messageTypes;

        public NetworkSerializer()
        {
            _messageTypes = new Dictionary<int, Type>();
            RegisterDefaultMessageTypes();
        }

        private void RegisterDefaultMessageTypes()
        {
            RegisterMessageType<PingMessage>(MessageType.Ping);
            RegisterMessageType<PongMessage>(MessageType.Pong);
            RegisterMessageType<EntitySpawnMessage>(MessageType.EntitySpawn);
            RegisterMessageType<EntityDespawnMessage>(MessageType.EntityDespawn);
            RegisterMessageType<EntityStateUpdateMessage>(MessageType.EntityStateUpdate);
            RegisterMessageType<EntityDeltaUpdateMessage>(MessageType.EntityDeltaUpdate);
            RegisterMessageType<RpcInvokeMessage>(MessageType.RpcInvoke);
            RegisterMessageType<TransformSyncMessage>(MessageType.TransformSync);
        }

        /// <summary>
        /// Registers a message type by id; game messages (ids from <see cref="MessageType.GameSpecificStart"/>)
        /// implement <see cref="INetworkPayload"/> to write and read their fields.
        /// </summary>
        public void RegisterMessageType<T>(int messageTypeId) where T : INetworkMessage
        {
            _messageTypes[messageTypeId] = typeof(T);
        }

        public byte[] Serialize<T>(T message) where T : INetworkMessage
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write message type ID
                writer.Write(message.MessageTypeId);
                writer.Write(message.Timestamp);

                // Serialize specific message data
                SerializeMessage(writer, message);

                return ms.ToArray();
            }
        }

        public T Deserialize<T>(byte[] data) where T : INetworkMessage
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                int messageTypeId = reader.ReadInt32();
                long timestamp = reader.ReadInt64();

                return (T)DeserializeMessage(reader, messageTypeId, timestamp);
            }
        }

        public (int messageTypeId, object message) Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                int messageTypeId = reader.ReadInt32();
                long timestamp = reader.ReadInt64();

                object message = DeserializeMessage(reader, messageTypeId, timestamp);
                return (messageTypeId, message);
            }
        }

        private void SerializeMessage(BinaryWriter writer, INetworkMessage message)
        {
            switch (message.MessageTypeId)
            {
                case MessageType.Ping:
                    var ping = (PingMessage)message;
                    writer.Write(ping.ClientTimestamp);
                    break;

                case MessageType.Pong:
                    var pong = (PongMessage)message;
                    writer.Write(pong.ClientTimestamp);
                    writer.Write(pong.ServerTimestamp);
                    break;

                case MessageType.EntitySpawn:
                    var spawn = (EntitySpawnMessage)message;
                    writer.Write(spawn.NetworkId);
                    writer.Write(spawn.OwnerId);
                    writer.Write(spawn.PrefabId ?? "");
                    writer.Write(spawn.InitialState?.Length ?? 0);
                    if (spawn.InitialState != null)
                        writer.Write(spawn.InitialState);
                    break;

                case MessageType.EntityDespawn:
                    var despawn = (EntityDespawnMessage)message;
                    writer.Write(despawn.NetworkId);
                    break;

                case MessageType.EntityStateUpdate:
                    var stateUpdate = (EntityStateUpdateMessage)message;
                    writer.Write(stateUpdate.NetworkId);
                    writer.Write(stateUpdate.StateData.Length);
                    writer.Write(stateUpdate.StateData);
                    break;

                case MessageType.EntityDeltaUpdate:
                    var deltaUpdate = (EntityDeltaUpdateMessage)message;
                    writer.Write(deltaUpdate.NetworkId);
                    writer.Write(deltaUpdate.DeltaData.Length);
                    writer.Write(deltaUpdate.DeltaData);
                    break;

                case MessageType.RpcInvoke:
                    var rpc = (RpcInvokeMessage)message;
                    writer.Write(rpc.NetworkId);
                    writer.Write(rpc.MethodName ?? "");
                    writer.Write(rpc.Parameters?.Length ?? 0);
                    if (rpc.Parameters != null)
                        writer.Write(rpc.Parameters);
                    break;

                case MessageType.TransformSync:
                    var transform = (TransformSyncMessage)message;
                    writer.Write(transform.NetworkId);
                    writer.Write(transform.Position.X);
                    writer.Write(transform.Position.Y);
                    writer.Write(transform.Position.Z);
                    writer.Write(transform.Rotation.X);
                    writer.Write(transform.Rotation.Y);
                    writer.Write(transform.Rotation.Z);
                    writer.Write(transform.Rotation.W);
                    writer.Write(transform.Velocity.X);
                    writer.Write(transform.Velocity.Y);
                    writer.Write(transform.Velocity.Z);
                    break;

                default:
                    if (!(message is INetworkPayload payload))
                        throw new NotSupportedException(
                            $"{message.GetType().Name} (type {message.MessageTypeId}) is not a built-in message and does not implement INetworkPayload");
                    payload.Write(writer);
                    break;
            }
        }

        private object DeserializeMessage(BinaryReader reader, int messageTypeId, long timestamp)
        {
            switch (messageTypeId)
            {
                case MessageType.Ping:
                    return new PingMessage
                    {
                        Timestamp = timestamp,
                        ClientTimestamp = reader.ReadInt64()
                    };

                case MessageType.Pong:
                    return new PongMessage
                    {
                        Timestamp = timestamp,
                        ClientTimestamp = reader.ReadInt64(),
                        ServerTimestamp = reader.ReadInt64()
                    };

                case MessageType.EntitySpawn:
                    var spawn = new EntitySpawnMessage
                    {
                        Timestamp = timestamp,
                        NetworkId = reader.ReadInt32(),
                        OwnerId = reader.ReadInt32(),
                        PrefabId = reader.ReadString()
                    };
                    int stateLength = reader.ReadInt32();
                    if (stateLength > 0)
                        spawn.InitialState = reader.ReadBytes(stateLength);
                    return spawn;

                case MessageType.EntityDespawn:
                    return new EntityDespawnMessage
                    {
                        Timestamp = timestamp,
                        NetworkId = reader.ReadInt32()
                    };

                case MessageType.EntityStateUpdate:
                    var stateUpdate = new EntityStateUpdateMessage
                    {
                        Timestamp = timestamp,
                        NetworkId = reader.ReadInt32()
                    };
                    int dataLength = reader.ReadInt32();
                    stateUpdate.StateData = reader.ReadBytes(dataLength);
                    return stateUpdate;

                case MessageType.EntityDeltaUpdate:
                    var deltaUpdate = new EntityDeltaUpdateMessage
                    {
                        Timestamp = timestamp,
                        NetworkId = reader.ReadInt32()
                    };
                    int deltaLength = reader.ReadInt32();
                    deltaUpdate.DeltaData = reader.ReadBytes(deltaLength);
                    return deltaUpdate;

                case MessageType.RpcInvoke:
                    var rpc = new RpcInvokeMessage
                    {
                        Timestamp = timestamp,
                        NetworkId = reader.ReadInt32(),
                        MethodName = reader.ReadString()
                    };
                    int paramLength = reader.ReadInt32();
                    if (paramLength > 0)
                        rpc.Parameters = reader.ReadBytes(paramLength);
                    return rpc;

                case MessageType.TransformSync:
                    return new TransformSyncMessage
                    {
                        Timestamp = timestamp,
                        NetworkId = reader.ReadInt32(),
                        Position = new Vector3Network(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle()),
                        Rotation = new QuaternionNetwork(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle()),
                        Velocity = new Vector3Network(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle())
                    };

                default:
                    if (!_messageTypes.TryGetValue(messageTypeId, out var type) || !typeof(INetworkPayload).IsAssignableFrom(type))
                        throw new NotSupportedException($"Message type {messageTypeId} is not registered");
                    var custom = Activator.CreateInstance(type);
                    if (custom is NetworkMessage withTime)
                        withTime.Timestamp = timestamp;
                    ((INetworkPayload)custom).Read(reader);
                    return custom;
            }
        }
    }
}
