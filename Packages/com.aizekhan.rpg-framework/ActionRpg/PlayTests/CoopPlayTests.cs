using System;
using System.Collections;
using System.Linq;
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
    /// Any logged error fails the test.
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

        private ActionRpgHost StartGame(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            var host = gameObject.AddComponent<ActionRpgHost>();
            host.UseSetup(_setup);
            host.UseCoopPort(Port);
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
            friendOnFriend.transform.position = new Vector3(4f, 0f, 3f);
            Physics.SyncTransforms();
            yield return WaitUntil(() => Vector3.Distance(friendOnHost.transform.position, new Vector3(4f, 0f, 3f)) < 0.3f,
                "the friend's move on the host's screen");

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
            walker.transform.position = new Vector3(-5f, 0f, 6f);
            Physics.SyncTransforms();
            yield return WaitUntil(() => _first.Views.TryGetView(lastId, out var onHost) && _second.Views.TryGetView(lastId, out var onFriend)
                                         && Vector3.Distance(onHost.transform.position, walker.transform.position) < 0.3f
                                         && Vector3.Distance(onFriend.transform.position, walker.transform.position) < 0.3f,
                "the last friend's move on other screens");
        }

        [UnityTest]
        public IEnumerator NobodyHosting_TheFriendIsToldAndPlaysAlone()
        {
            // A documentation-only address (TEST-NET-1): nothing answers there, on any machine
            _second.JoinGame("192.0.2.1");

            yield return WaitUntil(() => _second.Role == CoopRole.Solo && _second.World != null && !_second.IsSwitching
                                         && _second.CoopStatus.Contains("Could not reach"), "giving up on the address");
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
