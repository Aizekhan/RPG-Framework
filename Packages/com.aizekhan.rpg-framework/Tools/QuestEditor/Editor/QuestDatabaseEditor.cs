using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Tools.QuestEditor.Editor
{
    /// <summary>
    /// Custom Inspector for QuestDatabase with management tools.
    /// </summary>
    [CustomEditor(typeof(QuestDatabase))]
    public class QuestDatabaseEditor : UnityEditor.Editor
    {
        private SerializedProperty questsProp;
        private Vector2 scrollPosition;
        private string searchFilter = "";

        private void OnEnable()
        {
            questsProp = serializedObject.FindProperty("quests");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            QuestDatabase database = (QuestDatabase)target;

            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quest Database", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Statistics
            DrawStatistics(database);

            EditorGUILayout.Space(10);

            // Search
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Quest List
            DrawQuestList(database);

            EditorGUILayout.Space(10);

            // Actions
            DrawActions(database);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatistics(QuestDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

            var validQuests = database.GetAllQuests();
            int total = validQuests.Count;
            int repeatable = validQuests.Count(q => q.isRepeatable);
            int hasPrereqs = validQuests.Count(q => q.prerequisiteQuests != null && q.prerequisiteQuests.Length > 0);

            EditorGUILayout.LabelField($"Total Quests: {total}");
            EditorGUILayout.LabelField($"Repeatable: {repeatable}");
            EditorGUILayout.LabelField($"With Prerequisites: {hasPrereqs}");

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestList(QuestDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Quests ({questsProp.arraySize})", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));

            var quests = database.GetAllQuests();
            var filteredQuests = string.IsNullOrWhiteSpace(searchFilter)
                ? quests
                : quests.Where(q =>
                    q.questId.ToLower().Contains(searchFilter.ToLower()) ||
                    q.title.ToLower().Contains(searchFilter.ToLower())).ToList();

            foreach (var quest in filteredQuests)
            {
                DrawQuestEntry(quest, database);
            }

            if (filteredQuests.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(searchFilter)
                        ? "No quests in database"
                        : "No quests match search filter",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawQuestEntry(QuestAsset quest, QuestDatabase database)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Quest info
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(quest.title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {quest.questId}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // Stats
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField($"Objectives: {quest.objectives?.Length ?? 0}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Rewards: {quest.rewards?.Length ?? 0}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // Buttons
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeObject = quest;
                EditorGUIUtility.PingObject(quest);
            }

            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog(
                    "Remove Quest",
                    $"Remove '{quest.title}' from database?\n\nThis will not delete the asset.",
                    "Remove",
                    "Cancel"))
                {
                    Undo.RecordObject(database, "Remove Quest from Database");
                    database.quests.Remove(quest);
                    EditorUtility.SetDirty(database);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions(QuestDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Validate button
            if (GUILayout.Button("Validate Database"))
            {
                var errors = database.ValidateDatabase();
                if (errors.Count == 0)
                {
                    EditorUtility.DisplayDialog("Validation", "No errors found!", "OK");
                }
                else
                {
                    string errorMessage = "Validation errors:\n\n" + string.Join("\n", errors);
                    EditorUtility.DisplayDialog("Validation Errors", errorMessage, "OK");
                    Debug.LogWarning($"Quest Database Validation:\n{errorMessage}");
                }
            }

            // Refresh from project
            if (GUILayout.Button("Refresh from Project"))
            {
                if (EditorUtility.DisplayDialog(
                    "Refresh Database",
                    "This will scan the project for all QuestAsset files and add them to the database. Continue?",
                    "Refresh",
                    "Cancel"))
                {
                    RefreshDatabase(database);
                }
            }

            // Sort quests
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sort by ID"))
            {
                Undo.RecordObject(database, "Sort Quests by ID");
                database.quests = database.quests.OrderBy(q => q != null ? q.questId : "").ToList();
                EditorUtility.SetDirty(database);
            }
            if (GUILayout.Button("Sort by Title"))
            {
                Undo.RecordObject(database, "Sort Quests by Title");
                database.quests = database.quests.OrderBy(q => q != null ? q.title : "").ToList();
                EditorUtility.SetDirty(database);
            }
            EditorGUILayout.EndHorizontal();

            // Remove nulls
            if (GUILayout.Button("Remove Null Entries"))
            {
                int nullCount = database.quests.Count(q => q == null);
                if (nullCount > 0)
                {
                    Undo.RecordObject(database, "Remove Null Quests");
                    database.quests = database.quests.Where(q => q != null).ToList();
                    EditorUtility.SetDirty(database);
                    EditorUtility.DisplayDialog("Cleanup", $"Removed {nullCount} null entries", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Cleanup", "No null entries found", "OK");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void RefreshDatabase(QuestDatabase database)
        {
            string[] guids = AssetDatabase.FindAssets("t:QuestAsset");
            List<QuestAsset> foundQuests = new List<QuestAsset>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                QuestAsset quest = AssetDatabase.LoadAssetAtPath<QuestAsset>(path);
                if (quest != null)
                {
                    foundQuests.Add(quest);
                }
            }

            Undo.RecordObject(database, "Refresh Quest Database");
            database.quests = foundQuests;
            database.UpdateStatistics();
            EditorUtility.SetDirty(database);

            EditorUtility.DisplayDialog(
                "Refresh Complete",
                $"Found {foundQuests.Count} quest(s) in project",
                "OK");
        }
    }
}
