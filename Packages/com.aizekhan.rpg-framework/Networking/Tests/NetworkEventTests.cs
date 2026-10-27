using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Networking.Tests
{
    /// <summary>
    /// Given an event publisher, the network manager announces connections, losses (a silent peer as a timeout) and
    /// accepted messages on the event bus, and the replication system announces spawns, despawns and replicated
    /// states, so game code and UI can react without holding the network objects.
    /// </summary>
    public sealed class NetworkEventTests
    {
        private const int Port = 7777;

        private sealed class ChatMessage : NetworkMessage, INetworkPayload
        {
            public const int Id = MessageType.GameSpecificStart + 1;

            public override int MessageTypeId => Id;
            public string Text;

            public void Write(BinaryWriter writer) => writer.Write(Text ?? "");

            public void Read(BinaryReader reader) => Text = reader.ReadString();
        }

        private LoopbackNetwork _network;
        private EventBus _serverBus;
        private EventBus _clientBus;

        [SetUp]
        public void SetUp()
        {
            _network = new LoopbackNetwork();
            _serverBus = new EventBus();
            _clientBus = new EventBus();
        }

        private NetworkManager Manager(EventBus bus, out LoopbackTransport transport, float timeout = 3f)
        {
            var serializer = new NetworkSerializer();
            serializer.RegisterMessageType<ChatMessage>(ChatMessage.Id);
            transport = _network.CreateTransport();
            return new NetworkManager(transport, serializer, new NetworkConfig { PingInterval = 1f, ConnectionTimeout = timeout },
                events: bus);
        }

        private static void Run(float seconds, params NetworkManager[] managers)
        {
            for (float time = 0f; time < seconds; time += 0.1f)
            {
                foreach (var manager in managers)
                    manager.Update(0.1f);
            }
        }

        private static List<T> Collect<T>(EventBus bus) where T : IEvent
        {
            var list = new List<T>();
            bus.Subscribe<T>(list.Add);
            return list;
        }

        [Test]
        public void Connecting_IsPublishedOnBothSides()
        {
            var serverEvents = Collect<ConnectionEstablishedEvent>(_serverBus);
            var clientEvents = Collect<ConnectionEstablishedEvent>(_clientBus);
            var server = Manager(_serverBus, out _);
            var client = Manager(_clientBus, out _);
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);

            Run(0.5f, server, client);

            Assert.That(serverEvents, Has.Count.EqualTo(1));
            Assert.That(serverEvents[0].IsServer, Is.True);
            Assert.That(serverEvents[0].ConnectionId, Is.EqualTo(server.Connections[0].ConnectionId));
            Assert.That(clientEvents, Has.Count.EqualTo(1));
            Assert.That(clientEvents[0].IsServer, Is.False);
            Assert.That(clientEvents[0].ConnectionId, Is.EqualTo(client.LocalConnectionId));
        }

        [Test]
        public void AClientLeaving_IsPublishedAsLost_NotAsATimeout()
        {
            var lost = Collect<ConnectionLostEvent>(_serverBus);
            var server = Manager(_serverBus, out _);
            var client = Manager(_clientBus, out _);
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);
            Run(0.5f, server, client);
            int id = server.Connections[0].ConnectionId;

            client.Stop();
            Run(0.5f, server);

            Assert.That(lost, Has.Count.EqualTo(1));
            Assert.That(lost[0].ConnectionId, Is.EqualTo(id));
            Assert.That(lost[0].WasTimeout, Is.False);
        }

        [Test]
        public void ASilentClient_IsPublishedAsLostByTimeout()
        {
            var lost = Collect<ConnectionLostEvent>(_serverBus);
            var server = Manager(_serverBus, out _);
            var client = Manager(_clientBus, out var clientTransport);
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);
            Run(0.5f, server, client);

            clientTransport.GoSilent();
            Run(4f, server);

            Assert.That(lost, Has.Count.EqualTo(1));
            Assert.That(lost[0].WasTimeout, Is.True);
            Assert.That(lost[0].Reason, Is.EqualTo(PeerDropReason.TimedOut.ToString()));
        }

        [Test]
        public void AnAcceptedMessage_IsPublishedAfterItsHandlers()
        {
            var order = new List<string>();
            _serverBus.Subscribe<MessageReceivedEvent>(e => order.Add("event " + ((ChatMessage)e.Message).Text + " " + e.MessageTypeId));
            var server = Manager(_serverBus, out _);
            var client = Manager(_clientBus, out _);
            server.RegisterMessageHandler<ChatMessage>((_, m) => order.Add("handler " + m.Text));
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);
            Run(0.5f, server, client);
            order.Clear(); // pings are internal and never published

            client.SendToServer(new ChatMessage { Text = "hi" }, reliable: true);
            Run(0.2f, server, client);

            Assert.That(order, Is.EqualTo(new[] { "handler hi", "event hi " + ChatMessage.Id }));
        }

        [Test]
        public void Replication_PublishesSpawnsReplicatedStatesAndDespawns()
        {
            var bus = new EventBus();
            var spawned = Collect<EntitySpawnedEvent>(bus);
            var replicated = Collect<StateReplicatedEvent>(bus);
            var despawned = Collect<EntityDespawnedEvent>(bus);
            var replication = new ReplicationSystem(new NetworkConfig(), bus);

            replication.SpawnEntity(5, 2, "goblin", Array.Empty<byte>());
            var entity = new MockNetworkEntity(5, 2);
            replication.RegisterEntity(entity);
            replication.ApplyReplication(5, BitConverter.GetBytes(42), isDelta: false);
            replication.ApplyReplication(99, BitConverter.GetBytes(1), isDelta: false); // not registered: nothing
            replication.DespawnEntity(5);

            Assert.That(spawned, Has.Count.EqualTo(1));
            Assert.That((spawned[0].NetworkId, spawned[0].OwnerId, spawned[0].PrefabId), Is.EqualTo((5, 2, "goblin")));
            Assert.That(entity.Value, Is.EqualTo(42));
            Assert.That(replicated, Has.Count.EqualTo(1));
            Assert.That((replicated[0].NetworkId, replicated[0].IsDelta, replicated[0].DataSize), Is.EqualTo((5, false, 4)));
            Assert.That(despawned, Has.Count.EqualTo(1));
            Assert.That(despawned[0].NetworkId, Is.EqualTo(5));
            Assert.That(replication.GetEntity(5), Is.Null);
        }
    }
}
