using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.ActionRpg;
using RPGFramework.ActionRpg.Coop;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Logging;
using Unity.Networking.Transport.Relay;
using Unity.Services.Multiplayer;

namespace RPGFramework.Networking.UnityRelay.Tests
{
    /// <summary>
    /// The Unity Relay provider against a fake room service and an in-memory network: hosting gives a code, joining
    /// with it connects, the co-op plays through it and still refuses a different game version, errors read plainly,
    /// and closing the game leaves the room. This proves the integration logic only. Real Relay traffic between two
    /// networks needs Unity's servers and a linked Unity Gaming Services project, and is checked by hand.
    /// </summary>
    public sealed class UnityRelaySessionProviderTests
    {
        /// <summary>
        /// Rooms "on Unity's servers": each is a port on the shared in-memory network.
        /// </summary>
        private sealed class FakeCloud
        {
            public readonly Dictionary<string, (int Port, int MaxPlayers, int Players)> Rooms =
                new Dictionary<string, (int, int, int)>();
            public int NextPort = 9000;
            public int Leaves;
            public int LastMaxPlayers;
            public string LastName;
        }

        private sealed class FakeRelayService : IRelayService
        {
            private readonly FakeCloud _cloud;
            private string _room;
            private bool _host;

            public FakeRelayService(FakeCloud cloud)
            {
                _cloud = cloud;
            }

            public string SetupProblem { get; set; }
            public int Calls { get; private set; }

            public async UniTask<string> CreateAsync(int maxPlayers, string name, Func<RelayServerData, UniTask> startNetwork,
                CancellationToken cancellation)
            {
                Calls++;
                _cloud.LastMaxPlayers = maxPlayers;
                _cloud.LastName = name;
                int port = _cloud.NextPort++;
                _room = $"ROOM{port}";
                _host = true;
                _cloud.Rooms[_room] = (port, maxPlayers, 1);
                await startNetwork(Relay("127.0.0.1", port));
                return _room;
            }

            public async UniTask JoinAsync(string code, Func<RelayServerData, UniTask> startNetwork, CancellationToken cancellation)
            {
                Calls++;
                if (!_cloud.Rooms.TryGetValue(code, out var room))
                    throw new NetworkSessionException(RelayErrors.Explain(SessionError.SessionNotFound, code));
                if (room.Players >= room.MaxPlayers)
                    throw new NetworkSessionException(RelayErrors.Explain(SessionError.Forbidden, code));
                _cloud.Rooms[code] = (room.Port, room.MaxPlayers, room.Players + 1);
                _room = code;
                await startNetwork(Relay("127.0.0.1", room.Port));
            }

            public UniTask LeaveAsync()
            {
                _cloud.Leaves++;
                if (_host && _room != null)
                    _cloud.Rooms.Remove(_room);
                _room = null;
                return UniTask.CompletedTask;
            }
        }

        private FakeCloud _cloud;
        private LoopbackNetwork _network;
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        [SetUp]
        public void SetUp()
        {
            _cloud = new FakeCloud();
            _network = new LoopbackNetwork();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();
        }

        private static RelayServerData Relay(string host, int port)
        {
            var allocation = new byte[RelayAllocationId.k_Length];
            for (int i = 0; i < allocation.Length; i++)
                allocation[i] = (byte)(i + 1);
            return new RelayServerData(host, (ushort)port, allocation, new byte[RelayConnectionData.k_Length],
                new byte[RelayConnectionData.k_Length], new byte[RelayHMACKey.k_Length], isSecure: true);
        }

        /// <summary>
        /// A provider on its own "machine": its own room service, transports on the shared in-memory network.
        /// </summary>
        private UnityRelaySessionProvider Machine(out FakeRelayService service)
        {
            service = new FakeRelayService(_cloud);
            return new UnityRelaySessionProvider(service, (relay, host) =>
            {
                var transport = _network.CreateTransport();
                if (host)
                    transport.StartServer(relay.Endpoint.Port);
                else
                    transport.Connect("loopback", relay.Endpoint.Port);
                return transport;
            });
        }

        private static NetworkManager Manager(INetworkTransport transport) =>
            new NetworkManager(transport, new NetworkSerializer(), new NetworkConfig { PingInterval = 0 });

