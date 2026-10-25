using NUnit.Framework;
using RPGFramework.Networking;
using System;
using System.Collections.Generic;

namespace RPGFramework.Networking.Tests
{
    [TestFixture]
    public class NetworkingSystemTests
    {
        private MockNetworkTransport _transport;
        private NetworkSerializer _serializer;
        private NetworkConfig _config;

        [SetUp]
        public void SetUp()
        {
            _transport = new MockNetworkTransport();
            _serializer = new NetworkSerializer();
            _config = NetworkConfig.Default;
        }

        #region Connection Tests

        [Test]
        public void NetworkManager_StartServer_SetsRoleToServer()
        {
            var manager = new NetworkManager(_transport, _serializer, _config);
            manager.StartServer(7777);

            Assert.AreEqual(NetworkRole.Server, manager.Role);
            Assert.IsTrue(manager.IsConnected);
        }

        [Test]
        public void NetworkManager_StartClient_SetsRoleToClient()
        {
            var manager = new NetworkManager(_transport, _serializer, _config);
            manager.StartClient("127.0.0.1", 7777);

            Assert.AreEqual(NetworkRole.Client, manager.Role);
        }

        [Test]
        public void NetworkManager_Stop_DisconnectsAndResetsState()
        {
            var manager = new NetworkManager(_transport, _serializer, _config);
            manager.StartServer(7777);
            manager.Stop();

            Assert.AreEqual(NetworkRole.None, manager.Role);
            Assert.IsFalse(manager.IsConnected);
        }

        [Test]
        public void NetworkConnection_InitializesWithCorrectState()
        {
            var connection = new NetworkConnection(1, "127.0.0.1", _transport, _serializer);

            Assert.AreEqual(1, connection.ConnectionId);
            Assert.AreEqual("127.0.0.1", connection.RemoteAddress);
            Assert.AreEqual(ConnectionState.Connected, connection.State);
        }

        [Test]
        public void NetworkConnection_UpdateRtt_CalculatesAverage()
        {
            var connection = new NetworkConnection(1, "127.0.0.1", _transport, _serializer);

            connection.UpdateRtt(100f);
            connection.UpdateRtt(200f);
            connection.UpdateRtt(300f);

            Assert.AreEqual(200f, connection.AverageRtt, 0.01f);
        }

        #endregion

        #region Message Serialization Tests

        [Test]
        public void NetworkSerializer_SerializePingMessage_CanDeserialize()
        {
            var ping = new PingMessage { ClientTimestamp = 123456 };
            byte[] data = _serializer.Serialize(ping);

            var deserialized = _serializer.Deserialize<PingMessage>(data);

            Assert.AreEqual(MessageType.Ping, deserialized.MessageTypeId);
            Assert.AreEqual(123456, deserialized.ClientTimestamp);
        }

        [Test]
        public void NetworkSerializer_SerializePongMessage_CanDeserialize()
        {
            var pong = new PongMessage
            {
                ClientTimestamp = 123456,
                ServerTimestamp = 789012
            };
            byte[] data = _serializer.Serialize(pong);

            var deserialized = _serializer.Deserialize<PongMessage>(data);

            Assert.AreEqual(MessageType.Pong, deserialized.MessageTypeId);
            Assert.AreEqual(123456, deserialized.ClientTimestamp);
            Assert.AreEqual(789012, deserialized.ServerTimestamp);
        }

        [Test]
        public void NetworkSerializer_SerializeEntitySpawnMessage_CanDeserialize()
        {
            var spawn = new EntitySpawnMessage
            {
                NetworkId = 42,
                OwnerId = 1,
                PrefabId = "Player",
                InitialState = new byte[] { 1, 2, 3, 4 }
            };
            byte[] data = _serializer.Serialize(spawn);

            var deserialized = _serializer.Deserialize<EntitySpawnMessage>(data);

            Assert.AreEqual(42, deserialized.NetworkId);
            Assert.AreEqual(1, deserialized.OwnerId);
            Assert.AreEqual("Player", deserialized.PrefabId);
            Assert.AreEqual(4, deserialized.InitialState.Length);
        }

        [Test]
        public void NetworkSerializer_SerializeEntityDespawnMessage_CanDeserialize()
        {
            var despawn = new EntityDespawnMessage { NetworkId = 42 };
            byte[] data = _serializer.Serialize(despawn);

            var deserialized = _serializer.Deserialize<EntityDespawnMessage>(data);

            Assert.AreEqual(42, deserialized.NetworkId);
        }

