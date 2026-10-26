using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace RPGFramework.Networking.Tests
{
    /// <summary>
    /// Network managers talk over an in-memory network: connect and leave events on both sides, a game's own
    /// message with its fields, broadcasts, and a client that finds no server.
    /// </summary>
    public sealed class LoopbackNetworkTests
    {
        private const int Port = 7777;

        private sealed class ChatMessage : NetworkMessage, INetworkPayload
        {
            public const int Id = MessageType.GameSpecificStart + 1;

            public override int MessageTypeId => Id;
            public string Text;
            public List<int> Numbers = new List<int>();

            public void Write(BinaryWriter writer)
            {
                writer.Write(Text ?? "");
                writer.Write(Numbers.Count);
                foreach (int number in Numbers)
                    writer.Write(number);
            }

            public void Read(BinaryReader reader)
            {
                Text = reader.ReadString();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    Numbers.Add(reader.ReadInt32());
            }
        }

        private LoopbackNetwork _network;

        [SetUp]
        public void SetUp() => _network = new LoopbackNetwork();

        private NetworkManager Manager()
        {
            var serializer = new NetworkSerializer();
            serializer.RegisterMessageType<ChatMessage>(ChatMessage.Id);
            return new NetworkManager(_network.CreateTransport(), serializer, new NetworkConfig { PingInterval = 0 });
        }

        private static void Pump(params NetworkManager[] managers)
        {
            for (int round = 0; round < 3; round++)
                foreach (var manager in managers)
                    manager.Update(0.016f);
        }

        [Test]
        public void ClientConnects_BothSidesHearIt()
        {
            var server = Manager();
            var client = Manager();
            var serverSaw = new List<int>();
            var clientSaw = new List<int>();
            server.PeerConnected += serverSaw.Add;
            client.PeerConnected += clientSaw.Add;

            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);
            Pump(server, client);

            Assert.That(serverSaw, Is.EqualTo(new[] { 1 }));
            Assert.That(clientSaw, Has.Count.EqualTo(1));
            Assert.That(client.IsConnected, Is.True);
            Assert.That(server.Connections, Has.Count.EqualTo(1));
        }

        [Test]
        public void GameMessage_ArrivesWithItsFields()
        {
            var server = Manager();
            var client = Manager();
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);
            Pump(server, client);
            var received = new List<(int From, ChatMessage Message)>();
            server.RegisterMessageHandler<ChatMessage>((from, message) => received.Add((from, message)));

            client.SendToServer(new ChatMessage { Text = "hi", Numbers = { 3, 5 } });
            Pump(server, client);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].From, Is.EqualTo(1));
            Assert.That(received[0].Message.Text, Is.EqualTo("hi"));
            Assert.That(received[0].Message.Numbers, Is.EqualTo(new[] { 3, 5 }));
        }

        [Test]
        public void Broadcast_ReachesEveryClient()
        {
            var server = Manager();
            var first = Manager();
            var second = Manager();
            server.StartServer(Port);
            first.StartClient("127.0.0.1", Port);
            second.StartClient("127.0.0.1", Port);
            Pump(server, first, second);
            var texts = new List<string>();
            first.RegisterMessageHandler<ChatMessage>((_, m) => texts.Add("first " + m.Text));
            second.RegisterMessageHandler<ChatMessage>((_, m) => texts.Add("second " + m.Text));

            server.Broadcast(new ChatMessage { Text = "wave" });
            Pump(server, first, second);

            Assert.That(texts, Is.EquivalentTo(new[] { "first wave", "second wave" }));
        }

        [Test]
        public void ClientLeaves_ServerHearsIt_ServerStops_ClientHearsIt()
        {
            var server = Manager();
            var first = Manager();
            var second = Manager();
            server.StartServer(Port);
            first.StartClient("127.0.0.1", Port);
            second.StartClient("127.0.0.1", Port);
            Pump(server, first, second);
            var left = new List<int>();
            server.PeerDisconnected += left.Add;
            bool secondLost = false;
            second.PeerDisconnected += _ => secondLost = true;

            first.Stop();
            Pump(server, first, second);
            Assert.That(left, Is.EqualTo(new[] { 1 }));
            Assert.That(server.Connections, Has.Count.EqualTo(1));

            server.Stop();
            Pump(server, first, second);
            Assert.That(secondLost, Is.True);
            Assert.That(second.IsConnected, Is.False);
        }

        [Test]
        public void NothingListening_ClientHearsItCouldNotConnect()
        {
            var client = Manager();
            bool lost = false;
            client.PeerDisconnected += _ => lost = true;

            client.StartClient("127.0.0.1", Port);
            Pump(client);

            Assert.That(lost, Is.True);
            Assert.That(client.IsConnected, Is.False);
        }

        [Test]
        public void UnregisteredGameMessage_FailsToSendWithAClearError()
        {
            var serializer = new NetworkSerializer();

            Assert.That(() => serializer.Deserialize(serializer.Serialize(new ChatMessage())),
                Throws.TypeOf<System.NotSupportedException>().With.Message.Contains("not registered"));
        }
    }
}
