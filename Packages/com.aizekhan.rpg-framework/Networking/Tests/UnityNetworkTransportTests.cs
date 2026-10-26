using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using RPGFramework.Networking.UnityTransport;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;

namespace RPGFramework.Networking.Tests
{
    /// <summary>
    /// Real UDP on this machine through Unity Transport: a client connects, messages go both ways (a large one is
    /// fragmented), and leaving is heard on the other side. Driver settings can be extended (how relays plug in) without
    /// changing any of that.
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

        [Test]
        public void ExtraDriverSettings_ApplyToEveryDriver_AndUdpWorksAsBefore()
        {
            int configured = 0;
            UnityNetworkTransport.DriverSettings configure = (ref NetworkSettings settings) =>
            {
                configured++;
                settings.WithReliableStageParameters(windowSize: 32);
            };
            var serverTransport = new UnityNetworkTransport(configure);
            var clientTransport = new UnityNetworkTransport(configure);
            _transports.Add(serverTransport);
            _transports.Add(clientTransport);
            var serializer = new NetworkSerializer();
            var server = new NetworkManager(serverTransport, serializer, new NetworkConfig { PingInterval = 0 });
            var client = new NetworkManager(clientTransport, new NetworkSerializer(), new NetworkConfig { PingInterval = 0 });

            server.StartServer(0);
            clientTransport.Connect(NetworkEndpoint.LoopbackIpv4.WithPort((ushort)serverTransport.ListeningPort));
            client.StartClient();
            PumpUntil("connection through an endpoint", () => client.IsConnected && server.Connections.Count == 1, server, client);

            Assert.That(configured, Is.EqualTo(2));
            Assert.That(serverTransport.RelayStatus, Is.EqualTo(RelayConnectionStatus.NotUsingRelay));
        }

        [Test]
        public void ThroughRelay_TheDriverUsesTheRelay_AndWaitsForItsAcceptance()
        {
            // A documentation-only address (TEST-NET-1): no relay answers there, so the relay never accepts us
            var allocationId = new byte[RelayAllocationId.k_Length];
            for (int i = 0; i < allocationId.Length; i++)
                allocationId[i] = (byte)(i + 1); // an empty id means "no allocation" to Unity Transport
            var relay = new RelayServerData("192.0.2.1", 7777, allocationId,
                new byte[RelayConnectionData.k_Length], new byte[RelayConnectionData.k_Length], new byte[RelayHMACKey.k_Length],
                isSecure: false);
            var transport = UnityNetworkTransport.ThroughRelay(relay);
            _transports.Add(transport);
            Assert.That(transport.RelayStatus, Is.EqualTo(RelayConnectionStatus.NotUsingRelay), "nothing before it starts");

            transport.Connect(relay.Endpoint);
            var until = DateTime.UtcNow.AddSeconds(0.5);
            while (DateTime.UtcNow < until)
            {
                while (transport.Poll(out _))
                {
                }
                Thread.Sleep(5);
            }

            Assert.That(transport.IsActive, Is.True);
            Assert.That(transport.RelayStatus, Is.EqualTo(RelayConnectionStatus.NotEstablished));
        }
    }
}
