using RPGFramework.Bootstrap.Unity;
using RPGFramework.Core.Composition;
using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// Scene entry point of Goblin Valley: starts the framework with the game's modules and draws a debug HUD.
    /// Space attacks the nearest goblin, N spawns a goblin, R revives the hero.
    /// </summary>
    public sealed class GoblinValleyHost : GameHostBehaviour
    {
        [SerializeField] private int _startingGoblins = 3;

        private GoblinValleyModule _module;

        public GoblinValleyGame Game => _module?.Game;

        protected override void ConfigureModules(IGameCompositionBuilder builder)
        {
            _module = GoblinValleyComposition.Configure(builder);
        }

        private void Start()
        {
            if (Game == null)
                return;
            for (int i = 0; i < _startingGoblins; i++)
                Game.SpawnGoblin();
        }

        protected override void Update()
        {
            base.Update();
            if (Game == null)
                return;

            if (Input.GetKeyDown(KeyCode.Space))
                Game.PlayerAttacksNearestGoblin();
            if (Input.GetKeyDown(KeyCode.N))
                Game.SpawnGoblin();
            if (Input.GetKeyDown(KeyCode.R))
                Game.RevivePlayer();
        }

        private void OnGUI()
        {
            var game = Game;
            if (game == null)
            {
                GUILayout.Label("Goblin Valley failed to start; see the Console");
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 420, 480));
            var player = game.Player;
            GUILayout.Label($"Hero - Level {game.Level} ({game.TotalXp} XP)");
            GUILayout.Label($"Health {player.Health:F0}/{player.MaxHealth:F0}   Attack {player.Attack:F0}   Defense {player.Defense:F0}");
            GUILayout.Label($"Goblins slain: {game.GoblinsKilled}   Backpack: {game.Inventory.Count} items");
            GUILayout.Label(game.QuestCompleted ? "Clear the Valley: done" : "Clear the Valley: in progress");
            GUILayout.Label("Space attack · N spawn goblin · R revive");
            GUILayout.Space(8);
            foreach (var line in game.Log)
                GUILayout.Label(line);
            GUILayout.EndArea();
        }
    }
}
