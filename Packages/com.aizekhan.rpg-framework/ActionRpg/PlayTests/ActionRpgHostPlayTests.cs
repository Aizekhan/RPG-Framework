using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Cameras;
using RPGFramework.Combat;
using RPGFramework.Items;
using RPGFramework.Tools.ItemDatabaseManager;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
        private ItemAsset _gem;
        private Sprite _gemIcon;

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
            _gem = ScriptableObject.CreateInstance<ItemAsset>();
            _gem.itemId = "gem";
            _gem.itemName = "Blue Gem";
            _gem.itemType = ItemType.Crafting;
            _gem.maxStackSize = 10;
            _gemIcon = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);
            _gem.icon = _gemIcon;
            _setup.Items.Add(_gem);

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
            Object.Destroy(_gem);
            Object.Destroy(_gemIcon);
        }

        [UnityTest]
        public IEnumerator ScenePlaces_AndScreensMadeInCode_Work()
        {
            var world = _host.World;
            yield return WaitUntil(() => _host.Views != null && _host.Views.TryGetView(world.PlayerId, out _), "player view");
            _host.Views.TryGetView(world.PlayerId, out var playerView);

            // A chest beside the player: the host adds it to the world where it stands
            var chest = new GameObject("chest");
            chest.transform.position = playerView.transform.position + Vector3.right;
            var lid = new GameObject("Lid");
            lid.transform.SetParent(chest.transform, false);
            chest.SetActive(false);
            var place = chest.AddComponent<InteractionPointView>();
            SetField(place, "_kind", "chest");
            SetField(place, "_prompt", "Open the chest");
            SetField(place, "_once", true);
            SetField(place, "_shownWhileOpen", lid);
            chest.SetActive(true);
            var opened = new System.Collections.Generic.List<RPGFramework.Interaction.InteractedEvent>();
            _host.Host.Container.Resolve<RPGFramework.Events.IEventBus>().Subscribe<RPGFramework.Interaction.InteractedEvent>(opened.Add);
            yield return null;

            Assert.That(world.Interactions.Get("chest"), Is.Not.Null);
            Assert.That(world.NearestInteraction?.Prompt, Is.EqualTo("Open the chest"));
            Assert.That(_host.CurrentWorld.Interact(), Is.True);
            Assert.That(opened.Single().PointId, Is.EqualTo("chest"));
            yield return null;
            Assert.That(lid.activeSelf, Is.False, "the opened chest shows open");
            Assert.That(world.NearestInteraction, Is.Null);

            // Out of reach once it moves away; gone with its object
            Object.Destroy(chest);
            yield return null;
            Assert.That(world.Interactions.Get("chest"), Is.Null);

            // A screen made in code opens through the kit's navigation
            _host.Screens.Register<RPGFramework.UI.Panels.StatListPanel>("stats", panel =>
                panel.Bind(new (string, Func<string>)[] { ("Level", () => world.Level.ToString()) }));
            var open = _host.Navigation.NavigateToAsync("stats");
            yield return WaitUntil(() => open.Status != Cysharp.Threading.Tasks.UniTaskStatus.Pending, "the screen");
            var screen = (RPGFramework.UI.Panels.StatListPanel)open.GetAwaiter().GetResult();
            Assert.That(screen.IsVisible, Is.True);
            Assert.That(screen.ValueAt(0), Is.EqualTo("1"));
            Assert.That(screen.Navigation, Is.SameAs(_host.Navigation));
            Assert.That(UnityEngine.EventSystems.EventSystem.current != null
                        || Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null, Is.True, "buttons can be clicked");
            screen.Close();
            yield return null;
            Assert.That(screen.IsVisible, Is.True, "the only screen: Close goes back, there is nothing before it");
            var clear = _host.Navigation.ClearAsync();
            yield return WaitUntil(() => clear.Status != Cysharp.Threading.Tasks.UniTaskStatus.Pending, "closing");
            Assert.That(screen.IsVisible, Is.False);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);

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

            // The character sheet opens and shows the backpack with item icons
            Assert.That(world.GiveItem("gem", 2), Is.True);
            var hud = _host.GetComponent<ActionRpgHud>();
            hud.ToggleSheet();
            yield return null;
            Assert.That(hud.IsSheetOpen, Is.True);
            Assert.That(hud.GetComponentsInChildren<Image>().Any(image => image.sprite == _gemIcon && image.enabled), Is.True,
                "the gem shows its icon");
            Assert.That(hud.GetComponentsInChildren<Text>().Any(text => text.text.Contains("Blue Gem") && text.text.Contains("x2")), Is.True);

            // Diagnostics are ready behind F3
            Assert.That(_host.Diagnostics, Is.Not.Null);
            Assert.That(_host.Diagnostics.Visible, Is.False);
            _host.Diagnostics.Refresh();
            Assert.That(_host.Diagnostics.Text, Does.Contain("Systems").And.Contain("Events"));
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

        private ActionRpgHost StartHost(bool saving, bool startOnRequest = false)
        {
            var hostObject = new GameObject("Game");
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<ActionRpgHost>();
            host.UseSetup(_setup);
            host.UseSaving(saving);
            host.UseStartOnRequest(startOnRequest);
            hostObject.SetActive(true);
            return host;
        }

        [UnityTest]
        public IEnumerator StartOnRequest_WaitsForTheMenu_ThenContinuesOrStartsOver()
        {
            Object.Destroy(_host.gameObject);
            _host = null;
            yield return null;
            _setup.name = "StartTest_" + Guid.NewGuid().ToString("N");
            string directory = ActionRpgHost.SaveDirectoryOf(_setup);
            try
            {
                var first = StartHost(saving: true);
                yield return null;
                var lifeStates = first.Host.Container.Resolve<ILifeStateManager>();
                for (int i = 0; i < 5; i++)
                    lifeStates.Kill(first.World.SpawnEnemy("goblin"), first.World.PlayerId, "test");
                int level = first.World.Level;
                Assume.That(level, Is.GreaterThan(1));
                Object.Destroy(first.gameObject); // quitting saves
                yield return null;
                Assert.That(ActionRpgHost.SavedGameOf(_setup).Level, Is.EqualTo(level), "the menu reads the save without a game");

                // Waiting for the menu: nothing is shown, the world does not run and the save is not loaded yet
                _host = StartHost(saving: true, startOnRequest: true);
                var started = new System.Collections.Generic.List<GameStart>();
                _host.GameStarted += started.Add;
                yield return new WaitForSecondsRealtime(0.5f); // past the first wave's delay
                Assert.That(_host.IsStarted, Is.False);
                Assert.That(_host.Views, Is.Null);
                Assert.That(_host.World.Level, Is.EqualTo(1));
                Assert.That(_host.World.LivingEnemies.Count(), Is.Zero, "no wave before the game starts");
                Assert.That(_host.SavedGame.Level, Is.EqualTo(level));
                Assert.That(_host.StartGame(GameStart.Join("")), Is.False, "joining needs a code");

                Assert.That(_host.StartGame(GameStart.Continue), Is.True);
                Assert.That(_host.IsStarted, Is.True);
                Assert.That(_host.World.Level, Is.EqualTo(level));
                Assert.That(_host.Views, Is.Not.Null);
                Assert.That(started.Single().Kind, Is.EqualTo(GameStartKind.Continue));
                Assert.That(_host.StartGame(GameStart.NewGame), Is.False, "once per host");
                Object.Destroy(_host.gameObject);
                yield return null;

                // A new game deletes the save and starts over
                _host = StartHost(saving: true, startOnRequest: true);
                yield return null;
                Assert.That(_host.StartGame(GameStart.NewGame), Is.True);
                Assert.That(_host.World.Level, Is.EqualTo(1));
                Assert.That(_host.SavedGame, Is.Null);
            }
            finally
            {
                if (_host != null)
                    Object.DestroyImmediate(_host.gameObject);
                if (System.IO.Directory.Exists(directory))
                    System.IO.Directory.Delete(directory, true);
            }
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
