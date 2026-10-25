using UnityEngine;
using UnityEditor;
using RPGFramework.Tools.LootTableDesigner;
using System.Linq;

namespace RPGFramework.Tools.LootTableDesigner.Editor
{
    /// <summary>
    /// Custom Inspector for LootTableDatabase with management tools.
    /// </summary>
    [CustomEditor(typeof(LootTableDatabase))]
    public class LootTableDatabaseEditor : UnityEditor.Editor
    {
        private string searchFilter = "";
        private Vector2 scrollPosition;

        public override void OnInspectorGUI()
        {
            var database = (LootTableDatabase)target;
            serializedObject.Update();

            // Header
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Loot Table Database Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Statistics
            DrawStatistics(database);

            EditorGUILayout.Space(10);

            // Search
            EditorGUILayout.LabelField("Search & Filter", EditorStyles.boldLabel);
            searchFilter = EditorGUILayout.TextField("Search by ID or Name", searchFilter);

            EditorGUILayout.Space(5);

            // Loot Tables List
            DrawLootTablesList(database);

            EditorGUILayout.Space(10);

            // Actions
            DrawActions(database);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatistics(LootTableDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Database Statistics", EditorStyles.boldLabel);

            EditorGUILayout.LabelField(database.GetStatisticsSummary());

            EditorGUILayout.EndVertical();
        }

        private void DrawLootTablesList(LootTableDatabase database)
        {
            EditorGUILayout.LabelField($"Loot Tables ({database.lootTables.Count})", EditorStyles.boldLabel);

            var filteredTables = database.lootTables
                .Where(t => t != null && (string.IsNullOrWhiteSpace(searchFilter) ||
                                          t.tableId.Contains(searchFilter.ToLower()) ||
                                          t.tableName.ToLower().Contains(searchFilter.ToLower())))
                .ToList();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

            foreach (var table in filteredTables)
            {
                if (table == null) continue;

                EditorGUILayout.BeginHorizontal("box");

                // Table info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(table.tableName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"ID: {table.tableId}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Entries: {table.entries.Count} | Drops: {table.minDrops}-{table.maxDrops}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // Select button
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = table;
                    EditorGUIUtility.PingObject(table);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (filteredTables.Count == 0)
            {
                EditorGUILayout.HelpBox("No loot tables found matching search filter", MessageType.Info);
            }
        }

        private void DrawActions(LootTableDatabase database)
        {
            EditorGUILayout.LabelField("Database Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Validate All"))
            {
                var errors = database.ValidateDatabase();
                if (errors.Count == 0)
                {
                    EditorUtility.DisplayDialog("Validation Success", "All loot tables are valid!", "OK");
                }
                else
                {
                    var errorMessage = string.Join("\n", errors);
                    EditorUtility.DisplayDialog("Validation Failed", errorMessage, "OK");
                    Debug.LogError($"Loot Table Database Validation Errors:\n{errorMessage}");
                }
            }

            if (GUILayout.Button("Refresh Database"))
            {
                RefreshDatabase(database);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Sort by ID"))
            {
                Undo.RecordObject(database, "Sort Loot Tables by ID");
                database.SortByTableId();
                database.UpdateStatistics();
                EditorUtility.SetDirty(database);
            }

            if (GUILayout.Button("Sort by Name"))
            {
                Undo.RecordObject(database, "Sort Loot Tables by Name");
                database.SortByTableName();
                database.UpdateStatistics();
                EditorUtility.SetDirty(database);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Remove Null Entries"))
            {
                Undo.RecordObject(database, "Remove Null Entries");
                var beforeCount = database.lootTables.Count;
                database.RemoveNullEntries();
                var afterCount = database.lootTables.Count;
                database.UpdateStatistics();
                EditorUtility.SetDirty(database);

                EditorUtility.DisplayDialog("Cleanup Complete",
                    $"Removed {beforeCount - afterCount} null entries",
                    "OK");
            }

            if (GUILayout.Button("Update Statistics"))
            {
                database.UpdateStatistics();
                EditorUtility.SetDirty(database);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Export to ILootTableRegistry", GUILayout.Height(30)))
            {
                var registry = database.ToLootTableRegistry();
                Debug.Log($"Exported {database.lootTables.Count} loot tables to LootTableRegistry");

                var allTableIds = string.Join(", ", database.lootTables.Select(t => t.tableId));
                Debug.Log($"Table IDs: {allTableIds}");
            }
        }

        private void RefreshDatabase(LootTableDatabase database)
        {
            Undo.RecordObject(database, "Refresh Loot Table Database");

            // Find all LootTableAsset instances in project
            var guids = AssetDatabase.FindAssets("t:LootTableAsset");
            var lootTables = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<LootTableAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToList();

            database.lootTables = lootTables;
            database.UpdateStatistics();
            EditorUtility.SetDirty(database);

            EditorUtility.DisplayDialog("Refresh Complete",
                $"Found and registered {lootTables.Count} loot table(s)",
                "OK");
        }
    }
}