        private static void Pump(Func<bool> done, string what, params Action[] updates)
        {
            for (int round = 0; round < 50 && !done(); round++)
            {
                foreach (var update in updates)
                    update();
            }
            Assert.That(done(), Is.True, $"Waited in vain for {what}");
        }

        [Test]
        public void Host_GetsACode_AFriendTypesIt_AndTheyConnect()
        {
            var host = Machine(out _).HostAsync(new SessionOptions { MaxPlayers = 6, Name = "Maks" }).GetAwaiter().GetResult();
            Assert.That(host.JoinCode, Is.Not.Empty);
            Assert.That(_cloud.LastMaxPlayers, Is.EqualTo(6));
            Assert.That(_cloud.LastName, Is.EqualTo("Maks"));

            // Typed by hand: lower case, a dash and spaces are fine
            string typed = " " + host.JoinCode.ToLowerInvariant().Insert(4, "-") + " ";
            var friend = Machine(out _).JoinAsync(typed).GetAwaiter().GetResult();

            var server = Manager(host.Transport);
            var client = Manager(friend);
            server.StartServer();
            client.StartClient();
            Pump(() => client.IsConnected && server.Connections.Count == 1, "the friend's connection",
                () => server.Update(0.02f), () => client.Update(0.02f));
        }

        [Test]
        public void ThroughTheRelay_TheCoopPlays_AndADifferentGameVersionIsStillRefused()
        {
            var settings = Settings(goblinAttack: 6f);
            var content = new ActionRpgContent();
            using var logger = new Logger(LogSeverity.Warning, new MemoryLogSink(64));
            using var game = new GameHost(logger);
            ActionRpgModule module = null;
            game.Start(builder => module = ActionRpgComposition.Configure(builder, settings, content));

            var session = Machine(out _).HostAsync(new SessionOptions()).GetAwaiter().GetResult();
            var coopHost = new CoopHost(module.World, game.Container.Resolve<IEventBus>(),
                CoopProtocol.CreateNetwork(session.Transport), null, CoopProtocol.Fingerprint(settings, content));
            _disposables.Add(coopHost);
            coopHost.Open();

            var same = Join(session.JoinCode, Settings(goblinAttack: 6f), content);
            var other = Join(session.JoinCode, Settings(goblinAttack: 9f), content);
            Pump(() => same.State == CoopClientState.Playing && other.State == CoopClientState.Ended, "both answers",
                () => coopHost.Update(0.05f), () => same.Update(0.05f), () => other.Update(0.05f));

            Assert.That(module.World.Players, Has.Count.EqualTo(2));
            Assert.That(other.EndReason, Does.Contain("different version"));
        }

        private CoopClient Join(string code, ActionRpgSettings settings, ActionRpgContent content)
        {
            var transport = Machine(out _).JoinAsync(code).GetAwaiter().GetResult();
            var client = new CoopClient(settings, content, new EventBus(), CoopProtocol.CreateNetwork(transport), null);
            _disposables.Add(client);
            client.JoinSession(code);
            return client;
        }

