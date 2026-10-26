using System.IO;
using System.Linq;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Tools.LootTableDesigner;
using RPGFramework.Tools.QuestEditor;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.ActionRpg.Editor
{
    /// <summary>
    /// Game Setup inspector: what is wrong with the setup (before pressing Play) and one-click creation of new
    /// enemies, items, loot tables and quests next to the setup asset.
    /// </summary>
    [CustomEditor(typeof(GameSetup))]
    public sealed class GameSetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var setup = (GameSetup)target;

            var problems = setup.Validate();
            if (problems.Count == 0)
                EditorGUILayout.HelpBox("Ready to play. Put an Action Rpg Host in the scene with this setup and press Play.",
                    MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Error);

            EditorGUILayout.Space();
            DrawDefaultInspector();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Create", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Enemy"))
                {
                    var enemy = CreateNext<CharacterDefinition>(setup, "Characters", "Enemy");
                    Undo.RecordObject(setup, "Add Enemy");
                    setup.AddEnemy(enemy);
                    Finish(setup, enemy);
                }
                if (GUILayout.Button("New Item"))
                {
                    var item = CreateNext<ItemAsset>(setup, "Items", "Item", i =>
                    {
                        i.itemId = i.name.ToLowerInvariant().Replace(' ', '_');
                        i.itemName = i.name;
                        i.itemType = RPGFramework.Items.ItemType.Crafting;
                        i.maxStackSize = 99;
                    });
                    Undo.RecordObject(setup, "Add Item");
                    setup.Items.Add(item);
                    Finish(setup, item);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Loot Table"))
                {
                    var table = CreateNext<LootTableAsset>(setup, "Loot", "Loot", t =>
                    {
                        t.tableId = t.name.ToLowerInvariant().Replace(' ', '_');
                        t.tableName = t.name;
                        t.entries.Clear();
                    });
                    Undo.RecordObject(setup, "Add Loot Table");
                    setup.LootTables.Add(table);
                    Finish(setup, table);
                }
                if (GUILayout.Button("New Starting Quest"))
                {
                    var quest = CreateNext<QuestAsset>(setup, "Quests", "Quest", q =>
                    {
                        q.questId = q.name.ToLowerInvariant().Replace(' ', '_');
                        q.title = q.name;
                        var enemy = setup.Enemies.FirstOrDefault(e => e != null);
                        q.objectives = new[]
                        {
                            new QuestObjectiveEntry
                            {
                                objectiveId = "kill", type = RPGFramework.Quests.ObjectiveType.Kill,
                                targetId = enemy != null ? enemy.Id : "enemy_id", requiredCount = 5,
                                description = enemy != null ? $"Slay {enemy.DisplayName}s" : "Slay enemies"
                            }
                        };
                        q.rewards = new[] { new QuestRewardEntry() };
                    });
                    Undo.RecordObject(setup, "Add Quest");
                    setup.StartingQuests.Add(quest);
                    Finish(setup, quest);
                }
            }
        }

        private static T CreateNext<T>(GameSetup setup, string folder, string baseName, System.Action<T> init = null)
            where T : ScriptableObject
        {
            string root = Path.GetDirectoryName(AssetDatabase.GetAssetPath(setup))?.Replace('\\', '/') ?? "Assets";
            string directory = $"{root}/{folder}";
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder(root, folder);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{baseName}.asset");
            var asset = CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Finish(GameSetup setup, Object created)
        {
            EditorUtility.SetDirty(setup);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(created);
            Selection.activeObject = created;
        }
    }
}
