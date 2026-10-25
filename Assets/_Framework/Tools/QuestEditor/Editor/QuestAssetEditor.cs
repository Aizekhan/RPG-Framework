using UnityEngine;
using UnityEditor;
using RPGFramework.Quests;

namespace RPGFramework.Tools.QuestEditor.Editor
{
    /// <summary>
    /// Custom Inspector for QuestAsset with enhanced UI and validation.
    /// </summary>
    [CustomEditor(typeof(QuestAsset))]
    public class QuestAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty questIdProp;
        private SerializedProperty titleProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty prerequisiteQuestsProp;
        private SerializedProperty minimumLevelProp;
        private SerializedProperty isRepeatableProp;
        private SerializedProperty objectivesProp;
        private SerializedProperty rewardsProp;

        private bool showObjectives = true;
        private bool showRewards = true;
        private bool showRequirements = true;
        private bool showValidation = true;

        private void OnEnable()
        {
            questIdProp = serializedObject.FindProperty("questId");
            titleProp = serializedObject.FindProperty("title");
            descriptionProp = serializedObject.FindProperty("description");
            prerequisiteQuestsProp = serializedObject.FindProperty("prerequisiteQuests");
            minimumLevelProp = serializedObject.FindProperty("minimumLevel");
            isRepeatableProp = serializedObject.FindProperty("isRepeatable");
            objectivesProp = serializedObject.FindProperty("objectives");
            rewardsProp = serializedObject.FindProperty("rewards");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            QuestAsset questAsset = (QuestAsset)target;

            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quest Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Quest Identity Section
            DrawSection("Quest Identity", () =>
            {
                EditorGUILayout.PropertyField(questIdProp, new GUIContent("Quest ID"));
                EditorGUILayout.PropertyField(titleProp, new GUIContent("Title"));
                EditorGUILayout.PropertyField(descriptionProp, new GUIContent("Description"));
            });

            EditorGUILayout.Space(10);

            // Requirements Section
            showRequirements = DrawFoldoutSection("Requirements", showRequirements, () =>
            {
                EditorGUILayout.PropertyField(prerequisiteQuestsProp, new GUIContent("Prerequisites"), true);
                EditorGUILayout.PropertyField(minimumLevelProp, new GUIContent("Minimum Level"));
                EditorGUILayout.PropertyField(isRepeatableProp, new GUIContent("Repeatable"));
            });

            EditorGUILayout.Space(10);

            // Objectives Section
            showObjectives = DrawFoldoutSection($"Objectives ({objectivesProp.arraySize})", showObjectives, () =>
            {
                DrawObjectivesArray();
            });

            EditorGUILayout.Space(10);

            // Rewards Section
            showRewards = DrawFoldoutSection($"Rewards ({rewardsProp.arraySize})", showRewards, () =>
            {
                DrawRewardsArray();
            });

            EditorGUILayout.Space(10);

            // Validation Section
            showValidation = DrawFoldoutSection("Validation", showValidation, () =>
            {
                DrawValidation(questAsset);
            });

            EditorGUILayout.Space(10);

            // Action Buttons
            DrawActionButtons(questAsset);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSection(string title, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            content();
            EditorGUILayout.EndVertical();
        }

        private bool DrawFoldoutSection(string title, bool foldout, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool newFoldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            if (newFoldout)
            {
                EditorGUI.indentLevel++;
                content();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            return newFoldout;
        }

        private void DrawObjectivesArray()
        {
            for (int i = 0; i < objectivesProp.arraySize; i++)
            {
                DrawObjective(i);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Objective", GUILayout.Width(120)))
            {
                objectivesProp.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjective(int index)
        {
            SerializedProperty objectiveProp = objectivesProp.GetArrayElementAtIndex(index);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Objective {index + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                objectivesProp.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(objectiveProp.FindPropertyRelative("objectiveId"), new GUIContent("ID"));
            EditorGUILayout.PropertyField(objectiveProp.FindPropertyRelative("type"), new GUIContent("Type"));
            EditorGUILayout.PropertyField(objectiveProp.FindPropertyRelative("targetId"), new GUIContent("Target ID"));
            EditorGUILayout.PropertyField(objectiveProp.FindPropertyRelative("requiredCount"), new GUIContent("Required Count"));
            EditorGUILayout.PropertyField(objectiveProp.FindPropertyRelative("description"), new GUIContent("Description"));
            EditorGUILayout.PropertyField(objectiveProp.FindPropertyRelative("isOptional"), new GUIContent("Optional"));
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawRewardsArray()
        {
            for (int i = 0; i < rewardsProp.arraySize; i++)
            {
                DrawReward(i);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Reward", GUILayout.Width(120)))
            {
                rewardsProp.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReward(int index)
        {
            SerializedProperty rewardProp = rewardsProp.GetArrayElementAtIndex(index);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Reward {index + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                rewardsProp.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(rewardProp.FindPropertyRelative("type"), new GUIContent("Type"));
            EditorGUILayout.PropertyField(rewardProp.FindPropertyRelative("rewardId"), new GUIContent("Reward ID"));
            EditorGUILayout.PropertyField(rewardProp.FindPropertyRelative("amount"), new GUIContent("Amount"));
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawValidation(QuestAsset questAsset)
        {
            bool hasIssues = false;

            // Check quest ID
            if (string.IsNullOrWhiteSpace(questAsset.questId))
            {
                EditorGUILayout.HelpBox("Quest ID is required", MessageType.Error);
                hasIssues = true;
            }

            // Check title
            if (string.IsNullOrWhiteSpace(questAsset.title))
            {
                EditorGUILayout.HelpBox("Quest title is required", MessageType.Warning);
                hasIssues = true;
            }

            // Check objectives
            if (questAsset.objectives == null || questAsset.objectives.Length == 0)
            {
                EditorGUILayout.HelpBox("Quest must have at least one objective", MessageType.Error);
                hasIssues = true;
            }
            else
            {
                for (int i = 0; i < questAsset.objectives.Length; i++)
                {
                    var obj = questAsset.objectives[i];
                    if (obj != null && string.IsNullOrWhiteSpace(obj.targetId))
                    {
                        EditorGUILayout.HelpBox($"Objective {i + 1} has empty target ID", MessageType.Warning);
                        hasIssues = true;
                    }
                }
            }

            // Check rewards
            if (questAsset.rewards == null || questAsset.rewards.Length == 0)
            {
                EditorGUILayout.HelpBox("Quest should have at least one reward", MessageType.Info);
            }

            // Check for circular prerequisites
            if (questAsset.prerequisiteQuests != null)
            {
                foreach (var prereq in questAsset.prerequisiteQuests)
                {
                    if (prereq == questAsset)
                    {
                        EditorGUILayout.HelpBox("Quest cannot be its own prerequisite", MessageType.Error);
                        hasIssues = true;
                        break;
                    }
                }
            }

            if (!hasIssues)
            {
                EditorGUILayout.HelpBox("No validation issues found", MessageType.Info);
            }
        }

        private void DrawActionButtons(QuestAsset questAsset)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Quest ID from Title"))
            {
                if (!string.IsNullOrWhiteSpace(questAsset.title))
                {
                    Undo.RecordObject(questAsset, "Generate Quest ID");
                    questAsset.questId = "quest_" + questAsset.title.ToLower()
                        .Replace(" ", "_")
                        .Replace("'", "")
                        .Replace("\"", "");
                    EditorUtility.SetDirty(questAsset);
                }
            }

            if (GUILayout.Button("Preview Quest Data"))
            {
                QuestData data = questAsset.ToQuestData();
                Debug.Log($"Quest Preview:\n" +
                         $"ID: {data.Id}\n" +
                         $"Title: {data.Title}\n" +
                         $"Objectives: {data.Objectives?.Length ?? 0}\n" +
                         $"Rewards: {data.Rewards?.Length ?? 0}");
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
