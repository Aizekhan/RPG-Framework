using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using RPGFramework.Networking.UnityTransport;

namespace RPGFramework.Networking.Tests
{
    /// <summary>
    /// Session providers hand the game a ready transport: host → a code, join with the code → connected. Wrong codes
    /// and taken ports fail with a message a player can read. The local-network provider runs over real UDP.
    /// </summary>
    public sealed class SessionProviderTests
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();
        }

        private static NetworkManager Manager(INetworkTransport transport) =>
            new NetworkManager(transport, new NetworkSerializer(), new NetworkConfig { PingInterval = 0 });

        private static void PumpUntil(string what, Func<bool> done, params NetworkManager[] managers)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!done())
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"Timed out waiting for {what}");
                foreach (var manager in managers)
                    manager.Update(0.01f);
                Thread.Sleep(2);
            }
        }

        [Test]
        public void Loopback_HostGivesACode_JoiningWithItConnects()
        {
            var provider = new LoopbackSessionProvider(new LoopbackNetwork());
            var session = provider.HostAsync(new SessionOptions()).GetAwaiter().GetResult();
            var server = Manager(session.Transport);
            server.StartServer();
            var client = Manager(provider.JoinAsync(session.JoinCode).GetAwaiter().GetResult());
            int joined = -1;
            server.PeerConnected += id => joined = id;
            client.StartClient();

            PumpUntil("the client", () => joined > 0 && client.IsConnected, server, client);

            Assert.That(server.Role, Is.EqualTo(NetworkRole.Server));
            Assert.That(client.Role, Is.EqualTo(NetworkRole.Client));
        }

        [Test]
        public void Loopback_WrongCode_SaysSo()
        {
            var provider = new LoopbackSessionProvider(new LoopbackNetwork());

            Assert.That(() => provider.JoinAsync("abc").GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("abc"));
        }

        [Test]
        public void StartingOnATransportThatIsNotOpen_FailsClearly()
        {
            var manager = Manager(new LoopbackNetwork().CreateTransport());

            Assert.That(() => manager.StartServer(), Throws.InvalidOperationException.With.Message.Contains("session provider"));
            Assert.That(() => manager.StartClient(), Throws.InvalidOperationException.With.Message.Contains("session provider"));
        }

        [TestCase("192.168.1.20", "192.168.1.20", 7777)]
        [TestCase(" 192.168.1.20:7790 ", "192.168.1.20", 7790)]
        [TestCase("::1", "::1", 7777)]
        [TestCase("[::1]:7790", "::1", 7790)]
        public void Lan_CodesAreAddressesWithAnOptionalPort(string code, string address, int port)
        {
            Assert.That(LanSessionProvider.TryParse(code, 7777, out string parsedAddress, out int parsedPort), Is.True);
            Assert.That(parsedAddress, Is.EqualTo(address));
            Assert.That(parsedPort, Is.EqualTo(port));
        }

        [TestCase("")]
        [TestCase("my-friends-pc")]
        [TestCase("192.168.1.20:port")]
        [TestCase("192.168.1.20:70000")]
        public void Lan_OtherCodesAreRefused(string code)
        {
            Assert.That(LanSessionProvider.TryParse(code, 7777, out _, out _), Is.False);
            Assert.That(() => new LanSessionProvider().JoinAsync(code).GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("not an IP address"));
        }

        [Test]
        public void Lan_HostAndJoinWithTheCode_OverUdp()
        {
            var provider = new LanSessionProvider(port: 0);
            var session = provider.HostAsync(new SessionOptions()).GetAwaiter().GetResult();
            _disposables.Add((IDisposable)session.Transport);
            StringAssert.Contains(":", session.JoinCode, "a picked port is part of the code");
            var server = Manager(session.Transport);
            server.StartServer();

            // The code names this machine's LAN address; join through loopback with the same port
            LanSessionProvider.TryParse(session.JoinCode, 0, out _, out int port);
            var transport = provider.JoinAsync($"127.0.0.1:{port}").GetAwaiter().GetResult();
            _disposables.Add((IDisposable)transport);
            var client = Manager(transport);
            client.StartClient();

            PumpUntil("the client over UDP", () => client.IsConnected && server.Connections.Count == 1, server, client);
        }

        [Test]
        public void Lan_OffersEveryAddressFriendsCanReach_HomeNetworkFirst_ThenVpns()
        {
            var addresses = new[] { "26.12.0.4", "127.0.0.1", "100.101.2.3", "169.254.9.9", "192.168.1.20", "8.8.4.4", "192.168.1.20" }
                .Select(System.Net.IPAddress.Parse).Concat(new[] { System.Net.IPAddress.IPv6Loopback });

            Assert.That(LanSessionProvider.OrderForFriends(addresses),
                Is.EqualTo(new[] { "192.168.1.20", "26.12.0.4", "100.101.2.3", "8.8.4.4" }),
                "Radmin and Tailscale kept, loopback, self-assigned, IPv6 and duplicates out");

            // This machine: the session's codes are all its addresses with the port
            var provider = new LanSessionProvider(port: 0);
            var session = provider.HostAsync(new SessionOptions()).GetAwaiter().GetResult();
            _disposables.Add((IDisposable)session.Transport);
            var local = LanSessionProvider.LocalAddresses();
            if (local.Count > 0)
                Assert.That(session.OtherJoinCodes.Count, Is.EqualTo(local.Count - 1));
            Assert.That(session.OtherJoinCodes, Has.None.EqualTo(session.JoinCode));
        }

        [Test]
        public void Lan_PortInUse_SaysSo()
        {
            var first = new LanSessionProvider(port: 0).HostAsync(new SessionOptions()).GetAwaiter().GetResult();
            _disposables.Add((IDisposable)first.Transport);
            LanSessionProvider.TryParse(first.JoinCode, 0, out _, out int port);
            // Unity Transport also logs the failed bind, worded per platform; what counts is the player's message
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            Assert.That(() => new LanSessionProvider(port).HostAsync(new SessionOptions()).GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains(port.ToString()));
        }
    }
}
