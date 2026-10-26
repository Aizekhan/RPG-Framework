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
            Assert.That(_first.HostGame(), Is.True, _first.CoopStatus);
            Assert.That(_first.Role, Is.EqualTo(CoopRole.Hosting));

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
            yield return WaitUntil(() => _second.Views.TryGetView(_second.World.PlayerId, out _), "the friend's own player");
        }

        [UnityTest]
        public IEnumerator NobodyHosting_TheFriendIsToldAndPlaysAlone()
        {
            // A documentation-only address (TEST-NET-1): nothing answers there, on any machine
            _second.JoinGame("192.0.2.1");

            yield return WaitUntil(() => _second.Role == CoopRole.Solo && _second.World != null && !_second.IsSwitching
                                         && _second.CoopStatus.Contains("Could not reach"), "giving up on the address");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float waited = 0f;
            while (!condition())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > Timeout)
                    Assert.Fail($"Timed out waiting for {what}");
                yield return null;
            }
        }
    }
}
