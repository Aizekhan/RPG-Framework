using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using RPGFramework.Networking.UnityTransport;

namespace RPGFramework.Networking.Tests
{
    /// <summary>
    /// Real UDP on this machine through Unity Transport: a client connects, messages go both ways (a large one is
    /// fragmented), and leaving is heard on the other side.
    /// </summary>
    public sealed class UnityNetworkTransportTests
    {
        private sealed class BlobMessage : NetworkMessage, INetworkPayload
        {
            public const int Id = MessageType.GameSpecificStart + 2;

            public override int MessageTypeId => Id;
            public byte[] Bytes = Array.Empty<byte>();

            public void Write(BinaryWriter writer)
            {
                writer.Write(Bytes.Length);
                writer.Write(Bytes);
            }

            public void Read(BinaryReader reader) => Bytes = reader.ReadBytes(reader.ReadInt32());
        }

        private readonly List<UnityNetworkTransport> _transports = new List<UnityNetworkTransport>();

        [TearDown]
        public void TearDown()
        {
            foreach (var transport in _transports)
                transport.Dispose();
            _transports.Clear();
        }

        private NetworkManager Manager(out UnityNetworkTransport transport)
        {
            transport = new UnityNetworkTransport();
            _transports.Add(transport);
            var serializer = new NetworkSerializer();
            serializer.RegisterMessageType<BlobMessage>(BlobMessage.Id);
            return new NetworkManager(transport, serializer, new NetworkConfig { PingInterval = 0 });
        }

        private static void PumpUntil(string what, Func<bool> done, params NetworkManager[] managers)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!done())
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"Timed out waiting for {what}");
                foreach (var manager in managers)
                    manager.Update(0.01f);
                Thread.Sleep(5);
            }
        }

        [Test]
        public void ClientConnects_MessagesGoBothWays_LeavingIsHeard()
        {
            var server = Manager(out var serverTransport);
            var client = Manager(out _);
            server.StartServer(0);
            int port = serverTransport.ListeningPort;
            Assert.That(port, Is.GreaterThan(0));

            int connected = -1;
            server.PeerConnected += id => connected = id;
            client.StartClient("127.0.0.1", port);
            PumpUntil("connection", () => connected > 0 && client.IsConnected, server, client);

            var atServer = new List<BlobMessage>();
            var atClient = new List<BlobMessage>();
            server.RegisterMessageHandler<BlobMessage>((_, m) => atServer.Add(m));
            client.RegisterMessageHandler<BlobMessage>((_, m) => atClient.Add(m));

            var large = new byte[20000];
            new Random(1).NextBytes(large);
            client.SendToServer(new BlobMessage { Bytes = new byte[] { 1, 2, 3 } });
            server.Send(connected, new BlobMessage { Bytes = large });
            server.Broadcast(new BlobMessage { Bytes = new byte[] { 9 } }, reliable: false);
            PumpUntil("messages", () => atServer.Count == 1 && atClient.Count == 2, server, client);

            Assert.That(atServer[0].Bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(atClient.Exists(m => m.Bytes.Length == large.Length && m.Bytes[19999] == large[19999]), Is.True);

            int left = -1;
            server.PeerDisconnected += id => left = id;
            client.Stop();
            PumpUntil("leaving", () => left == connected, server);
        }

        [Test]
        public void JoinWithAHostName_SaysItNeedsAnIpAddress()
        {
            var transport = new UnityNetworkTransport();
            _transports.Add(transport);

            Assert.That(() => transport.Connect("my-friends-pc", 7777),
                Throws.ArgumentException.With.Message.Contains("not an IP address"));
        }
    }
}
