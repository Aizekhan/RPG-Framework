// TODO: This Editor window needs refactoring to work with QuestData struct instead of ScriptableObject
#if FALSE

using UnityEditor;
using UnityEngine;
using RPGFramework.Quests;
using System.Collections.Generic;

namespace RPGFramework.Editor.EditorWindows
{
    /// <summary>
    /// Visual quest editor for creating and editing quests.
    /// </summary>
    public class QuestEditorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private QuestData selectedQuest;
        private List<QuestData> availableQuests = new List<QuestData>();
        private string searchFilter = "";

        [MenuItem("RPG Framework/Tools/Quest Editor", false, 1)]
        public static void ShowWindow()
        {
            QuestEditorWindow window = GetWindow<QuestEditorWindow>("Quest Editor");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshQuestList();
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginHorizontal();

            // Left panel - Quest list
            DrawQuestListPanel();

            // Right panel - Quest editor
            DrawQuestEditorPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quest Editor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Create and edit quests visually", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawQuestListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            EditorGUILayout.LabelField("Available Quests", EditorStyles.boldLabel);

            // Search filter
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchFilter = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Refresh button
            if (GUILayout.Button("Refresh Quest List"))
            {
                RefreshQuestList();
            }

            EditorGUILayout.Space(5);

            // Quest list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var quest in availableQuests)
            {
                if (quest == null) continue;

                // Apply search filter
                if (!string.IsNullOrEmpty(searchFilter) &&
                    !quest.name.ToLower().Contains(searchFilter.ToLower()))
                {
                    continue;
                }

                bool isSelected = selectedQuest == quest;
                Color originalBg = GUI.backgroundColor;

                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
                }

                if (GUILayout.Button(quest.name, EditorStyles.miniButton))
                {
                    selectedQuest = quest;
                }

                GUI.backgroundColor = originalBg;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Create new quest button
            if (GUILayout.Button("Create New Quest"))
            {
                CreateNewQuest();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestEditorPanel()
        {
            EditorGUILayout.BeginVertical();

            if (selectedQuest == null)
            {
                EditorGUILayout.HelpBox("Select a quest from the list or create a new one.", MessageType.Info);
            }
            else
            {
                DrawQuestDetails();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestDetails()
        {
            EditorGUILayout.LabelField($"Editing: {selectedQuest.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            SerializedObject serializedQuest = new SerializedObject(selectedQuest);
            serializedQuest.Update();

            // Basic Info
            EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty questIdProp = serializedQuest.FindProperty("questId");
            EditorGUILayout.PropertyField(questIdProp);

            SerializedProperty titleProp = serializedQuest.FindProperty("title");
            EditorGUILayout.PropertyField(titleProp);

            SerializedProperty descriptionProp = serializedQuest.FindProperty("description");
            EditorGUILayout.LabelField("Description");
            descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, GUILayout.Height(80));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Requirements
            EditorGUILayout.LabelField("Requirements", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty minLevelProp = serializedQuest.FindProperty("minimumLevel");
            EditorGUILayout.PropertyField(minLevelProp);

            SerializedProperty repeatableProp = serializedQuest.FindProperty("isRepeatable");
            EditorGUILayout.PropertyField(repeatableProp);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Objectives
            DrawObjectivesSection(serializedQuest);

            EditorGUILayout.Space(10);

            // Rewards
            DrawRewardsSection(serializedQuest);

            serializedQuest.ApplyModifiedProperties();

            EditorGUILayout.Space(10);

            // Save button
            if (GUILayout.Button("Save Quest", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(selectedQuest);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success", "Quest saved successfully!", "OK");
            }
        }

        private void DrawObjectivesSection(SerializedObject serializedQuest)
        {
            SerializedProperty objectivesProp = serializedQuest.FindProperty("objectives");

            EditorGUILayout.LabelField($"Objectives ({objectivesProp.arraySize})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < objectivesProp.arraySize; i++)
            {
                SerializedProperty objectiveProp = objectivesProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(objectiveProp, new GUIContent($"Objective {i + 1}"), true);

                if (GUILayout.Button($"Remove Objective {i + 1}", EditorStyles.miniButton))
                {
                    objectivesProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("Add Objective"))
            {
                objectivesProp.InsertArrayElementAtIndex(objectivesProp.arraySize);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRewardsSection(SerializedObject serializedQuest)
        {
            SerializedProperty rewardsProp = serializedQuest.FindProperty("rewards");

            EditorGUILayout.LabelField($"Rewards ({rewardsProp.arraySize})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < rewardsProp.arraySize; i++)
            {
                SerializedProperty rewardProp = rewardsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(rewardProp, new GUIContent($"Reward {i + 1}"), true);

                if (GUILayout.Button($"Remove Reward {i + 1}", EditorStyles.miniButton))
                {
                    rewardsProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("Add Reward"))
            {
                rewardsProp.InsertArrayElementAtIndex(rewardsProp.arraySize);
            }

            EditorGUILayout.EndVertical();
        }

        private void RefreshQuestList()
        {
            availableQuests.Clear();
            string[] guids = AssetDatabase.FindAssets("t:QuestData");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (quest != null)
                {
                    availableQuests.Add(quest);
                }
            }
        }

        private void CreateNewQuest()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Quest",
                "NewQuest",
                "asset",
                "Enter a name for the new quest"
            );

            if (!string.IsNullOrEmpty(path))
            {
                QuestData newQuest = ScriptableObject.CreateInstance<QuestData>();
                AssetDatabase.CreateAsset(newQuest, path);
                AssetDatabase.SaveAssets();

                RefreshQuestList();
                selectedQuest = newQuest;

                EditorUtility.DisplayDialog("Success", "Quest created successfully!", "OK");
            }
        }
    }
}

#endif
