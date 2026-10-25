using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Editor.Utilities
{
    /// <summary>
    /// Validation utilities for RPG Framework data integrity.
    /// Checks for missing references, invalid configurations, and common issues.
    /// </summary>
    public static class FrameworkValidation
    {
        private static List<string> errors = new List<string>();
        private static List<string> warnings = new List<string>();
        private static List<string> infos = new List<string>();

        public static void ValidateAllData()
        {
            errors.Clear();
            warnings.Clear();
            infos.Clear();

            Debug.Log("Starting RPG Framework validation...");

            ValidateQuests();
            ValidateDialogues();
            ValidateFactions();
            ValidateCraftingRecipes();
            ValidateItems();
            ValidateLootTables();

            DisplayValidationResults();
        }

        private static void ValidateQuests()
        {
            string[] guids = AssetDatabase.FindAssets("t:QuestData");
            infos.Add($"Found {guids.Length} quest(s) to validate");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object questObj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (questObj == null)
                {
                    errors.Add($"Failed to load quest at path: {path}");
                    continue;
                }

                SerializedObject so = new SerializedObject(questObj);

                // Validate quest ID
                SerializedProperty questIdProp = so.FindProperty("questId");
                if (questIdProp != null && string.IsNullOrEmpty(questIdProp.stringValue))
                {
                    errors.Add($"Quest '{questObj.name}' has empty Quest ID");
                }

                // Validate title
                SerializedProperty titleProp = so.FindProperty("title");
                if (titleProp != null && string.IsNullOrEmpty(titleProp.stringValue))
                {
                    warnings.Add($"Quest '{questObj.name}' has empty title");
                }

                // Validate objectives
                SerializedProperty objectivesProp = so.FindProperty("objectives");
                if (objectivesProp != null && objectivesProp.arraySize == 0)
                {
                    warnings.Add($"Quest '{questObj.name}' has no objectives");
                }

                // Validate rewards
                SerializedProperty rewardsProp = so.FindProperty("rewards");
                if (rewardsProp != null && rewardsProp.arraySize == 0)
                {
                    infos.Add($"Quest '{questObj.name}' has no rewards (this may be intentional)");
                }
            }
        }

        private static void ValidateDialogues()
        {
            string[] guids = AssetDatabase.FindAssets("t:DialogueData");
            infos.Add($"Found {guids.Length} dialogue(s) to validate");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object dialogueObj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (dialogueObj == null)
                {
                    errors.Add($"Failed to load dialogue at path: {path}");
                    continue;
                }

                SerializedObject so = new SerializedObject(dialogueObj);

                // Validate dialogue ID
                SerializedProperty dialogueIdProp = so.FindProperty("dialogueId");
                if (dialogueIdProp != null && string.IsNullOrEmpty(dialogueIdProp.stringValue))
                {
                    errors.Add($"Dialogue '{dialogueObj.name}' has empty Dialogue ID");
                }

                // Validate start node
                SerializedProperty startNodeProp = so.FindProperty("startNodeId");
                if (startNodeProp != null && string.IsNullOrEmpty(startNodeProp.stringValue))
                {
                    warnings.Add($"Dialogue '{dialogueObj.name}' has no start node ID");
                }

                // Validate nodes
                SerializedProperty nodesProp = so.FindProperty("nodes");
                if (nodesProp != null)
                {
                    if (nodesProp.arraySize == 0)
                    {
                        warnings.Add($"Dialogue '{dialogueObj.name}' has no nodes");
                    }
                    else
                    {
                        // Check for duplicate node IDs
                        HashSet<string> nodeIds = new HashSet<string>();
                        for (int i = 0; i < nodesProp.arraySize; i++)
                        {
                            SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(i);
                            SerializedProperty nodeIdProp = nodeProp.FindPropertyRelative("id");

                            if (nodeIdProp != null)
                            {
                                string nodeId = nodeIdProp.stringValue;
                                if (string.IsNullOrEmpty(nodeId))
                                {
                                    errors.Add($"Dialogue '{dialogueObj.name}' has node with empty ID at index {i}");
                                }
                                else if (nodeIds.Contains(nodeId))
                                {
                                    errors.Add($"Dialogue '{dialogueObj.name}' has duplicate node ID: {nodeId}");
                                }
                                else
                                {
                                    nodeIds.Add(nodeId);
                                }
                            }
                        }

                        // Validate start node exists
                        if (startNodeProp != null && !string.IsNullOrEmpty(startNodeProp.stringValue))
                        {
                            if (!nodeIds.Contains(startNodeProp.stringValue))
                            {
                                errors.Add($"Dialogue '{dialogueObj.name}' start node '{startNodeProp.stringValue}' not found in nodes");
                            }
                        }
                    }
                }
            }
        }

        private static void ValidateFactions()
        {
            string[] guids = AssetDatabase.FindAssets("t:Faction");
            infos.Add($"Found {guids.Length} faction(s) to validate");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object factionObj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (factionObj == null)
                {
                    errors.Add($"Failed to load faction at path: {path}");
                    continue;
                }

                SerializedObject so = new SerializedObject(factionObj);

                // Validate faction ID
                SerializedProperty factionIdProp = so.FindProperty("_factionId");
                if (factionIdProp != null && string.IsNullOrEmpty(factionIdProp.stringValue))
                {
                    errors.Add($"Faction '{factionObj.name}' has empty Faction ID");
                }

                // Validate name
                SerializedProperty nameProp = so.FindProperty("_name");
                if (nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
                {
                    warnings.Add($"Faction '{factionObj.name}' has empty name");
                }
            }
        }

        private static void ValidateCraftingRecipes()
        {
            string[] guids = AssetDatabase.FindAssets("t:CraftingRecipeData");
            infos.Add($"Found {guids.Length} crafting recipe(s) to validate");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object recipeObj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (recipeObj == null)
                {
                    errors.Add($"Failed to load recipe at path: {path}");
                    continue;
                }

                SerializedObject so = new SerializedObject(recipeObj);

                // Validate recipe ID
                SerializedProperty recipeIdProp = so.FindProperty("recipeId");
                if (recipeIdProp != null && string.IsNullOrEmpty(recipeIdProp.stringValue))
                {
                    errors.Add($"Recipe '{recipeObj.name}' has empty Recipe ID");
                }

                // Validate ingredients
                SerializedProperty ingredientsProp = so.FindProperty("ingredients");
                if (ingredientsProp != null && ingredientsProp.arraySize == 0)
                {
                    warnings.Add($"Recipe '{recipeObj.name}' has no ingredients");
                }

                // Validate results
                SerializedProperty resultsProp = so.FindProperty("results");
                if (resultsProp != null && resultsProp.arraySize == 0)
                {
                    errors.Add($"Recipe '{recipeObj.name}' has no results - recipe produces nothing!");
                }
            }
        }

        private static void ValidateItems()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            int itemCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (obj != null && obj.GetType().Namespace != null &&
                    obj.GetType().Namespace.StartsWith("RPGFramework.Items"))
                {
                    itemCount++;

                    SerializedObject so = new SerializedObject(obj);

                    // Validate item ID
                    SerializedProperty itemIdProp = so.FindProperty("_itemId");
                    if (itemIdProp != null && string.IsNullOrEmpty(itemIdProp.stringValue))
                    {
                        errors.Add($"Item '{obj.name}' has empty Item ID");
                    }

                    // Validate max stack size
                    SerializedProperty maxStackProp = so.FindProperty("_maxStackSize");
                    if (maxStackProp != null && maxStackProp.intValue <= 0)
                    {
                        errors.Add($"Item '{obj.name}' has invalid max stack size: {maxStackProp.intValue}");
                    }
                }
            }

            infos.Add($"Found {itemCount} item(s) to validate");
        }

        private static void ValidateLootTables()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            int lootTableCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (obj != null && obj.GetType().Namespace != null &&
                    obj.GetType().Namespace.StartsWith("RPGFramework.Loot"))
                {
                    lootTableCount++;

                    SerializedObject so = new SerializedObject(obj);

                    // Validate table ID
                    SerializedProperty tableIdProp = so.FindProperty("_tableId");
                    if (tableIdProp != null && string.IsNullOrEmpty(tableIdProp.stringValue))
                    {
                        errors.Add($"Loot table '{obj.name}' has empty Table ID");
                    }

                    // Validate min/max drops
                    SerializedProperty minDropsProp = so.FindProperty("_minDrops");
                    SerializedProperty maxDropsProp = so.FindProperty("_maxDrops");

                    if (minDropsProp != null && maxDropsProp != null)
                    {
                        if (minDropsProp.intValue > maxDropsProp.intValue)
                        {
                            errors.Add($"Loot table '{obj.name}' has min drops > max drops");
                        }
                    }

                    // Validate entries
                    SerializedProperty entriesProp = so.FindProperty("_entries");
                    if (entriesProp != null && entriesProp.arraySize == 0)
                    {
                        warnings.Add($"Loot table '{obj.name}' has no entries");
                    }
                }
            }

            infos.Add($"Found {lootTableCount} loot table(s) to validate");
        }

        private static void DisplayValidationResults()
        {
            Debug.Log("=== RPG Framework Validation Results ===");

            // Display errors
            if (errors.Count > 0)
            {
                Debug.LogError($"Found {errors.Count} error(s):");
                foreach (var error in errors)
                {
                    Debug.LogError($"  - {error}");
                }
            }

            // Display warnings
            if (warnings.Count > 0)
            {
                Debug.LogWarning($"Found {warnings.Count} warning(s):");
                foreach (var warning in warnings)
                {
                    Debug.LogWarning($"  - {warning}");
                }
            }

            // Display info
            foreach (var info in infos)
            {
                Debug.Log($"  {info}");
            }

            // Summary
            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log("Validation completed successfully! No issues found.");

                EditorUtility.DisplayDialog(
                    "Validation Complete",
                    "Framework validation completed successfully!\n\nNo errors or warnings found.",
                    "OK"
                );
            }
            else
            {
                string message = $"Validation completed with:\n\n" +
                                $"Errors: {errors.Count}\n" +
                                $"Warnings: {warnings.Count}\n\n" +
                                "Check Console for details.";

                EditorUtility.DisplayDialog(
                    "Validation Complete",
                    message,
                    "OK"
                );
            }

            Debug.Log("=== End Validation Results ===");
        }
    }
}
