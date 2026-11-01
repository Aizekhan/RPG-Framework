using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Dialogue;
using RPGFramework.Quests;

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

        /// <summary>Errors found by the last validation.</summary>
        public static int ErrorCount => errors.Count;

        /// <summary>Warnings found by the last validation.</summary>
        public static int WarningCount => warnings.Count;

        /// <summary>
        /// <see cref="ValidateAllData"/> for batch mode: exit code 0 without errors, 1 with errors (2 when the
        /// validation itself failed). Warnings do not fail it.
        /// <c>Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.Editor.Utilities.FrameworkValidation.ValidateFromCommandLine</c>
        /// </summary>
        public static void ValidateFromCommandLine()
        {
            try
            {
                ValidateAllData();
                Debug.Log($"[Validation] {errors.Count} error(s), {warnings.Count} warning(s)");
                EditorApplication.Exit(errors.Count > 0 ? 1 : 0);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Validation] Failed: " + exception);
                EditorApplication.Exit(2);
            }
        }

        // Quest assets: each quest's stage graph and requirements, prerequisites across all quests
        private static void ValidateQuests()
        {
            var quests = LoadAll<RPGFramework.Tools.QuestEditor.QuestAsset>();
            infos.Add($"Found {quests.Count} quest(s) to validate");
            foreach (var issue in QuestGraph.Check(quests.Select(q => (IQuest)new Quest(q.ToQuestData()))))
            {
                var message = $"Quest {issue}";
                if (issue.Level == QuestIssueLevel.Error)
                    errors.Add(message);
                else
                    warnings.Add(message);
            }
            foreach (var quest in quests.Where(q => string.IsNullOrEmpty(q.questId)))
                errors.Add($"Quest '{quest.name}' has empty Quest ID");
        }

        // Dialogue assets: ids and links (DialogueAsset.Validate), then dead ends, loops, empty lines, speakers and the
        // quest and stage each one belongs to
        private static void ValidateDialogues()
        {
            var dialogues = LoadAll<RPGFramework.Tools.DialogueEditor.DialogueAsset>();
            infos.Add($"Found {dialogues.Count} dialogue(s) to validate");
            var quests = LoadAll<RPGFramework.Tools.QuestEditor.QuestAsset>()
                .GroupBy(q => q.questId).ToDictionary(g => g.Key ?? "", g => g.First().ToQuestData());
            var playable = new List<IDialogue>();
            foreach (var dialogue in dialogues)
            {
                var problems = dialogue.Validate();
                foreach (var problem in problems)
                    errors.Add($"Dialogue '{dialogue.name}': {problem}");
                if (problems.Count == 0)
                    playable.Add(dialogue.ToDialogue());
            }
            foreach (var issue in DialogueGraph.Check(playable, quests.ContainsKey,
                         (quest, stage) => quests.TryGetValue(quest, out var data) && (data.Stages ?? new QuestStageData[0]).Any(s => s.Id == stage)))
            {
                var message = $"Dialogue {issue}";
                if (issue.Level == DialogueIssueLevel.Error)
                    errors.Add(message);
                else if (issue.Code != "unreachable" && issue.Code != "badLink" && issue.Code != "badStart")
                    warnings.Add(message);
            }
        }

        private static List<T> LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToList();

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