        private static ActionRpgSettings Settings(float goblinAttack)
        {
            var settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 20f },
                Waves = new WaveSettings { Enabled = false }
            };
            settings.Enemies.Add(new CharacterData { Id = "goblin", DisplayName = "Goblin", MaxHealth = 30f, Attack = goblinAttack });
            return settings;
        }

        [Test]
        public void WrongCode_FullRoom_NoCode_EachSaysWhy()
        {
            var session = Machine(out _).HostAsync(new SessionOptions { MaxPlayers = 2 }).GetAwaiter().GetResult();
            Machine(out _).JoinAsync(session.JoinCode).GetAwaiter().GetResult();

            Assert.That(() => Machine(out _).JoinAsync("ZZZ999").GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("No game with the code ZZZ999"));
            Assert.That(() => Machine(out _).JoinAsync(session.JoinCode).GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("full"));
            Assert.That(() => Machine(out _).JoinAsync(" - ").GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("Type the code"));
        }

        [Test]
        public void ProjectNotSetUp_SaysWhatToDo_BeforeAskingTheService()
        {
            var provider = Machine(out var service);
            service.SetupProblem = RelayErrors.NotSetUp;

            Assert.That(() => provider.HostAsync(new SessionOptions()).GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("Project Settings > Services"));
            Assert.That(() => provider.JoinAsync("ABC123").GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("Project Settings > Services"));
            Assert.That(service.Calls, Is.Zero);
        }

        [Test]
        public void ClosingTheGame_LeavesTheRoomOnce_AndTheCodeStopsWorking()
        {
            var session = Machine(out _).HostAsync(new SessionOptions()).GetAwaiter().GetResult();
            var friend = Machine(out _).JoinAsync(session.JoinCode).GetAwaiter().GetResult();

            ((IDisposable)friend).Dispose();
            Assert.That(_cloud.Leaves, Is.EqualTo(1));
            session.Transport.Disconnect();
            ((IDisposable)session.Transport).Dispose();
            Assert.That(_cloud.Leaves, Is.EqualTo(2), "disconnect then dispose leaves once");

            Assert.That(() => Machine(out _).JoinAsync(session.JoinCode).GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("No game"));
        }

        [Test]
        public void ATransportThatCannotOpen_FailsClearly_AndLeavesNoRoomBehind()
        {
            var service = new FakeRelayService(_cloud);
            var provider = new UnityRelaySessionProvider(service, (relay, host) => throw new InvalidOperationException("port busy"));

            Assert.That(() => provider.HostAsync(new SessionOptions()).GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("could not be opened"));
            Assert.That(_cloud.Rooms, Is.Empty);
            Assert.That(_cloud.Leaves, Is.EqualTo(1));
        }

        [Test]
        public void WithUnityTransport_TheHostWaitsForTheRelay_AndGivesUpClearlyWhenNoneAnswers()
        {
            // Real Unity Transport through relay settings, but the room service hands out a relay at a
            // documentation-only address (TEST-NET-1), so no relay ever accepts the host
            var service = new FakeRelayServiceAt(_cloud, "192.0.2.1");
            var provider = new UnityRelaySessionProvider(service) { RelayTimeoutSeconds = 0f };

            Assert.That(() => provider.HostAsync(new SessionOptions()).GetAwaiter().GetResult(),
                Throws.TypeOf<NetworkSessionException>().With.Message.Contains("did not answer"));
            Assert.That(_cloud.Leaves, Is.EqualTo(1));
        }

        private sealed class FakeRelayServiceAt : IRelayService
        {
            private readonly FakeCloud _cloud;
            private readonly string _address;

            public FakeRelayServiceAt(FakeCloud cloud, string address)
            {
                _cloud = cloud;
                _address = address;
            }

            public string SetupProblem => null;

            public async UniTask<string> CreateAsync(int maxPlayers, string name, Func<RelayServerData, UniTask> startNetwork,
                CancellationToken cancellation)
            {
                await startNetwork(Relay(_address, 7777));
                return "ROOM";
            }

            public UniTask JoinAsync(string code, Func<RelayServerData, UniTask> startNetwork, CancellationToken cancellation) =>
                startNetwork(Relay(_address, 7777));

            public UniTask LeaveAsync()
            {
                _cloud.Leaves++;
                return UniTask.CompletedTask;
            }
        }

        [TestCase(SessionError.SessionNotFound, "No game with the code AB12")]
        [TestCase(SessionError.Forbidden, "full or closed")]
        [TestCase(SessionError.SessionDeleted, "has ended")]
        [TestCase(SessionError.NotAuthorized, "Project Settings > Services")]
        [TestCase(SessionError.RateLimitExceeded, "Wait a few seconds")]
        [TestCase(SessionError.MatchmakerCancelled, "refused (MatchmakerCancelled)")]
        public void ServiceErrors_ReadPlainly(SessionError error, string expected)
        {
            Assert.That(RelayErrors.Explain(error, "AB12"), Does.Contain(expected));
        }

        [Test]
        public void TheModule_OffersItselfToGames()
        {
            UnityRelaySessionProvider.Register();

            var entry = NetworkSessionProviders.Find(UnityRelaySessionProvider.Id);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.DisplayName, Is.EqualTo("Internet (Unity Relay)"));
            Assert.That(entry.Create(), Is.TypeOf<UnityRelaySessionProvider>());
        }
    }
}
