using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace RPGFramework.Editor.EditorWindows
{
    /// <summary>
    /// Loot table editor for managing loot drops and probabilities.
    /// </summary>
    public class LootTableEditorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private Vector2 lootListScroll;
        private List<Object> allLootTables = new List<Object>();
        private Object selectedLootTable;
        private string searchFilter = "";

        [MenuItem("Tools/RPG Framework/Loot Table Editor", false, 5)]
        public static void ShowWindow()
        {
            LootTableEditorWindow window = GetWindow<LootTableEditorWindow>("Loot Table Editor");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshLootTableList();
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginHorizontal();

            // Left panel - Loot table list
            DrawLootTableListPanel();

            // Right panel - Loot table editor
            DrawLootTableEditorPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Loot Table Editor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Loot Tables: {allLootTables.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawLootTableListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            EditorGUILayout.LabelField("Loot Tables", EditorStyles.boldLabel);

            // Search
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                searchFilter = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Refresh"))
            {
                RefreshLootTableList();
            }

            EditorGUILayout.Space(5);

            lootListScroll = EditorGUILayout.BeginScrollView(lootListScroll);

            foreach (var lootTable in allLootTables)
            {
                if (lootTable == null) continue;

                // Apply search filter
                if (!string.IsNullOrEmpty(searchFilter) &&
                    !lootTable.name.ToLower().Contains(searchFilter.ToLower()))
                {
                    continue;
                }

                bool isSelected = selectedLootTable == lootTable;
                Color originalBg = GUI.backgroundColor;

                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
                }

                if (GUILayout.Button(lootTable.name, EditorStyles.miniButton))
                {
                    selectedLootTable = lootTable;
                    Selection.activeObject = lootTable;
                }

                GUI.backgroundColor = originalBg;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Create New Loot Table"))
            {
                CreateNewLootTable();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLootTableEditorPanel()
        {
            EditorGUILayout.BeginVertical();

            if (selectedLootTable == null)
            {
                EditorGUILayout.HelpBox("Select a loot table from the list or create a new one.", MessageType.Info);
            }
            else
            {
                DrawLootTableDetails();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLootTableDetails()
        {
            EditorGUILayout.LabelField($"Editing: {selectedLootTable.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            SerializedObject serializedTable = new SerializedObject(selectedLootTable);
            serializedTable.Update();

            // Basic settings
            EditorGUILayout.LabelField("Loot Table Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty tableIdProp = serializedTable.FindProperty("_tableId");
            if (tableIdProp != null)
            {
                EditorGUILayout.PropertyField(tableIdProp, new GUIContent("Table ID"));
            }

            SerializedProperty nameProp = serializedTable.FindProperty("_name");
            if (nameProp != null)
            {
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            }

            SerializedProperty minDropsProp = serializedTable.FindProperty("_minDrops");
            if (minDropsProp != null)
            {
                EditorGUILayout.PropertyField(minDropsProp, new GUIContent("Min Drops"));
            }

            SerializedProperty maxDropsProp = serializedTable.FindProperty("_maxDrops");
            if (maxDropsProp != null)
            {
                EditorGUILayout.PropertyField(maxDropsProp, new GUIContent("Max Drops"));
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Loot entries
            SerializedProperty entriesProp = serializedTable.FindProperty("_entries");
            if (entriesProp != null)
            {
                EditorGUILayout.LabelField($"Loot Entries ({entriesProp.arraySize})", EditorStyles.boldLabel);

                DrawLootEntriesSection(entriesProp);

                EditorGUILayout.Space(10);

                if (entriesProp.arraySize > 0)
                {
                    DrawDropChanceVisualization(entriesProp);
                }
            }

            serializedTable.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Save button
            if (GUILayout.Button("Save Changes", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(selectedLootTable);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success", "Loot table saved successfully!", "OK");
            }
        }

        private void DrawLootEntriesSection(SerializedProperty entriesProp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(entryProp, true);

                if (GUILayout.Button($"Remove Entry {i + 1}", EditorStyles.miniButton))
                {
                    entriesProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("Add Loot Entry"))
            {
                entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDropChanceVisualization(SerializedProperty entriesProp)
        {
            EditorGUILayout.LabelField("Drop Chance Visualization", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float totalWeight = 0f;

            // Calculate total weight
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty weightProp = entryProp.FindPropertyRelative("weight");
                if (weightProp != null)
                {
                    totalWeight += weightProp.floatValue;
                }
            }

            EditorGUILayout.LabelField($"Total Weight: {totalWeight:F2}");
            EditorGUILayout.Space(5);

            // Draw bars for each entry
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty weightProp = entryProp.FindPropertyRelative("weight");

                if (weightProp != null && totalWeight > 0)
                {
                    float weight = weightProp.floatValue;
                    float chance = (weight / totalWeight) * 100f;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Entry {i + 1}:", GUILayout.Width(70));

                    Rect barRect = GUILayoutUtility.GetRect(200, 18);
                    float normalized = weight / totalWeight;

                    // Draw background
                    EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

                    // Draw bar
                    Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * normalized, barRect.height);
                    Color barColor = GetDropChanceColor(chance);
                    EditorGUI.DrawRect(fillRect, barColor);

                    // Draw text
                    GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUI.Label(barRect, $"{chance:F2}%", centeredStyle);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private Color GetDropChanceColor(float chancePercent)
        {
            if (chancePercent >= 50f) return new Color(0.3f, 0.9f, 0.3f);
            if (chancePercent >= 25f) return new Color(0.5f, 0.7f, 0.9f);
            if (chancePercent >= 10f) return new Color(0.7f, 0.4f, 0.9f);
            if (chancePercent >= 5f) return new Color(0.9f, 0.5f, 0.2f);
            return new Color(0.9f, 0.3f, 0.3f);
        }

        private void RefreshLootTableList()
        {
            allLootTables.Clear();
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (obj != null && obj.GetType().Namespace != null &&
                    obj.GetType().Namespace.StartsWith("RPGFramework.Loot"))
                {
                    allLootTables.Add(obj);
                }
            }
        }

        private void CreateNewLootTable()
        {
            EditorUtility.DisplayDialog("Create Loot Table",
                "Loot table creation wizard to be implemented.\n\nFor now, create loot tables manually using:\nAssets > Create > RPG Framework > Loot",
                "OK");
        }
    }
}
