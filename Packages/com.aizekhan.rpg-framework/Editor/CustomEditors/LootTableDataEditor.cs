using UnityEditor;
using UnityEngine;
using RPGFramework.Loot;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for LootTable data with drop chance visualization.
    /// </summary>
    public class LootTableDataEditor : UnityEditor.Editor
    {
        private bool showEntries = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Loot Table Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Table ID
            SerializedProperty tableIdProp = serializedObject.FindProperty("_tableId");
            if (tableIdProp != null)
            {
                EditorGUILayout.PropertyField(tableIdProp, new GUIContent("Table ID"));

                if (string.IsNullOrEmpty(tableIdProp.stringValue))
                {
                    EditorGUILayout.HelpBox("Table ID is required. Please enter a unique identifier.", MessageType.Error);
                }
            }

            EditorGUILayout.Space(5);

            // Name
            SerializedProperty nameProp = serializedObject.FindProperty("_name");
            if (nameProp != null)
            {
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            }

            EditorGUILayout.Space(10);

            // Loot Settings
            EditorGUILayout.LabelField("Loot Settings", EditorStyles.boldLabel);

            SerializedProperty minDropsProp = serializedObject.FindProperty("_minDrops");
            if (minDropsProp != null)
            {
                EditorGUILayout.PropertyField(minDropsProp, new GUIContent("Min Drops"));
            }

            SerializedProperty maxDropsProp = serializedObject.FindProperty("_maxDrops");
            if (maxDropsProp != null)
            {
                EditorGUILayout.PropertyField(maxDropsProp, new GUIContent("Max Drops"));
            }

            if (minDropsProp != null && maxDropsProp != null)
            {
                if (minDropsProp.intValue > maxDropsProp.intValue)
                {
                    EditorGUILayout.HelpBox("Min Drops cannot be greater than Max Drops.", MessageType.Error);
                }
            }

            EditorGUILayout.Space(10);

            // Loot Entries
            SerializedProperty entriesProp = serializedObject.FindProperty("_entries");
            if (entriesProp != null)
            {
                showEntries = EditorGUILayout.Foldout(showEntries, $"Loot Entries ({entriesProp.arraySize})", true);
                if (showEntries)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(entriesProp, true);

                    if (entriesProp.arraySize == 0)
                    {
                        EditorGUILayout.HelpBox("No loot entries defined. Add items to this loot table.", MessageType.Warning);
                    }
                    else
                    {
                        DrawLootSummary(entriesProp);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLootSummary(SerializedProperty entriesProp)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Drop Chance Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float totalWeight = 0f;
            int entryCount = entriesProp.arraySize;

            // Calculate total weight
            for (int i = 0; i < entryCount; i++)
            {
                SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty weightProp = entryProp.FindPropertyRelative("weight");
                if (weightProp != null)
                {
                    totalWeight += weightProp.floatValue;
                }
            }

            EditorGUILayout.LabelField($"Total Entries: {entryCount}");
            EditorGUILayout.LabelField($"Total Weight: {totalWeight:F2}");

            // Show top 5 drops
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Preview (first 3 entries):");

            int previewCount = Mathf.Min(3, entryCount);
            for (int i = 0; i < previewCount; i++)
            {
                SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty weightProp = entryProp.FindPropertyRelative("weight");

                if (weightProp != null && totalWeight > 0)
                {
                    float chance = (weightProp.floatValue / totalWeight) * 100f;
                    EditorGUILayout.LabelField($"  Entry {i + 1}: {chance:F2}% drop chance");
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
