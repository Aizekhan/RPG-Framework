using UnityEditor;
using UnityEngine;
using System.Linq;

namespace RPGFramework.Editor.Utilities
{
    /// <summary>
    /// Menu items for RPG Framework tools and utilities.
    /// Provides quick access to Framework features from Unity menu.
    /// </summary>
    public static class FrameworkMenuItems
    {
        private const string MENU_ROOT = "RPG Framework/Tools/";

        // ===== Framework Manager =====

        [MenuItem(MENU_ROOT + "Framework Manager %#F", false, 0)]
        public static void OpenFrameworkManager()
        {
            EditorWindows.FrameworkManagerWindow.ShowWindow();
        }

        // ===== Editor Windows =====

        // TODO: Quest Editor disabled - needs refactoring for struct-based QuestData
        // [MenuItem(MENU_ROOT + "Quest Editor", false, 100)]
        // public static void OpenQuestEditor()
        // {
        //     EditorWindows.QuestEditorWindow.ShowWindow();
        // }

        // TODO: Dialogue Graph Editor disabled - needs refactoring for struct-based DialogueData
        // [MenuItem(MENU_ROOT + "Dialogue Graph Editor", false, 101)]
        // public static void OpenDialogueGraphEditor()
        // {
        //     EditorWindows.DialogueGraphEditorWindow.ShowWindow();
        // }

        [MenuItem(MENU_ROOT + "Item Database", false, 102)]
        public static void OpenItemDatabase()
        {
            EditorWindows.ItemDatabaseWindow.ShowWindow();
        }

        // TODO: Faction Relationship Map disabled - needs refactoring for runtime Faction class
        // [MenuItem(MENU_ROOT + "Faction Relationship Map", false, 103)]
        // public static void OpenFactionRelationshipMap()
        // {
        //     EditorWindows.FactionRelationshipMapWindow.ShowWindow();
        // }

        [MenuItem(MENU_ROOT + "Loot Table Editor", false, 104)]
        public static void OpenLootTableEditor()
        {
            EditorWindows.LootTableEditorWindow.ShowWindow();
        }

        // ===== Utilities =====

        [MenuItem(MENU_ROOT + "Utilities/Validate Framework Data", false, 200)]
        public static void ValidateFrameworkData()
        {
            FrameworkValidation.ValidateAllData();
        }

        [MenuItem(MENU_ROOT + "Utilities/Open Framework Folder", false, 201)]
        public static void OpenFrameworkFolder()
        {
            string frameworkPath = Application.dataPath + "/_Framework";
            EditorUtility.RevealInFinder(frameworkPath);
        }

        [MenuItem(MENU_ROOT + "Utilities/Open Framework Documentation", false, 202)]
        public static void OpenFrameworkDocumentation()
        {
            EditorUtility.DisplayDialog(
                "Framework Documentation",
                "Documentation will be available at:\n\nPackages/com.aizekhan.rpg-framework/README.md\n\n" +
                "Topics:\n" +
                "- Getting Started Guide\n" +
                "- System Guides\n" +
                "- API Reference\n" +
                "- Best Practices\n" +
                "- Example Projects",
                "OK"
            );
        }

        // ===== Quick Create =====

        [MenuItem(MENU_ROOT + "Create/Quest Data", false, 300)]
        public static void CreateQuestData()
        {
            CreateScriptableObjectAsset("QuestData", "New Quest");
        }

        [MenuItem(MENU_ROOT + "Create/Dialogue Data", false, 301)]
        public static void CreateDialogueData()
        {
            CreateScriptableObjectAsset("DialogueData", "New Dialogue");
        }

        [MenuItem(MENU_ROOT + "Create/Faction Data", false, 302)]
        public static void CreateFactionData()
        {
            CreateScriptableObjectAsset("Faction", "New Faction");
        }

        [MenuItem(MENU_ROOT + "Create/Crafting Recipe", false, 303)]
        public static void CreateCraftingRecipe()
        {
            CreateScriptableObjectAsset("CraftingRecipeData", "New Recipe");
        }

        [MenuItem(MENU_ROOT + "Create/Status Effect", false, 304)]
        public static void CreateStatusEffect()
        {
            CreateScriptableObjectAsset("StatusEffectData", "New Status Effect");
        }

        // ===== Help =====

        [MenuItem(MENU_ROOT + "Help/About RPG Framework", false, 400)]
        public static void AboutFramework()
        {
            EditorUtility.DisplayDialog(
                "About RPG Framework",
                "RPG Framework v1.0\n\n" +
                "A universal, reusable framework for building RPG games in Unity.\n\n" +
                "Features:\n" +
                "- Modular architecture\n" +
                "- ECS-based systems\n" +
                "- Comprehensive editor tools\n" +
                "- Highly customizable\n" +
                "- Performance optimized\n\n" +
                "19 Framework modules\n" +
                "14+ Editor tools\n" +
                "10+ Custom editors",
                "OK"
            );
        }

        [MenuItem(MENU_ROOT + "Help/Report Issue", false, 401)]
        public static void ReportIssue()
        {
            EditorUtility.DisplayDialog(
                "Report Issue",
                "To report an issue with the RPG Framework:\n\n" +
                "1. Check existing documentation\n" +
                "2. Review framework logs\n" +
                "3. Submit detailed issue report\n\n" +
                "Include:\n" +
                "- Unity version\n" +
                "- Framework version\n" +
                "- Steps to reproduce\n" +
                "- Expected vs actual behavior",
                "OK"
            );
        }

        // ===== Helper Methods =====

        private static void CreateScriptableObjectAsset(string typeName, string defaultName)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                $"Create {typeName}",
                defaultName,
                "asset",
                $"Enter a name for the new {typeName}"
            );

            if (!string.IsNullOrEmpty(path))
            {
                // Try to find the type in Framework assemblies
                System.Type type = null;

                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.FullName.StartsWith("RPGFramework")) continue;

                    type = assembly.GetType($"RPGFramework.{typeName}") ??
                           assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);

                    if (type != null) break;
                }

                if (type != null && type.IsSubclassOf(typeof(ScriptableObject)))
                {
                    ScriptableObject asset = ScriptableObject.CreateInstance(type);
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();
                    EditorUtility.FocusProjectWindow();
                    Selection.activeObject = asset;

                    EditorUtility.DisplayDialog("Success", $"{typeName} created successfully!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        $"Could not find ScriptableObject type: {typeName}\n\n" +
                        "Please create this asset type manually from the Assets > Create menu.",
                        "OK"
                    );
                }
            }
        }
    }
}
