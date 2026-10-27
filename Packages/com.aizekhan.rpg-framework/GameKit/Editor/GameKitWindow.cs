using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.GameKit.Editor
{
    /// <summary>
    /// RPG Framework > Game Kit: create a game or a feature, check a game's architecture.
    /// </summary>
    public sealed class GameKitWindow : EditorWindow
    {
        private string _game = "MyGame";
        private string _feature = "Shop";
        private bool _installAIGuidance;
        private string _message;
        private MessageType _messageType;

        public static void Open()
        {
            var window = GetWindow<GameKitWindow>(true, "Game Kit");
            window.minSize = new Vector2(420f, 300f);
            var existing = ExistingGames();
            if (existing.Length > 0)
                window._game = existing[0];
        }

        [MenuItem("RPG Framework/Game Kit/Check Architecture", priority = 10)]
        public static void CheckFromMenu()
        {
            var games = ExistingGames();
            if (games.Length == 0)
            {
                Debug.LogWarning("[Game Kit] No game found (a folder Assets/<Game>/Scripts/Core with <Game>Composition.cs)");
                return;
            }
            foreach (var game in games)
                LogCheck(game);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Game", EditorStyles.boldLabel);
            _game = EditorGUILayout.TextField("Name", _game);
            _installAIGuidance = EditorGUILayout.ToggleLeft(
                new GUIContent("Also install AI guidance", "Adds a block to CLAUDE.md, Docs/RPGFramework and a CI workflow at the project root"),
                _installAIGuidance);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Game"))
                    Run(() => GameScaffold.NewGame(_game, _installAIGuidance),
                        $"Created Assets/{_game} and opened its scene: press Play. Tune it in Assets/{_game}/Content/{_game}Setup.asset.");
                if (GUILayout.Button("Check Architecture"))
                    Run(() => LogCheck(_game), "Architecture checked; see the Console.");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Feature (in the game above)", EditorStyles.boldLabel);
            _feature = EditorGUILayout.TextField("Name", _feature);
            if (GUILayout.Button("New Feature"))
                Run(() => GameScaffold.NewFeature(_game, _feature),
                    $"Created {_feature} (module, rules, events, tests) in {_game}; its module is in {_game}Composition.");

            EditorGUILayout.Space();
            if (GUILayout.Button("Install AI Guidance (CLAUDE.md, Docs/RPGFramework)"))
                Run(AIGuidance.InstallFromMenu, "Installed CLAUDE.md and Docs/RPGFramework.");

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "New Game makes a real-time action RPG (ActionRpg). For a turn-based party RPG, compose TurnBased " +
                "(TurnBasedComposition.Configure: turns, abilities, effects, enemy AI, rewards, saves) and build its " +
                "battle screen; see recipe 15. Recipe 14 lists the core services every game should use.",
                MessageType.None);

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, _messageType);
        }

        private void Run(Action action, string success)
        {
            try
            {
                action();
                _message = success;
                _messageType = MessageType.Info;
            }
            catch (Exception exception)
            {
                _message = exception.Message;
                _messageType = MessageType.Error;
            }
        }

        private static void LogCheck(string game)
        {
            var codebase = GameCodebase.Load($"Assets/{game}");
            if (codebase.Assemblies.Count == 0)
                throw new InvalidOperationException($"No compiled scripts under Assets/{game}");

            var violations = GameArchitectureRules.CheckAll(codebase);
            if (violations.Count == 0)
                Debug.Log($"[Game Kit] {game}: all {GameArchitectureRules.All.Count} architecture rules hold");
            else
                Debug.LogError($"[Game Kit] {game}: {violations.Count} architecture violation(s) " +
                               $"(rules: {GameArchitectureRules.Guide})\n" + string.Join("\n", violations));
        }

        private static string[] ExistingGames() =>
            Directory.GetDirectories(Application.dataPath)
                .Select(Path.GetFileName)
                .Where(name => File.Exists(Path.Combine(Application.dataPath, name, "Scripts", "Core", $"{name}Composition.cs")))
                .ToArray();
    }
}