        [Test]
        public void NetworkSerializer_SerializeStateUpdateMessage_CanDeserialize()
        {
            var update = new EntityStateUpdateMessage
            {
                NetworkId = 42,
                StateData = new byte[] { 5, 6, 7, 8, 9 }
            };
            byte[] data = _serializer.Serialize(update);

            var deserialized = _serializer.Deserialize<EntityStateUpdateMessage>(data);

            Assert.AreEqual(42, deserialized.NetworkId);
            Assert.AreEqual(5, deserialized.StateData.Length);
        }

        [Test]
        public void NetworkSerializer_SerializeTransformSyncMessage_CanDeserialize()
        {
            var transform = new TransformSyncMessage
            {
                NetworkId = 42,
                Position = new Vector3Network(1, 2, 3),
                Rotation = new QuaternionNetwork(0, 0, 0, 1),
                Velocity = new Vector3Network(4, 5, 6)
            };
            byte[] data = _serializer.Serialize(transform);

            var deserialized = _serializer.Deserialize<TransformSyncMessage>(data);

            Assert.AreEqual(42, deserialized.NetworkId);
            Assert.AreEqual(new Vector3Network(1, 2, 3), deserialized.Position);
            Assert.AreEqual(new QuaternionNetwork(0, 0, 0, 1), deserialized.Rotation);
            Assert.AreEqual(new Vector3Network(4, 5, 6), deserialized.Velocity);
        }

        #endregion

        #region Replication Tests

        [Test]
        public void ReplicationSystem_RegisterEntity_AddsEntity()
        {
            var replication = new ReplicationSystem(_config);
            var entity = new MockNetworkEntity(1, 0);

            replication.RegisterEntity(entity);

            Assert.AreEqual(entity, replication.GetEntity(1));
        }

        [Test]
        public void ReplicationSystem_UnregisterEntity_RemovesEntity()
        {
            var replication = new ReplicationSystem(_config);
            var entity = new MockNetworkEntity(1, 0);

            replication.RegisterEntity(entity);
            replication.UnregisterEntity(1);

            Assert.IsNull(replication.GetEntity(1));
        }

        [Test]
        public void ReplicationSystem_RegisterEntity_ThrowsOnDuplicateId()
        {
            var replication = new ReplicationSystem(_config);
            var entity1 = new MockNetworkEntity(1, 0);
            var entity2 = new MockNetworkEntity(1, 0);

            replication.RegisterEntity(entity1);

            Assert.Throws<InvalidOperationException>(() => replication.RegisterEntity(entity2));
        }

        [Test]
        public void ReplicationSystem_GetAllEntities_ReturnsAllRegistered()
        {
            var replication = new ReplicationSystem(_config);
            var entity1 = new MockNetworkEntity(1, 0);
            var entity2 = new MockNetworkEntity(2, 0);

            replication.RegisterEntity(entity1);
            replication.RegisterEntity(entity2);

            var entities = replication.GetAllEntities();

            Assert.AreEqual(2, entities.Count);
        }

        #endregion

        #region NetworkEntity Tests

        [Test]
        public void NetworkEntity_SerializeState_ReturnsData()
        {
            var entity = new MockNetworkEntity(1, 0);
            entity.Value = 42;

            byte[] data = entity.SerializeState();

            Assert.IsNotNull(data);
            Assert.Greater(data.Length, 0);
        }

        [Test]
        public void NetworkEntity_DeserializeState_RestoresState()
        {
            var entity1 = new MockNetworkEntity(1, 0);
            entity1.Value = 42;

            byte[] data = entity1.SerializeState();

            var entity2 = new MockNetworkEntity(1, 0);
            entity2.DeserializeState(data);

            Assert.AreEqual(42, entity2.Value);
        }

        [Test]
        public void NetworkEntity_SerializeDelta_ReturnsNullWhenNoChanges()
        {
            var entity = new MockNetworkEntity(1, 0);
            entity.Value = 42;

            entity.SerializeDelta(); // First call establishes baseline
            byte[] delta = entity.SerializeDelta(); // Second call with no changes

            Assert.IsNull(delta);
        }

