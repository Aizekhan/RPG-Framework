using UnityEngine;
using UnityEditor;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Items;
using System.Linq;

namespace RPGFramework.Tools.ItemDatabaseManager.Editor
{
    [CustomEditor(typeof(ItemDatabase))]
    public class ItemDatabaseEditor : UnityEditor.Editor
    {
        private string searchFilter = "";
        private ItemType filterType = ItemType.Equipment;
        private bool useTypeFilter = false;
        private Vector2 scrollPosition;

        public override void OnInspectorGUI()
        {
            var database = (ItemDatabase)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Database Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Statistics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Database Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(database.GetStatisticsSummary(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Filters
            EditorGUILayout.LabelField("Search & Filter", EditorStyles.boldLabel);
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            EditorGUILayout.BeginHorizontal();
            useTypeFilter = EditorGUILayout.Toggle("Filter by Type", useTypeFilter, GUILayout.Width(120));
            if (useTypeFilter)
            {
                filterType = (ItemType)EditorGUILayout.EnumPopup(filterType);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Items List
            DrawItemsList(database);

            EditorGUILayout.Space(10);

            // Actions
            DrawActions(database);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawItemsList(ItemDatabase database)
        {
            var filteredItems = database.items
                .Where(i => i != null)
                .Where(i => !useTypeFilter || i.itemType == filterType)
                .Where(i => string.IsNullOrWhiteSpace(searchFilter) ||
                           i.itemId.Contains(searchFilter.ToLower()) ||
                           i.itemName.ToLower().Contains(searchFilter.ToLower()))
                .ToList();

            EditorGUILayout.LabelField($"Items ({filteredItems.Count})", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));

            foreach (var item in filteredItems)
            {
                EditorGUILayout.BeginHorizontal("box");

                // Item info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(item.itemName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"ID: {item.itemId} | Type: {item.itemType} | Rarity: {item.rarity}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // Select button
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = item;
                    EditorGUIUtility.PingObject(item);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawActions(ItemDatabase database)
        {
            EditorGUILayout.LabelField("Database Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Validate All"))
            {
                var errors = database.ValidateDatabase();
                if (errors.Count == 0)
                {
                    EditorUtility.DisplayDialog("Validation Success", "All items are valid!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation Failed", string.Join("\n", errors), "OK");
                }
            }

            if (GUILayout.Button("Refresh Database"))
            {
                Undo.RecordObject(database, "Refresh Item Database");
                var guids = AssetDatabase.FindAssets("t:ItemAsset");
                database.items = guids
                    .Select(guid => AssetDatabase.LoadAssetAtPath<ItemAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(asset => asset != null)
                    .ToList();
                database.UpdateStatistics();
                EditorUtility.SetDirty(database);
                EditorUtility.DisplayDialog("Refresh Complete", $"Found {database.items.Count} item(s)", "OK");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Sort by ID"))
            {
                Undo.RecordObject(database, "Sort by ID");
                database.SortByItemId();
                database.UpdateStatistics();
                EditorUtility.SetDirty(database);
            }

            if (GUILayout.Button("Sort by Type & Rarity"))
            {
                Undo.RecordObject(database, "Sort by Type & Rarity");
                database.SortByTypeAndRarity();
                database.UpdateStatistics();
                EditorUtility.SetDirty(database);
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Export to IItemRegistry", GUILayout.Height(30)))
            {
                var registry = database.ToItemRegistry();
                Debug.Log($"Exported {database.items.Count} items to ItemRegistry");
            }
        }
    }
}
