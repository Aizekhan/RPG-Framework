using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Cameras;
using RPGFramework.Combat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RPGFramework.ActionRpg.PlayTests
{
    /// <summary>
    /// A game made only of assets: a host with a Game Setup whose characters have no prefabs. The player and a wave
    /// appear as placeholders with controls, AI and health bars added; enemies chase the player; the player's strike
    /// reaches the rules; slain enemies leave the scene. Any logged error fails the test.
    /// </summary>
    public sealed class ActionRpgHostPlayTests
    {
        private const float Timeout = 10f;

        private GameObject _camera;
        private ActionRpgHost _host;
        private GameSetup _setup;

        [SetUp]
        public void SetUp()
        {
            if (Camera.main == null)
                _camera = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };

            var hero = CharacterDefinition.Create("hero", "Hero", 200f, 40f, 5f);
            hero.SetMovement(6f, 2.4f, 0.4f);
            var goblin = CharacterDefinition.Create("goblin", "Goblin", 30f, 4f, 1f, 40);
            _setup = GameSetup.Create(hero, new[] { goblin },
                new WaveSettings { FirstWaveDelay = 0.2f, FirstWaveSize = 2, DelayBetweenWaves = 60f });
            _setup.SetSeed(5);

            var hostObject = new GameObject("Game");
            hostObject.SetActive(false);
            _host = hostObject.AddComponent<ActionRpgHost>();
            _host.UseSetup(_setup);
            hostObject.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null)
                Object.Destroy(_host.gameObject);
            if (_camera != null)
                Object.Destroy(_camera);
            yield return null;
            foreach (var character in _setup.Characters.ToList())
                Object.Destroy(character);
            Object.Destroy(_setup);
        }

        [UnityTest]
        public IEnumerator AssetsOnlyGame_Plays()
        {
            var world = _host.World;
            Assert.That(world, Is.Not.Null, "the game failed to start");

            yield return WaitUntil(() => _host.Views != null && _host.Views.TryGetView(world.PlayerId, out _), "player view");
            yield return WaitUntil(() => world.LivingEnemies.Count() == 2 && world.LivingEnemies.All(id => _host.Views.TryGetView(id, out _)),
                "first wave with views");

            _host.Views.TryGetView(world.PlayerId, out var playerView);
            var player = playerView.GetComponent<PlayerController>();
            Assert.That(player, Is.Not.Null, "player controls added to the placeholder");
            Assert.That(playerView.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(playerView.GetComponentInChildren<HealthBarView>(), Is.Not.Null);
            Assert.That(Camera.main.GetComponent<CameraFollow>().Target, Is.SameAs(playerView.transform));
            Assert.That(_host.GetComponent<ActionRpgHud>(), Is.Not.Null);

            int enemy = world.LivingEnemies.First();
            _host.Views.TryGetView(enemy, out var enemyView);
            Assert.That(enemyView.GetComponent<EnemyBrain>(), Is.Not.Null, "enemy AI added to the placeholder");

            // Enemies run at the player
            float before = Vector3.Distance(enemyView.transform.position, playerView.transform.position);
            yield return new WaitForSeconds(0.5f);
            float after = Vector3.Distance(enemyView.transform.position, playerView.transform.position);
            Assert.That(after, Is.LessThan(before));

            // The player's strike reaches the rules
            enemyView.transform.position = playerView.transform.position + playerView.transform.forward;
            float health = world.Get(enemy).Health;
            Assert.That(player.TryAttackNearest(), Is.True);
            Assert.That(world.Get(enemy) == null || world.Get(enemy).Health < health, Is.True);

            // Slain enemies leave the scene and give experience
            var lifeStates = _host.Host.Container.Resolve<ILifeStateManager>();
            foreach (int id in world.LivingEnemies.ToList())
                lifeStates.Kill(id, world.PlayerId, "test");
            yield return null;
            Assert.That(_host.Views.Count, Is.EqualTo(1));
            Assert.That(world.Kills, Is.EqualTo(2));
            Assert.That(world.TotalExperience, Is.EqualTo(80));

            // The character sheet opens and fills without errors
            var hud = _host.GetComponent<ActionRpgHud>();
            hud.ToggleSheet();
            yield return null;
            Assert.That(hud.IsSheetOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedProgress_ComesBackNextSession()
        {
            Object.Destroy(_host.gameObject);
            yield return null;
            _setup.name = "SaveTest_" + Guid.NewGuid().ToString("N");

            var first = StartHost(saving: true);
            yield return null;
            Assert.That(first.Saves, Is.Not.Null);
            var lifeStates = first.Host.Container.Resolve<ILifeStateManager>();
            for (int i = 0; i < 5; i++)
                lifeStates.Kill(first.World.SpawnEnemy("goblin"), first.World.PlayerId, "test");
            int level = first.World.Level;
            long experience = first.World.TotalExperience;
            Assume.That(level, Is.GreaterThan(1));
            string directory = first.SaveDirectory;
            Object.Destroy(first.gameObject); // quitting saves
            yield return null;

            try
            {
                var second = StartHost(saving: true);
                _host = second;
                yield return null;
                Assert.That(second.World.Level, Is.EqualTo(level));
                Assert.That(second.World.TotalExperience, Is.EqualTo(experience));
                Assert.That(second.World.Kills, Is.EqualTo(5));
            }
            finally
            {
                if (_host != null)
                    Object.DestroyImmediate(_host.gameObject);

                if (System.IO.Directory.Exists(directory))
                    System.IO.Directory.Delete(directory, true);
            }
        }

        private ActionRpgHost StartHost(bool saving)
        {
            var hostObject = new GameObject("Game");
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<ActionRpgHost>();
            host.UseSetup(_setup);
            host.UseSaving(saving);
            hostObject.SetActive(true);
            return host;
        }

        [UnityTest]
        public IEnumerator BrokenSetup_DoesNotStartAndSaysWhy()
        {
            Object.Destroy(_host.gameObject);
            yield return null;

            _setup.SetPlayer(null);
            var hostObject = new GameObject("Broken");
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<ActionRpgHost>();
            host.UseSetup(_setup);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No player character"));
            // The host also logs the failed start as critical
            LogAssert.ignoreFailingMessages = true;
            hostObject.SetActive(true);
            _host = host;

            Assert.That(host.World, Is.Null);
            Assert.That(host.enabled, Is.False);
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