        [Test]
        public void NetworkEntity_SerializeDelta_ReturnsDataWhenChanged()
        {
            var entity = new MockNetworkEntity(1, 0);
            entity.Value = 42;

            entity.SerializeDelta(); // Establish baseline
            entity.Value = 100; // Change value
            byte[] delta = entity.SerializeDelta();

            Assert.IsNotNull(delta);
            Assert.Greater(delta.Length, 0);
        }

        #endregion

        #region NetworkTransform Tests

        [Test]
        public void NetworkTransform_SerializeState_IncludesAllComponents()
        {
            var transform = new NetworkTransform(1, 0);
            transform.Position = new Vector3Network(1, 2, 3);
            transform.Rotation = new QuaternionNetwork(0, 0, 0, 1);
            transform.Velocity = new Vector3Network(4, 5, 6);

            byte[] data = transform.SerializeState();

            Assert.IsNotNull(data);
            Assert.Greater(data.Length, 0);
        }

        [Test]
        public void NetworkTransform_DeserializeState_RestoresAllComponents()
        {
            var transform1 = new NetworkTransform(1, 0);
            transform1.Position = new Vector3Network(1, 2, 3);
            transform1.Rotation = new QuaternionNetwork(0.1f, 0.2f, 0.3f, 0.9f);
            transform1.Velocity = new Vector3Network(4, 5, 6);

            byte[] data = transform1.SerializeState();

            var transform2 = new NetworkTransform(1, 0);
            transform2.DeserializeState(data);

            Assert.AreEqual(transform1.Position, transform2.Position);
            Assert.AreEqual(transform1.Rotation, transform2.Rotation);
            Assert.AreEqual(transform1.Velocity, transform2.Velocity);
        }

        [Test]
        public void NetworkTransform_SerializeDelta_OnlyIncludesChangedComponents()
        {
            var transform = new NetworkTransform(1, 0);
            transform.Position = new Vector3Network(1, 2, 3);
            transform.Rotation = new QuaternionNetwork(0, 0, 0, 1);
            transform.Velocity = new Vector3Network(4, 5, 6);

            transform.SerializeDelta(); // Establish baseline
            transform.Position = new Vector3Network(10, 20, 30); // Only change position
            byte[] delta = transform.SerializeDelta();

            Assert.IsNotNull(delta);
            Assert.Greater(delta.Length, 0);
        }

        [Test]
        public void NetworkTransform_ApplyDelta_UpdatesOnlyChangedComponents()
        {
            var transform1 = new NetworkTransform(1, 0);
            transform1.Position = new Vector3Network(1, 2, 3);
            transform1.Rotation = new QuaternionNetwork(0, 0, 0, 1);
            transform1.Velocity = new Vector3Network(4, 5, 6);

            transform1.SerializeDelta(); // Establish baseline
            transform1.Position = new Vector3Network(10, 20, 30); // Only change position
            byte[] delta = transform1.SerializeDelta();

            var transform2 = new NetworkTransform(1, 0);
            transform2.Position = new Vector3Network(1, 2, 3);
            transform2.Rotation = new QuaternionNetwork(0, 0, 0, 1);
            transform2.Velocity = new Vector3Network(4, 5, 6);

            transform2.ApplyDelta(delta);

            Assert.AreEqual(new Vector3Network(10, 20, 30), transform2.Position);
            Assert.AreEqual(new QuaternionNetwork(0, 0, 0, 1), transform2.Rotation);
            Assert.AreEqual(new Vector3Network(4, 5, 6), transform2.Velocity);
        }

        #endregion

        #region Data Structure Tests

        [Test]
        public void Vector3Network_Equality_WorksCorrectly()
        {
            var v1 = new Vector3Network(1, 2, 3);
            var v2 = new Vector3Network(1, 2, 3);
            var v3 = new Vector3Network(4, 5, 6);

            Assert.IsTrue(v1 == v2);
            Assert.IsFalse(v1 == v3);
            Assert.IsTrue(v1.Equals(v2));
            Assert.IsFalse(v1.Equals(v3));
        }

