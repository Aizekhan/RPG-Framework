using UnityEngine;
using UnityEditor;
using RPGFramework.Tools.LootTableDesigner;
using System.Linq;

namespace RPGFramework.Tools.LootTableDesigner.Editor
{
    /// <summary>
    /// Custom Inspector for LootTableAsset with drop rate visualization.
    /// </summary>
    [CustomEditor(typeof(LootTableAsset))]
    public class LootTableAssetEditor : UnityEditor.Editor
    {
        private bool showEntries = true;
        private bool showGuaranteedDrops = true;
        private bool showValidation = true;
        private bool showStatistics = true;

        public override void OnInspectorGUI()
        {
            var lootTable = (LootTableAsset)target;
            serializedObject.Update();

            // Header
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Loot Table Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Table Identity
            EditorGUILayout.LabelField("Table Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tableId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tableName"));

            EditorGUILayout.Space(10);

            // Drop Configuration
            EditorGUILayout.LabelField("Drop Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minDrops"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxDrops"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowDuplicates"));

            EditorGUILayout.Space(10);

            // Statistics
            showStatistics = EditorGUILayout.Foldout(showStatistics, "Statistics", true);
            if (showStatistics)
            {
                EditorGUI.indentLevel++;
                DrawStatistics(lootTable);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Loot Entries
            showEntries = EditorGUILayout.Foldout(showEntries, $"Loot Entries ({lootTable.entries.Count})", true);
            if (showEntries)
            {
                EditorGUI.indentLevel++;
                DrawLootEntries(lootTable);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Guaranteed Drops
            showGuaranteedDrops = EditorGUILayout.Foldout(showGuaranteedDrops, $"Guaranteed Drops ({lootTable.guaranteedDrops.Count})", true);
            if (showGuaranteedDrops)
            {
                EditorGUI.indentLevel++;
                DrawGuaranteedDrops(lootTable);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Validation
            showValidation = EditorGUILayout.Foldout(showValidation, "Validation", true);
            if (showValidation)
            {
                EditorGUI.indentLevel++;
                DrawValidation(lootTable);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Action Buttons
            DrawActions(lootTable);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatistics(LootTableAsset lootTable)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var totalWeight = lootTable.GetTotalWeight();
            EditorGUILayout.LabelField($"Total Weight: {totalWeight}");
            EditorGUILayout.LabelField($"Total Entries: {lootTable.entries.Count}");
            EditorGUILayout.LabelField($"Guaranteed Drops: {lootTable.guaranteedDrops.Count}");

            var validEntries = lootTable.entries.Count(e => e != null && !string.IsNullOrWhiteSpace(e.itemId));
            EditorGUILayout.LabelField($"Valid Entries: {validEntries}");

            EditorGUILayout.EndVertical();
        }

        private void DrawLootEntries(LootTableAsset lootTable)
        {
            var entriesProp = serializedObject.FindProperty("entries");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < lootTable.entries.Count; i++)
            {
                var entry = lootTable.entries[i];
                if (entry == null) continue;

                EditorGUILayout.BeginVertical("box");

                // Entry header with delete button
                EditorGUILayout.BeginHorizontal();
                var dropRate = lootTable.GetDropRatePercentage(entry);
                EditorGUILayout.LabelField($"Entry {i + 1}: {entry.itemId}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{dropRate:F2}%", GUILayout.Width(60));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    Undo.RecordObject(lootTable, "Remove Loot Entry");
                    lootTable.entries.RemoveAt(i);
                    EditorUtility.SetDirty(lootTable);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                // Draw entry fields
                var entryProp = entriesProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("itemId"));
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("weight"));
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("dropChance"));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Quantity", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("minQuantity"));
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("maxQuantity"));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Level Requirements", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("minLevel"));
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("maxLevel"));

                // Drop rate visualization
                DrawDropRateBar(dropRate, entry.dropChance);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Add Entry Button
            if (GUILayout.Button("+ Add Entry"))
            {
                Undo.RecordObject(lootTable, "Add Loot Entry");
                lootTable.entries.Add(new LootEntryData());
                EditorUtility.SetDirty(lootTable);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDropRateBar(float weightPercentage, float dropChance)
        {
            var finalChance = weightPercentage * (dropChance / 100f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Final Drop Chance: {finalChance:F2}%", GUILayout.Width(150));

            var rect = GUILayoutUtility.GetRect(100, 18);
            EditorGUI.ProgressBar(rect, finalChance / 100f, "");

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGuaranteedDrops(LootTableAsset lootTable)
        {
            var guaranteedProp = serializedObject.FindProperty("guaranteedDrops");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < lootTable.guaranteedDrops.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var itemIdProp = guaranteedProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(itemIdProp, GUIContent.none);

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    Undo.RecordObject(lootTable, "Remove Guaranteed Drop");
                    lootTable.guaranteedDrops.RemoveAt(i);
                    EditorUtility.SetDirty(lootTable);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Guaranteed Drop"))
            {
                Undo.RecordObject(lootTable, "Add Guaranteed Drop");
                lootTable.guaranteedDrops.Add("item_id");
                EditorUtility.SetDirty(lootTable);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidation(LootTableAsset lootTable)
        {
            var errors = lootTable.Validate();

            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("✓ Loot table is valid", MessageType.Info);
            }
            else
            {
                foreach (var error in errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }
        }

        private void DrawActions(LootTableAsset lootTable)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Table ID"))
            {
                Undo.RecordObject(lootTable, "Generate Table ID");
                lootTable.tableId = "loot_" + lootTable.tableName.ToLower().Replace(" ", "_");
                EditorUtility.SetDirty(lootTable);
            }

            if (GUILayout.Button("Sort Entries by Weight"))
            {
                Undo.RecordObject(lootTable, "Sort Entries");
                lootTable.entries = lootTable.entries.OrderByDescending(e => e.weight).ToList();
                EditorUtility.SetDirty(lootTable);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Preview ILootTable"))
            {
                var lootTableInterface = lootTable.ToLootTable();
                Debug.Log($"Loot Table Preview:\n" +
                          $"  ID: {lootTableInterface.TableId}\n" +
                          $"  Name: {lootTableInterface.Name}\n" +
                          $"  Entries: {lootTableInterface.Entries.Count}\n" +
                          $"  Drops: {lootTableInterface.MinDrops}-{lootTableInterface.MaxDrops}\n" +
                          $"  Guaranteed: {lootTableInterface.GuaranteedDrops.Count}");
            }

            if (GUILayout.Button("Test Roll"))
            {
                var lootTableInterface = lootTable.ToLootTable();
                var generator = new LootGenerator();
                var result = generator.GenerateLoot(lootTableInterface, 1);

                Debug.Log($"Loot Roll Result ({result.Drops.Count} items):\n" +
                          string.Join("\n", result.Drops.Select(d => $"  - {d.ItemId} x{d.Quantity}")));
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
