using UnityEditor;
using UnityEngine;

namespace RPGFramework.Editor.EditorWindows
{
    /// <summary>
    /// Central hub window for all RPG Framework Editor tools.
    /// Provides quick access to all Framework features and documentation.
    /// </summary>
    public class FrameworkManagerWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private int selectedTab = 0;
        private readonly string[] tabs = { "Overview", "Systems", "Tools", "Documentation" };

        [MenuItem("RPG Framework/Tools/Framework Manager", false, 0)]
        public static void ShowWindow()
        {
            FrameworkManagerWindow window = GetWindow<FrameworkManagerWindow>("RPG Framework Manager");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.Space(10);

            selectedTab = GUILayout.Toolbar(selectedTab, tabs);

            EditorGUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawSystemsTab(); break;
                case 2: DrawToolsTab(); break;
                case 3: DrawDocumentationTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("RPG Framework Manager", titleStyle, GUILayout.Height(30));
            EditorGUILayout.LabelField("Central Hub for Framework Tools", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Framework Overview", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "RPG Framework is a universal, reusable framework for building RPG games in Unity.\n\n" +
                "It provides modular systems for:\n" +
                "- Combat & Stats\n" +
                "- Inventory & Items\n" +
                "- Quests & Dialogue\n" +
                "- Crafting & Trading\n" +
                "- Factions & Reputation\n" +
                "- Skills & Progression\n" +
                "- And more!",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Quick Stats", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawQuickStat("Framework Modules", "19");
            DrawQuickStat("Editor Tools", "14+");
            DrawQuickStat("Custom Editors", "10+");
            DrawQuickStat("Editor Windows", "7");

            EditorGUILayout.EndVertical();
        }

        private void DrawSystemsTab()
        {
            EditorGUILayout.LabelField("Framework Systems", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawSystemCategory("Core Systems", new[]
            {
                ("ECS", "Entity Component System"),
                ("Logging", "Framework logging system"),
                ("Serialization", "Data serialization"),
                ("Pooling", "Object pooling")
            });

            DrawSystemCategory("Gameplay Systems", new[]
            {
                ("Combat", "Combat and damage system"),
                ("Stats", "Character statistics"),
                ("Abilities", "Skills and abilities"),
                ("Status Effects", "Buffs and debuffs"),
                ("Progression", "Leveling and XP")
            });

            DrawSystemCategory("Item Systems", new[]
            {
                ("Inventory", "Inventory management"),
                ("Items", "Item definitions"),
                ("Loot", "Loot generation"),
                ("Crafting", "Crafting system")
            });

            DrawSystemCategory("Social Systems", new[]
            {
                ("Quests", "Quest system"),
                ("Dialogue", "Dialogue trees"),
                ("Trading", "NPC trading"),
                ("Factions", "Faction reputation")
            });
        }

        private void DrawToolsTab()
        {
            EditorGUILayout.LabelField("Framework Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // TODO: Quest Editor disabled - needs refactoring for struct-based QuestData
            // DrawToolButton("Quest Editor", "Create and edit quests visually", () => QuestEditorWindow.ShowWindow());

            // TODO: Dialogue Graph Editor disabled - needs refactoring for struct-based DialogueData
            // DrawToolButton("Dialogue Graph Editor", "Visual dialogue tree editor", () => DialogueGraphEditorWindow.ShowWindow());

            DrawToolButton("Item Database", "Browse and manage items", () => ItemDatabaseWindow.ShowWindow());

            // TODO: Faction Relationship Map disabled - needs refactoring for runtime Faction class
            // DrawToolButton("Faction Relationship Map", "Visualize faction relationships", () => FactionRelationshipMapWindow.ShowWindow());
            DrawToolButton("Achievement Browser", "Browse and search achievements", () => EditorGUILayout.HelpBox("Achievement Browser - Coming Soon", MessageType.Info));
            DrawToolButton("Loot Table Editor", "Edit loot tables and drop chances", () => LootTableEditorWindow.ShowWindow());

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
            DrawToolButton("Framework Validation", "Validate Framework data integrity", () => EditorGUILayout.HelpBox("Validation tool - Coming Soon", MessageType.Info));
            DrawToolButton("ScriptableObject Creator", "Create Framework ScriptableObjects", () => EditorGUILayout.HelpBox("Creator tool - Coming Soon", MessageType.Info));
        }

        private void DrawDocumentationTab()
        {
            EditorGUILayout.LabelField("Documentation", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Framework documentation and guides will be available here.\n\n" +
                "Topics:\n" +
                "- Getting Started\n" +
                "- System Guides\n" +
                "- API Reference\n" +
                "- Best Practices\n" +
                "- Examples",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open Framework Folder"))
            {
                string frameworkPath = "Packages/com.aizekhan.rpg-framework";
                EditorUtility.RevealInFinder(frameworkPath);
            }
        }

        private void DrawQuickStat(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSystemCategory(string categoryName, (string name, string description)[] systems)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(categoryName, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (var system in systems)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• {system.name}", GUILayout.Width(150));
                EditorGUILayout.LabelField(system.description, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawToolButton(string toolName, string description, System.Action onClickAction)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(toolName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);

            if (GUILayout.Button($"Open {toolName}"))
            {
                onClickAction?.Invoke();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
