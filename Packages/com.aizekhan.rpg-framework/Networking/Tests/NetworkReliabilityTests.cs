using System.Collections.Generic;
using NUnit.Framework;

namespace RPGFramework.Networking.Tests
{
    /// <summary>
    /// What happens when the other side stops answering or talks nonsense: quiet but alive peers stay (pings keep them
    /// heard), a crashed client or a vanished server is dropped after the timeout with the reason, others stay, and a
    /// peer that keeps sending unreadable messages is dropped past the limit.
    /// </summary>
    public sealed class NetworkReliabilityTests
    {
        private const int Port = 7777;
        private const float Step = 0.1f;

        private LoopbackNetwork _network;

        [SetUp]
        public void SetUp() => _network = new LoopbackNetwork();

        private NetworkManager Manager(out LoopbackTransport transport, float timeout = 3f, int maxBadMessages = 5)
        {
            transport = _network.CreateTransport();
            return new NetworkManager(transport, new NetworkSerializer(),
                new NetworkConfig { PingInterval = 1f, ConnectionTimeout = timeout, MaxBadMessages = maxBadMessages });
        }

        private static void Run(float seconds, params NetworkManager[] managers)
        {
            for (float time = 0f; time < seconds; time += Step)
            {
                foreach (var manager in managers)
                    manager.Update(Step);
            }
        }

        [Test]
        public void QuietPeers_StayConnected_BecausePingsKeepThemHeard()
        {
            var server = Manager(out _);
            var client = Manager(out _);
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);

            Run(30f, server, client);

            Assert.That(server.Connections, Has.Count.EqualTo(1));
            Assert.That(client.IsConnected, Is.True);
            Assert.That(server.SecondsSinceHeard(1), Is.LessThan(1.5f));
            Assert.That(client.SecondsSinceHeard(client.LocalConnectionId), Is.LessThan(1.5f));
        }

        [Test]
        public void ACrashedClient_IsDroppedAfterTheTimeout_AndTheOthersStay()
        {
            var server = Manager(out _);
            var crashing = Manager(out var crashingTransport);
            var staying = Manager(out _);
            server.StartServer(Port);
            crashing.StartClient("127.0.0.1", Port);
            staying.StartClient("127.0.0.1", Port);
            Run(0.5f, server, crashing, staying);
            var dropped = new List<(int, PeerDropReason)>();
            var left = new List<int>();
            server.PeerDropped += (id, reason) => dropped.Add((id, reason));
            server.PeerDisconnected += left.Add;

            crashingTransport.GoSilent(); // the cable is pulled: nobody is told
            Run(2f, server, staying);

            Assert.That(dropped, Is.Empty, "not yet: it may only be a hiccup");
            Assert.That(server.SecondsSinceHeard(1), Is.GreaterThan(1.5f), "the silence shows before the drop");

            Run(1.5f, server, staying);

            Assert.That(dropped, Is.EqualTo(new[] { (1, PeerDropReason.TimedOut) }));
            Assert.That(left, Is.EqualTo(new[] { 1 }));
            Assert.That(server.Connections, Has.Count.EqualTo(1));

            Run(10f, server, staying);
            Assert.That(staying.IsConnected, Is.True);
            Assert.That(left, Has.Count.EqualTo(1));
        }

        [Test]
        public void AVanishedServer_IsNoticedByTheClient()
        {
            var server = Manager(out var serverTransport);
            var client = Manager(out _);
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);
            Run(0.5f, server, client);
            var dropped = new List<PeerDropReason>();
            int disconnects = 0;
            client.PeerDropped += (_, reason) => dropped.Add(reason);
            client.PeerDisconnected += _ => disconnects++;

            serverTransport.GoSilent();
            Run(4f, client);

            Assert.That(client.IsConnected, Is.False);
            Assert.That(dropped, Is.EqualTo(new[] { PeerDropReason.TimedOut }));
            Assert.That(disconnects, Is.EqualTo(1));
        }

        [Test]
        public void WithoutATimeout_OnlyTheTransportEndsAConnection()
        {
            var server = Manager(out _, timeout: 0f);
            var client = Manager(out var clientTransport, timeout: 0f);
            server.StartServer(Port);
            client.StartClient("127.0.0.1", Port);
            Run(0.5f, server, client);

            clientTransport.GoSilent();
            Run(30f, server);

            Assert.That(server.Connections, Has.Count.EqualTo(1));
            Assert.That(server.SecondsSinceHeard(1), Is.GreaterThan(29f));
        }

        [Test]
        public void UnreadableMessages_PastTheLimit_DropThePeer()
        {
            var server = Manager(out _, maxBadMessages: 5);
            var dropped = new List<(int, PeerDropReason)>();
            server.PeerDropped += (id, reason) => dropped.Add((id, reason));
            server.StartServer(Port);
            // A peer without a manager, sending raw bytes
            var raw = _network.CreateTransport();
            raw.Connect("127.0.0.1", Port);
            Run(0.2f, server);

            for (int i = 0; i < 5; i++)
                raw.Send(0, new byte[] { 0xFF, 0x01, 0x02 }, reliable: true);
            Run(0.2f, server);

            Assert.That(server.Connections, Has.Count.EqualTo(1), "a few bad messages are survivable");
            Assert.That(server.GetStats().DroppedPackets, Is.EqualTo(5));

            raw.Send(0, new byte[] { 0xFF }, reliable: true);
            Run(0.2f, server);

            Assert.That(dropped, Is.EqualTo(new[] { (1, PeerDropReason.BadMessages) }));
            Assert.That(server.Connections, Is.Empty);
            var heard = new List<NetworkTransportEventType>();
            while (raw.Poll(out var e))
                heard.Add(e.Type);
            Assert.That(heard, Has.Member(NetworkTransportEventType.Disconnected), "the peer is told it was dropped");

            // What it sends now is not read
            raw.Send(0, new byte[] { 0xFF }, reliable: true);
            Run(0.2f, server);
            Assert.That(server.GetStats().DroppedPackets, Is.EqualTo(6));
        }
    }
}
