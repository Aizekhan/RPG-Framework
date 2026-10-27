using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Cameras;
using RPGFramework.Presentation;
using RPGFramework.Pooling.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RPGFramework.ActionRpg.PlayTests
{
    /// <summary>
    /// A game moving from its hub to a run: the game, its characters and a friend who joined go on in the new scene,
    /// the run scene's own host steps aside, the camera follows the player there, pooled objects survive. Any logged
    /// error fails the test.
    /// </summary>
    public sealed class SceneChangePlayTests
    {
        private const float Timeout = 15f;
        private const int Port = 17795;

        private GameSetup _setup;
        private SceneChangingHost _host;
        private ActionRpgHost _friend;
        private GameObject _prefab;
        private Scene _hub;

        [SetUp]
        public void SetUp()
        {
            _hub = SceneManager.CreateScene("Hub");
            SceneManager.SetActiveScene(_hub);
            new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };

            var hero = CharacterDefinition.Create("hero", "Hero", 200f, 40f, 5f);
            hero.SetMovement(6f, 2.4f, 0.4f);
            var goblin = CharacterDefinition.Create("goblin", "Goblin", 30f, 4f, 1f, 40);
            _setup = GameSetup.Create(hero, new[] { goblin }, new WaveSettings { Enabled = false });
            _setup.SetSeed(5);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_friend != null)
                Object.Destroy(_friend.gameObject);
            if (_host != null)
                Object.Destroy(_host.gameObject);
            if (_prefab != null)
                Object.Destroy(_prefab);
            yield return null;
            // Back to the test runner's scene; drop the ones the test made
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == "Hub" || scene.name == "Run")
                {
                    if (SceneManager.sceneCount > 1)
                        yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            foreach (var character in _setup.Characters.ToList())
                Object.Destroy(character);
            Object.Destroy(_setup);
        }

        private T StartGame<T>(string name) where T : ActionRpgHost
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            var host = gameObject.AddComponent<T>();
            host.UseSetup(_setup);
            host.UseCoopPort(Port);
            gameObject.SetActive(true);
            return host;
        }

        // The run scene: its own camera, and a host so it can be played on its own in the Editor
        private void BuildRun(string sceneName)
        {
            new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            var duplicate = new GameObject("Run's own game");
            duplicate.SetActive(false);
            duplicate.AddComponent<ActionRpgHost>().UseSetup(_setup);
            duplicate.SetActive(true);
        }

        [UnityTest]
        public IEnumerator ChangeScene_TheGameAndTheFriendGoOn_InTheNewScene()
        {
            _host = StartGame<SceneChangingHost>("Game");
            _host.Populate = BuildRun;
            _friend = StartGame<ActionRpgHost>("Friend");
            yield return null; // the games show themselves on their first frame
            Assert.That(_host.HostGame(), Is.True, _host.CoopStatus);
            yield return WaitUntil(() => _host.Role == CoopRole.Hosting, "hosting");
            _friend.JoinGame("127.0.0.1");
            yield return WaitUntil(() => _friend.CoopClient?.State == Coop.CoopClientState.Playing, "the friend to join");

            var world = _host.World;
            int friendId = _friend.CurrentWorld.PlayerId;
            int goblin = world.SpawnEnemy("goblin");
            EntityView player = null, friendOnHost = null;
            yield return WaitUntil(() => _host.Views.TryGetView(world.PlayerId, out player)
                                         && _host.Views.TryGetView(friendId, out friendOnHost)
                                         && _friend.Views.TryGetView(goblin, out _), "everyone on both screens");

            bool changed = false;
            yield return _host.ChangeScene("Run").ToCoroutine(result => changed = result);

            Assert.That(changed, Is.True);
            Assert.That(_hub.isLoaded, Is.False, "the hub is gone");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Run"));
            Assert.That(_host.ScenesChanged, Is.EqualTo(new[] { "Run" }));
            Assert.That(Object.FindObjectsByType<ActionRpgHost>(FindObjectsSortMode.None), Has.Length.EqualTo(2),
                "the run scene's own host stepped aside for the two games already running");
            Assert.That(_host.World, Is.SameAs(world), "the same game goes on");
            Assert.That(player != null && friendOnHost != null, Is.True, "the characters came along");
            Assert.That(_host.Views.TryGetView(goblin, out _), Is.True);
            var camera = Camera.main;
            Assert.That(camera.gameObject.scene.name, Is.EqualTo("Run"));
            Assert.That(camera.GetComponent<CameraFollow>()?.Target, Is.EqualTo(player.transform), "the new camera follows the player");

            // The friend is told and still plays in the host's game
            yield return WaitUntil(() => _friend.CoopClient?.Scene == "Run", "the friend told of the new scene");
            Assert.That(_friend.Role, Is.EqualTo(CoopRole.Joined));
            Assert.That(_host.CoopHost.Scene, Is.EqualTo("Run"));
            _host.CoopHost.MaxAttackDistance = 0f;
            world.Get(goblin)?.SetHealth(1f);
            yield return WaitUntil(() =>
            {
                if (_friend.CurrentWorld.Get(goblin) != null)
                    _friend.CurrentWorld.Attack(friendId, goblin);
                return world.Get(goblin) == null;
            }, "the friend's kill in the new scene");

            // Changing again while joined is not the friend's to do
            bool refused = true;
            yield return _friend.ChangeScene("Hub").ToCoroutine(result => refused = !result);
            Assert.That(refused, Is.True);
        }

        [UnityTest]
        public IEnumerator PooledObjects_SurviveASceneChange()
        {
            _prefab = new GameObject("Crate");
            Object.DontDestroyOnLoad(_prefab);
            var pool = new GameObjectPool(_prefab, maxSize: 4);
            var crate = pool.Get();
            pool.Release(crate);

            var run = SceneManager.CreateScene("Run");
            SceneManager.SetActiveScene(run);
            yield return SceneManager.UnloadSceneAsync(_hub);

            Assert.That(pool.Container != null, Is.True, "the pool's container outlived the hub");
            Assert.That(crate != null, Is.True, "the pooled crate too");
            Assert.That(pool.Get(), Is.SameAs(crate), "and is handed out again");
            pool.Dispose();
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float waited = 0f;
            while (!condition())
            {
                if (waited > Timeout)
                    Assert.Fail($"Timed out waiting for {what}");
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
