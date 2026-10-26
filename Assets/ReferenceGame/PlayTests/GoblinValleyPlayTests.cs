using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
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
    /// Plays the real Goblin Valley scene: the hero and a wave appear as prefabs, goblins chase the hero, the hero's
    /// strike reaches the game rules and slain goblins disappear from the scene. Any logged error fails the test.
    /// </summary>
    public sealed class GoblinValleyPlayTests
    {
        private const string ScenePath = "Assets/ReferenceGame/Scenes/GoblinValley.unity";
        private const float Timeout = 10f;

        private GoblinValleyHost _host;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The host survives scene loads (DontDestroyOnLoad); later tests must not inherit it
            if (_host != null)
                Object.Destroy(_host.gameObject);
            yield return null;
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
            Assert.That(_host.Game, Is.Not.Null, "the game failed to start");
            var game = _host.Game;

            yield return WaitUntil(() => _host.Views != null && _host.Views.TryGetView(game.PlayerId, out _), "hero view");
            yield return WaitUntil(() => game.LivingGoblins.Any() && game.LivingGoblins.All(id => _host.Views.TryGetView(id, out _)),
                "first wave with views");

            _host.Views.TryGetView(game.PlayerId, out var heroView);
            var hero = heroView.GetComponent<HeroController>();
            Assert.That(hero, Is.Not.Null);
            Assert.That(Camera.main.GetComponent<RPGFramework.Cameras.CameraFollow>().Target, Is.SameAs(heroView.transform));

            // Goblins run at the hero
            int goblin = game.LivingGoblins.First();
            _host.Views.TryGetView(goblin, out var goblinView);
            float before = Vector3.Distance(goblinView.transform.position, heroView.transform.position);
            yield return new WaitForSeconds(0.5f);
            float after = Vector3.Distance(goblinView.transform.position, heroView.transform.position);
            Assert.That(after, Is.LessThan(before));

            // The hero's strike reaches the rules
            goblinView.transform.position = heroView.transform.position + heroView.transform.forward;
            float health = game.Get(goblin).Health;
            Assert.That(hero.TryAttackNearest(), Is.True);
            Assert.That(game.Get(goblin) == null || game.Get(goblin).Health < health, Is.True);

            // Slain goblins leave the scene; their pooled views go back to the pool
            var lifeStates = _host.Host.Container.Resolve<RPGFramework.Combat.ILifeStateManager>();
            foreach (int id in game.LivingGoblins.ToList())
                lifeStates.Kill(id, game.PlayerId, "test");
            yield return null;
            Assert.That(_host.Views.Count, Is.EqualTo(1));
            Assert.That(goblinView == null || !goblinView.gameObject.activeSelf, Is.True);
            Assert.That(game.GoblinsKilled, Is.GreaterThan(0));
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
