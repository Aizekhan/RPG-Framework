using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RPGFramework.ActionRpg.PlayTests
{
    /// <summary>
    /// Two games on this machine over real UDP: one hosts, the other joins and gets its player in the host's party;
    /// movement goes across, the host's goblin appears on the joined screen without its AI, the joined player's
    /// attacks kill it through the host's rules, and when the host stops, the friend is back in a game of their own.
    /// The host corrects a friend who moves faster than their character can. In PvP the two meet at their sides'
    /// spawn points and fight it out. Any logged error fails the test.
    /// </summary>
    public sealed class CoopPlayTests
    {
        private const float Timeout = 15f;
        private const int Port = 17791;

        private GameObject _camera;
        private GameSetup _setup;
        private ActionRpgHost _first;
        private ActionRpgHost _second;
        private readonly System.Collections.Generic.List<ActionRpgHost> _more = new System.Collections.Generic.List<ActionRpgHost>();

        [SetUp]
        public void SetUp()
        {
            if (Camera.main == null)
                _camera = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };

            var hero = CharacterDefinition.Create("hero", "Hero", 200f, 40f, 5f);
            hero.SetMovement(6f, 2.4f, 0.4f);
            var goblin = CharacterDefinition.Create("goblin", "Goblin", 30f, 4f, 1f, 40);
            _setup = GameSetup.Create(hero, new[] { goblin }, new WaveSettings { Enabled = false });
            _setup.SetSeed(5);

            _first = StartGame("Host");
            _second = StartGame("Friend");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var game in _more.Where(game => game != null))
                Object.Destroy(game.gameObject);
            _more.Clear();
            if (_second != null)
                Object.Destroy(_second.gameObject);
            if (_first != null)
                Object.Destroy(_first.gameObject);
            if (_camera != null)
                Object.Destroy(_camera);
            yield return null;
            foreach (var character in _setup.Characters.ToList())
                Object.Destroy(character);
            Object.Destroy(_setup);
        }

        private ActionRpgHost StartGame(string name, GameSetup setup = null, int port = Port)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            var host = gameObject.AddComponent<ActionRpgHost>();
            host.UseSetup(setup ?? _setup);
            host.UseCoopPort(port);
            gameObject.SetActive(true);
            return host;
        }

        [UnityTest]
        public IEnumerator FriendJoins_PlaysInTheHostsGame_AndIsBackAloneWhenTheHostStops()
        {
            _first.UsePlayerName("Maks");
            _second.UsePlayerName("Oleh");
            Assert.That(_first.HostGame(), Is.True, _first.CoopStatus);
            Assert.That(_first.Role, Is.EqualTo(CoopRole.Hosting));
            Assert.That(_first.JoinCode, Does.EndWith(":" + Port), "the code friends type: this machine's address and port");

            _second.JoinGame("127.0.0.1");
            yield return WaitUntil(() => _second.CoopClient?.State == Coop.CoopClientState.Playing, "the friend to join");
            Assert.That(_second.Role, Is.EqualTo(CoopRole.Joined));
            Assert.That(_second.World, Is.Null, "the friend's machine does not run the rules");
            Assert.That(_first.World.Players, Has.Count.EqualTo(2));

            var host = _first.World;
            var friend = _second.CurrentWorld;
            int friendId = friend.PlayerId;
            Assert.That(friendId, Is.EqualTo(host.Players[1]));

            // Everyone on both screens; each drives only their own player
            EntityView friendOnHost = null, friendOnFriend = null;
            yield return WaitUntil(() => _first.Views.TryGetView(friendId, out friendOnHost)
                                         && _second.Views.TryGetView(friendId, out friendOnFriend)
                                         && _second.Views.TryGetView(host.PlayerId, out _), "the players on both screens");
            Assert.That(friendOnHost.GetComponent<RemoteMotion>().enabled, Is.True);
            Assert.That(friendOnHost.GetComponent<PlayerController>() == null || !friendOnHost.GetComponent<PlayerController>().enabled, Is.True);
            Assert.That(friendOnFriend.GetComponent<PlayerController>().enabled, Is.True);

            // Names above heads and party colours, the same on both screens
            EntityView hostOnFriend = null;
            yield return WaitUntil(() => _second.Views.TryGetView(host.PlayerId, out hostOnFriend)
                                         && friendOnHost.GetComponent<PlayerTagView>().ShownName == "Oleh"
                                         && hostOnFriend.GetComponent<PlayerTagView>().ShownName == "Maks", "name tags");
            Assert.That(friendOnHost.GetComponent<PlayerTagView>().ShownColor, Is.EqualTo(_first.ColorOfSeat(1)));
            Assert.That(hostOnFriend.GetComponent<PlayerTagView>().ShownColor, Is.EqualTo(_second.ColorOfSeat(0)));
            Assert.That(friendOnFriend.GetComponent<PlayerTagView>().ShownColor, Is.EqualTo(_second.ColorOfSeat(1)));
            Assert.That(_first.ColorOfSeat(0), Is.Not.EqualTo(_first.ColorOfSeat(1)));

            // The friend walks; the host sees it
            yield return Walk(friendOnFriend.transform, new Vector3(4f, 0f, 3f));
            yield return WaitUntil(() => Vector3.Distance(friendOnHost.transform.position, new Vector3(4f, 0f, 3f)) < 0.3f,
                "the friend's move on the host's screen");
            Assert.That(_first.CoopHost.MovementCorrections, Is.EqualTo(0), "walking is never corrected");

            // Jumping across the map at once is not walking: the host puts the friend back where they could have got
            friendOnFriend.transform.position = new Vector3(60f, 0f, 3f);
            Physics.SyncTransforms();
            yield return WaitUntil(() => _second.CoopClient.Corrections > 0 && friendOnFriend.transform.position.x < 15f,
                "the host's correction on the friend's screen");
            Assert.That(_first.CoopHost.MovementCorrections, Is.GreaterThan(0));
            Assert.That(friendOnHost.transform.position.x, Is.LessThan(15f));

            // The host's goblin shows on the friend's screen, moved by the host
            int goblin = host.SpawnEnemy("goblin");
            EntityView goblinOnFriend = null;
            yield return WaitUntil(() => _second.Views.TryGetView(goblin, out goblinOnFriend), "the goblin on the friend's screen");
            Assert.That(goblinOnFriend.GetComponent<RemoteMotion>().enabled, Is.True);
            Assert.That(goblinOnFriend.GetComponent<EnemyBrain>() == null || !goblinOnFriend.GetComponent<EnemyBrain>().enabled, Is.True);

            // The friend strikes; the host's rules deal the damage and the kill comes back
            _first.CoopHost.MaxAttackDistance = 0f;
            yield return WaitUntil(() =>
            {
                if (friend.Get(goblin) != null)
                    friend.Attack(friendId, goblin);
                return host.Get(goblin) == null && friend.Get(goblin) == null && friend.Kills == 1;
            }, "the friend's kill");
            Assert.That(friend.TotalExperience, Is.EqualTo(host.TotalExperience).And.GreaterThan(0));
            Assert.That(_second.Views.TryGetView(goblin, out _), Is.False);

            // The host stops: the friend is told and plays alone again
            _first.LeaveGame();
            Assert.That(_first.Role, Is.EqualTo(CoopRole.Solo));
            Assert.That(host.Players, Has.Count.EqualTo(1));
            yield return WaitUntil(() => _second.Role == CoopRole.Solo && _second.World != null, "the friend back in a game of their own");
            Assert.That(_second.CoopStatus, Does.Contain("host left"));
            EntityView alone = null;
            yield return WaitUntil(() => _second.Views.TryGetView(_second.World.PlayerId, out alone), "the friend's own player");
            yield return null;
            Assert.That(alone.GetComponent<PlayerTagView>().ShownName, Is.Empty, "no name tags when playing alone");
        }

        [UnityTest]
        public IEnumerator EightPlayers_OverUdp_AllJoin_AndEachHasItsOwnColour()
        {
            _first.MaxPlayers = 8;
            Assert.That(_first.HostGame(), Is.True, _first.CoopStatus);
            var friends = new System.Collections.Generic.List<ActionRpgHost> { _second };
            for (int i = 3; i <= 8; i++)
            {
                var game = StartGame($"Player {i}");
                _more.Add(game);
                friends.Add(game);
            }
            foreach (var friend in friends)
            {
                friend.UsePlayerName(friend.name);
                friend.JoinGame("127.0.0.1");
            }

            yield return WaitUntil(() => friends.All(f => f.CoopClient?.State == Coop.CoopClientState.Playing),
                () => "seven friends to join: " + string.Join("; ", friends.Select(f => $"{f.name} {f.Role} {f.CoopClient?.State} '{f.CoopStatus}'")));
            var host = _first.World;
            Assert.That(host.Players, Has.Count.EqualTo(8));
            Assert.That(_first.CoopHost.MaxPlayers, Is.EqualTo(8));

            // Every machine shows all eight players, each ring in its seat's colour, and no two seats look the same
            yield return WaitUntil(() => friends.Append(_first).All(game => host.Players.All(id =>
                game.Views.TryGetView(id, out var view) && view.GetComponent<PlayerTagView>().ShownName != "")), "all players on all screens");
            var colours = host.Players.Select(id => _first.ColorOfSeat(host.Get(id).Seat)).ToList();
            Assert.That(colours.Distinct().Count(), Is.EqualTo(8));
            foreach (var game in friends)
                foreach (int id in host.Players)
                {
                    game.Views.TryGetView(id, out var view);
                    Assert.That(view.GetComponent<PlayerTagView>().ShownColor, Is.EqualTo(_first.ColorOfSeat(host.Get(id).Seat)));
                }

            // The last friend walks; the host and the first friend see it
            var last = friends[friends.Count - 1];
            int lastId = last.CurrentWorld.PlayerId;
            last.Views.TryGetView(lastId, out var walker);
            yield return Walk(walker.transform, new Vector3(-5f, 0f, 6f));
            yield return WaitUntil(() => _first.Views.TryGetView(lastId, out var onHost) && _second.Views.TryGetView(lastId, out var onFriend)
                                         && Vector3.Distance(onHost.transform.position, walker.transform.position) < 0.3f
                                         && Vector3.Distance(onFriend.transform.position, walker.transform.position) < 0.3f,
                "the last friend's move on other screens");
        }

        [UnityTest]
        public IEnumerator AnInstalledSessionModule_IsOffered_AndTheGamePlaysThroughIt()
        {
            // What an installed module (Unity Relay, ...) does: register itself; the game's code does not change
            var network = new Networking.LoopbackNetwork();
            Networking.NetworkSessionProviders.Register("test-rooms", "Test rooms", () => new Networking.LoopbackSessionProvider(network, 5000));
            try
            {
                var first = StartGame("Host via module");
                var second = StartGame("Friend via module");
                _more.Add(first);
                _more.Add(second);
                yield return null;

                var offered = first.SessionProviders.Select(p => p.DisplayName).ToList();
                Assert.That(offered[0], Is.EqualTo("Local network"));
                Assert.That(first.SessionProviders.OfType<Networking.LoopbackSessionProvider>().Count(), Is.EqualTo(1),
                    "the registered module is offered (other installed ones, such as Unity Relay, may be too)");
                Assert.That(first.SessionProvider.DisplayName, Is.EqualTo("Local network"), "the local network stays the default");
                Assert.That(first.UseSessionProvider("test-rooms"), Is.True);
                Assert.That(second.UseSessionProvider("test-rooms"), Is.True);
                Assert.That(first.UseSessionProvider("not-installed"), Is.False);

                Assert.That(first.HostGame(), Is.True, first.CoopStatus);
                yield return WaitUntil(() => first.Role == CoopRole.Hosting && first.JoinCode != "", "hosting through the module");
                second.JoinGame(first.JoinCode);
                yield return WaitUntil(() => second.CoopClient?.State == Coop.CoopClientState.Playing, () => "joining through the module: " + second.CoopStatus);
                Assert.That(first.World.Players, Has.Count.EqualTo(2));
            }
            finally
            {
                Networking.NetworkSessionProviders.Unregister("test-rooms");
            }
        }

        /// <summary>
        /// In-process rooms whose joined links can be cut, as a pulled cable would.
        /// </summary>
        private sealed class CablePuller : Networking.INetworkSessionProvider
        {
            private readonly Networking.LoopbackSessionProvider _rooms;

            public CablePuller(Networking.LoopbackNetwork network) => _rooms = new Networking.LoopbackSessionProvider(network, 5100);

            public Networking.LoopbackTransport LastJoined { get; private set; }
            public string DisplayName => "Test cables";
            public string JoinCodeHint => _rooms.JoinCodeHint;

            public UniTask<Networking.HostedSession> HostAsync(Networking.SessionOptions options, CancellationToken cancellation = default) =>
                _rooms.HostAsync(options, cancellation);

            public async UniTask<Networking.INetworkTransport> JoinAsync(string joinCode, CancellationToken cancellation = default)
            {
                var transport = await _rooms.JoinAsync(joinCode, cancellation);
                LastJoined = transport as Networking.LoopbackTransport;
                return transport;
            }
        }

        [UnityTest]
        public IEnumerator LostLink_TheFriendReconnectsByItself_IntoTheSameSeat()
        {
            var cables = new CablePuller(new Networking.LoopbackNetwork());
            Networking.NetworkSessionProviders.Register("test-cables", "Test cables", () => cables);
            try
            {
                var first = StartGame("Host with cables");
                var second = StartGame("Friend with cables");
                _more.Add(first);
                _more.Add(second);
                first.ConnectionTimeout = 1f;
                second.ConnectionTimeout = 1f;
                yield return null;
                Assert.That(first.UseSessionProvider("test-cables") && second.UseSessionProvider("test-cables"), Is.True);
                Assert.That(first.HostGame(), Is.True, first.CoopStatus);
                yield return WaitUntil(() => first.Role == CoopRole.Hosting && first.JoinCode != "", "hosting");
                second.JoinGame(first.JoinCode);
                yield return WaitUntil(() => second.CoopClient?.State == Coop.CoopClientState.Playing, () => "joining: " + second.CoopStatus);
                int seat = first.World.Get(first.World.Players.Last()).Seat;
                string name = first.World.Get(first.World.Players.Last()).DisplayName;

                cables.LastJoined.GoSilent();

                yield return WaitUntil(() => second.CoopStatus.Contains("Reconnecting"), () => "the friend to notice: " + second.CoopStatus);
                yield return WaitUntil(() => second.CoopClient?.State == Coop.CoopClientState.Playing && second.CoopClient.Rejoined,
                    () => "the friend to be back: " + second.CoopStatus);
                Assert.That(second.CoopStatus, Does.Contain("back in your seat"));
                Assert.That(first.World.Players, Has.Count.EqualTo(2));
                var back = first.World.Get(first.World.Players.Last());
                Assert.That(back.Seat, Is.EqualTo(seat));
                Assert.That(back.DisplayName, Is.EqualTo(name));
            }
            finally
            {
                Networking.NetworkSessionProviders.Unregister("test-cables");
            }
        }

        [UnityTest]
        public IEnumerator NobodyHosting_TheFriendIsToldAndPlaysAlone()
        {
            // A documentation-only address (TEST-NET-1): nothing answers there, on any machine
            _second.JoinGame("192.0.2.1");

            yield return WaitUntil(() => _second.Role == CoopRole.Solo && _second.World != null && !_second.IsSwitching
                                         && _second.CoopStatus.Contains("Could not reach"), "giving up on the address");
        }

        [UnityTest]
        public IEnumerator Pvp_TheTwoMeetAtTheirSpawnPoints_AndTheFriendWinsTheDuel()
        {
            var setup = Object.Instantiate(_setup);
            setup.Pvp.Enabled = true;
            setup.Pvp.Mode = PvpMode.Duel;
            setup.Pvp.RoundsToWin = 1;
            var first = StartGame("PvP host", setup, Port + 1);
            var second = StartGame("PvP friend", setup, Port + 1);
            _more.Add(first);
            _more.Add(second);
            yield return null;

            Assert.That(first.HostGame(), Is.True, first.CoopStatus);
            yield return WaitUntil(() => first.Role == CoopRole.Hosting, "hosting");
            second.JoinGame("127.0.0.1");
            yield return WaitUntil(() => second.CoopClient?.State == Coop.CoopClientState.Playing, "the friend to join");
            var host = first.World;
            var friend = second.CurrentWorld;
            int hostId = host.PlayerId, friendId = friend.PlayerId;
            yield return WaitUntil(() => friend.PvpStatus?.Phase == PvpPhase.Playing, "the duel to start on the friend's screen");
            Assert.That(friend.Get(hostId).Team, Is.EqualTo("Red"));
            Assert.That(friend.Player.Team, Is.EqualTo("Blue"));

            // Each stands at their side's spawn point, on both screens: the host placed the friend on their own machine
            var redSpot = setup.Pvp.SpawnPoint(0, 0, 2).position;
            var blueSpot = setup.Pvp.SpawnPoint(1, 0, 2).position;
            EntityView friendOnFriend = null, hostOnFriend = null;
            yield return WaitUntil(() => second.Views.TryGetView(friendId, out friendOnFriend) && second.Views.TryGetView(hostId, out hostOnFriend)
                                         && Flat(friendOnFriend.transform.position - blueSpot) < 0.3f
                                         && Flat(hostOnFriend.transform.position - redSpot) < 0.5f, "both at their spawn points");
            first.Views.TryGetView(hostId, out var hostOnHost);
            Assert.That(Flat(hostOnHost.transform.position - redSpot), Is.LessThan(0.3f));

            // Too far to strike; the friend walks up and strikes until the host falls
            var controller = friendOnFriend.GetComponent<PlayerController>();
            Assert.That(controller.TryAttackNearest(), Is.False);
            yield return Walk(friendOnFriend.transform, redSpot + (blueSpot - redSpot).normalized * 1.5f);
            yield return WaitUntil(() =>
            {
                controller.TryAttackNearest();
                return host.PvpStatus.Phase == PvpPhase.Finished;
            }, "the duel to end");
            Assert.That(host.PvpStatus.Winner, Is.EqualTo("Blue"));
            yield return WaitUntil(() => friend.PvpStatus.Phase == PvpPhase.Finished && friend.PvpStatus.Winner == "Blue",
                "the result on the friend's screen");
            Object.Destroy(setup);
        }

        private static float Flat(Vector3 offset) => new Vector2(offset.x, offset.z).magnitude;

        /// <summary>
        /// Moves a player the way walking would: a little every frame, slower than the character's speed (6).
        /// </summary>
        private static IEnumerator Walk(Transform walker, Vector3 to)
        {
            to.y = walker.position.y;
            float waited = 0f;
            while (Vector3.Distance(walker.position, to) > 0.01f)
            {
                walker.position = Vector3.MoveTowards(walker.position, to, 5f * Time.deltaTime);
                Physics.SyncTransforms();
                waited += Time.deltaTime;
                if (waited > Timeout)
                    Assert.Fail("Timed out walking");
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what) => WaitUntil(condition, () => what);

        private static IEnumerator WaitUntil(Func<bool> condition, Func<string> what)
        {
            float waited = 0f;
            while (!condition())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > Timeout)
                    Assert.Fail($"Timed out waiting for {what()}");
                yield return null;
            }
        }
    }
}
