using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RPGFramework.Networking.Tests
{
    public sealed class NetworkAuthorityTests
    {
        private MockNetworkTransport _transport;
        private NetworkSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _transport = new MockNetworkTransport();
            _serializer = new NetworkSerializer();
        }

        private NetworkManager Server()
        {
            var manager = new NetworkManager(_transport, _serializer, NetworkConfig.Default);
            manager.StartServer(7777);
            return manager;
        }

        private NetworkManager ConnectedClient()
        {
            var manager = new NetworkManager(_transport, _serializer, NetworkConfig.Default);
            manager.StartClient("127.0.0.1", 7777);
            manager.Update(0f);
            Assert.That(manager.IsConnected, Is.True);
            return manager;
        }

        private void Receive(NetworkManager manager, INetworkMessage message)
        {
            _transport.SimulateDataReceived(1, _serializer.Serialize(message));
            manager.Update(0f);
        }

        [Test]
        public void SeveralHandlers_AllRun_AndAFailingOneIsIsolated()
        {
            var server = Server();
            var calls = new List<string>();
            server.RegisterMessageHandler<EntitySpawnMessage>((id, m) => calls.Add("a:" + m.PrefabId));
            server.RegisterMessageHandler<EntitySpawnMessage>((id, m) => throw new InvalidOperationException("boom"));
            server.RegisterMessageHandler<EntitySpawnMessage>((id, m) => calls.Add("c:" + m.PrefabId));

            Receive(server, new EntitySpawnMessage { NetworkId = 1, PrefabId = "Orc" });

            CollectionAssert.AreEqual(new[] { "a:Orc", "c:Orc" }, calls);
            Assert.That(server.GetStats().HandlerErrors, Is.EqualTo(1));
        }

        [Test]
        public void HandlersCanBeRemovedIndividually()
        {
            var server = Server();
            var calls = new List<string>();
            Action<int, EntitySpawnMessage> first = (id, m) => calls.Add("first");
            server.RegisterMessageHandler(first);
            var second = server.RegisterMessageHandler<EntitySpawnMessage>((id, m) => calls.Add("second"));
            server.RegisterMessageHandler<EntitySpawnMessage>((id, m) => calls.Add("third"));

            second.Dispose();
            second.Dispose();
            Assert.That(server.UnregisterMessageHandler(first), Is.True);
            Assert.That(server.UnregisterMessageHandler(first), Is.False);
            Receive(server, new EntitySpawnMessage { PrefabId = "x" });

            CollectionAssert.AreEqual(new[] { "third" }, calls);
        }

        [Test]
        public void Server_RejectsServerAuthoritativeMessagesFromClients()
        {
            var server = Server();
            server.Authority = NetworkAuthority.CreateDefault();
            bool handled = false;
            server.RegisterMessageHandler<EntitySpawnMessage>((id, m) => handled = true);

            Receive(server, new EntitySpawnMessage { NetworkId = 99, PrefabId = "Cheat" });

            Assert.That(handled, Is.False);
            Assert.That(server.GetStats().RejectedMessages, Is.EqualTo(1));
        }

        [Test]
        public void Client_AcceptsServerMessages_RejectsClientAuthoritativeOnes()
        {
            var client = ConnectedClient();
            client.Authority = NetworkAuthority.CreateDefault().Set<EntityDespawnMessage>(MessageAuthority.Client);
            var calls = new List<string>();
            client.RegisterMessageHandler<EntitySpawnMessage>((id, m) => calls.Add("spawn"));
            client.RegisterMessageHandler<EntityDespawnMessage>((id, m) => calls.Add("despawn"));

            Receive(client, new EntitySpawnMessage { NetworkId = 1, PrefabId = "Orc" });
            Receive(client, new EntityDespawnMessage { NetworkId = 1 });

            CollectionAssert.AreEqual(new[] { "spawn" }, calls);
            Assert.That(client.GetStats().RejectedMessages, Is.EqualTo(1));
        }

        [Test]
        public void SendingFromTheWrongSide_Throws()
        {
            var client = ConnectedClient();
            client.Authority = NetworkAuthority.CreateDefault();

            Assert.Throws<InvalidOperationException>(() => client.SendToServer(new EntitySpawnMessage()));
            Assert.DoesNotThrow(() => client.SendToServer(new PingMessage()));

            var server = new NetworkManager(new MockNetworkTransport(), _serializer, NetworkConfig.Default)
            {
                Authority = NetworkAuthority.CreateDefault().Set<PingMessage>(MessageAuthority.Client)
            };
            server.StartServer(7778);
            Assert.DoesNotThrow(() => server.Broadcast(new EntitySpawnMessage()));
            Assert.Throws<InvalidOperationException>(() => server.Broadcast(new PingMessage()));
        }

        [Test]
        public void Rules_ResolvePerRole()
        {
            var authority = new NetworkAuthority()
                .Set(MessageType.GameSpecificStart, MessageAuthority.Server)
                .Set(MessageType.GameSpecificStart + 1, MessageAuthority.Client);
            int serverMsg = MessageType.GameSpecificStart;
            int clientMsg = MessageType.GameSpecificStart + 1;

            Assert.That(authority.CanSend(serverMsg, NetworkRole.Host), Is.True);
            Assert.That(authority.CanSend(clientMsg, NetworkRole.Host), Is.True);
            Assert.That(authority.CanSend(serverMsg, NetworkRole.Client), Is.False);
            Assert.That(authority.CanReceive(clientMsg, NetworkRole.Host), Is.True);
            Assert.That(authority.CanReceive(serverMsg, NetworkRole.Host), Is.False);
            Assert.That(authority.CanReceive(clientMsg, NetworkRole.Client), Is.False);
            Assert.That(authority.Get(MessageType.Ping), Is.EqualTo(MessageAuthority.Any));

            authority.Set(serverMsg, MessageAuthority.Any);
            Assert.That(authority.CanSend(serverMsg, NetworkRole.Client), Is.True);
        }
    }
}