        [Test]
        public void QuaternionNetwork_Equality_WorksCorrectly()
        {
            var q1 = new QuaternionNetwork(1, 2, 3, 4);
            var q2 = new QuaternionNetwork(1, 2, 3, 4);
            var q3 = new QuaternionNetwork(5, 6, 7, 8);

            Assert.IsTrue(q1 == q2);
            Assert.IsFalse(q1 == q3);
            Assert.IsTrue(q1.Equals(q2));
            Assert.IsFalse(q1.Equals(q3));
        }

        [Test]
        public void NetworkConfig_Default_HasValidValues()
        {
            var config = NetworkConfig.Default;

            Assert.Greater(config.MaxConnections, 0);
            Assert.Greater(config.ConnectionTimeout, 0);
            Assert.Greater(config.ReplicationRate, 0);
        }

        #endregion

        #region Message Handler Tests

        [Test]
        public void NetworkManager_RegisterMessageHandler_InvokesOnMessage()
        {
            var manager = new NetworkManager(_transport, _serializer, _config);
            manager.StartServer(7777);

            bool handlerCalled = false;
            int receivedConnectionId = -1;

            manager.RegisterMessageHandler<EntitySpawnMessage>((connectionId, msg) =>
            {
                handlerCalled = true;
                receivedConnectionId = connectionId;
            });

            // Simulate receiving a spawn message
            var spawn = new EntitySpawnMessage { NetworkId = 1, OwnerId = 0, PrefabId = "Test" };
            byte[] data = _serializer.Serialize(spawn);
            _transport.SimulateDataReceived(1, data);

            manager.Update(0.016f);

            Assert.IsTrue(handlerCalled);
            Assert.AreEqual(1, receivedConnectionId);
        }

        [Test]
        public void NetworkManager_UnregisterMessageHandler_StopsInvokingHandler()
        {
            var manager = new NetworkManager(_transport, _serializer, _config);
            manager.StartServer(7777);

            bool handlerCalled = false;

            manager.RegisterMessageHandler<EntitySpawnMessage>((connectionId, msg) =>
            {
                handlerCalled = true;
            });

            manager.UnregisterMessageHandler<EntitySpawnMessage>();

            // Simulate receiving a spawn message
            var spawn = new EntitySpawnMessage { NetworkId = 1, OwnerId = 0, PrefabId = "Test" };
            byte[] data = _serializer.Serialize(spawn);
            _transport.SimulateDataReceived(1, data);

            manager.Update(0.016f);

            Assert.IsFalse(handlerCalled);
        }

        #endregion

        #region Stats Tests

        [Test]
        public void NetworkManager_GetStats_ReturnsValidStats()
        {
            var manager = new NetworkManager(_transport, _serializer, _config);
            manager.StartServer(7777);

            var stats = manager.GetStats();

            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.ActiveConnections);
        }

        #endregion
    }

    #region Mock Classes

    public class MockNetworkTransport : INetworkTransport
    {
        private Queue<NetworkTransportEvent> _events = new Queue<NetworkTransportEvent>();

        public bool IsActive { get; private set; }

        public void StartServer(int port)
        {
            IsActive = true;
        }

        public void Connect(string address, int port)
        {
            IsActive = true;
            _events.Enqueue(new NetworkTransportEvent
            {
                Type = NetworkTransportEventType.Connected,
                ConnectionId = 1
            });
        }

        public void Disconnect()
        {
            IsActive = false;
        }

        public void Send(int connectionId, byte[] data, bool reliable)
        {
            // Mock send - do nothing
        }

        public void Broadcast(byte[] data, bool reliable)
        {
            // Mock broadcast - do nothing
        }

        public bool Poll(out NetworkTransportEvent transportEvent)
        {
            if (_events.Count > 0)
            {
                transportEvent = _events.Dequeue();
                return true;
            }

            transportEvent = default;
            return false;
        }

        public void SimulateDataReceived(int connectionId, byte[] data)
        {
            _events.Enqueue(new NetworkTransportEvent
            {
                Type = NetworkTransportEventType.Data,
                ConnectionId = connectionId,
                Data = data
            });
        }
    }

    public class MockNetworkEntity : NetworkEntity
    {
        public int Value;

        public MockNetworkEntity(int networkId, int ownerId)
            : base(networkId, ownerId)
        {
        }

        public override byte[] SerializeState()
        {
            return BitConverter.GetBytes(Value);
        }

        public override void DeserializeState(byte[] data)
        {
            Value = BitConverter.ToInt32(data, 0);
        }
    }

    #endregion
}
