using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ActionRpg;
using RPGFramework.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ReferenceGame.PlayTests
{
    /// <summary>
    /// Plays the real Goblin Valley scene: the hero and a wave appear as their model prefabs, goblins chase the hero,
    /// the hero's strike reaches the rules, slain goblins leave the scene and the kill streak counts. Any logged error
    /// fails the test.
    /// </summary>
    public sealed class GoblinValleyPlayTests
    {
        private const string ScenePath = "Assets/ReferenceGame/Scenes/GoblinValley.unity";
        private const float Timeout = 10f;

        private GoblinValleyHost _host;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null)
            {
                // The scene saves progress; leave none behind
                var directory = _host.SaveDirectory;
                Object.Destroy(_host.gameObject);
                yield return null;
                if (System.IO.Directory.Exists(directory))
                    System.IO.Directory.Delete(directory, true);
            }
        }

        [UnityTest]
        public IEnumerator Scene_PlaysTheValley()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Loads the scene by path; Editor only");
#endif
            _host = Object.FindFirstObjectByType<GoblinValleyHost>();
            Assert.That(_host, Is.Not.Null);
            var world = _host.World;
            Assert.That(world, Is.Not.Null, "the game failed to start");

            yield return WaitUntil(() => _host.Views != null && _host.Views.TryGetView(world.PlayerId, out _), "hero view");
            yield return WaitUntil(() => world.LivingEnemies.Any() && world.LivingEnemies.All(id => _host.Views.TryGetView(id, out _)),
                "first wave with views");

            _host.Views.TryGetView(world.PlayerId, out var heroView);
            Assert.That(heroView.transform.Find("Model/Sword"), Is.Not.Null, "the hero shows its model prefab");
            var hero = heroView.GetComponent<PlayerController>();
            Assert.That(hero, Is.Not.Null);
            Assert.That(Camera.main.GetComponent<RPGFramework.Cameras.CameraFollow>().Target, Is.SameAs(heroView.transform));

            // Goblins run at the hero
            int goblin = world.LivingEnemies.First();
            _host.Views.TryGetView(goblin, out var goblinView);
            Assert.That(goblinView.GetComponent<EnemyBrain>(), Is.Not.Null);
            float before = Vector3.Distance(goblinView.transform.position, heroView.transform.position);
            yield return new WaitForSeconds(0.5f);
            float after = Vector3.Distance(goblinView.transform.position, heroView.transform.position);
            Assert.That(after, Is.LessThan(before));

            // The hero's strike reaches the rules
            goblinView.transform.position = heroView.transform.position + heroView.transform.forward;
            float health = world.Get(goblin).Health;
            Assert.That(hero.TryAttackNearest(), Is.True);
            Assert.That(world.Get(goblin) == null || world.Get(goblin).Health < health, Is.True);

            // Slain goblins leave the scene and count for the streak
            var lifeStates = _host.Host.Container.Resolve<ILifeStateManager>();
            foreach (int id in world.LivingEnemies.ToList())
                lifeStates.Kill(id, world.PlayerId, "test");
            yield return null;
            Assert.That(_host.Views.Count, Is.EqualTo(1));
            Assert.That(world.Kills, Is.GreaterThan(0));
            Assert.That(_host.KillStreak.Best, Is.GreaterThan(0));
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
