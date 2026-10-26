using UnityEditor;
using UnityEngine;
using RPGFramework.Quests;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for QuestData ScriptableObjects with visual quest information.
    /// </summary>
    [CustomEditor(typeof(QuestData))]
    public class QuestDataEditor : UnityEditor.Editor
    {
        private bool showObjectives = true;
        private bool showRewards = true;
        private bool showPrerequisites = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quest Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Quest ID
            SerializedProperty questIdProp = serializedObject.FindProperty("questId");
            EditorGUILayout.PropertyField(questIdProp);

            if (string.IsNullOrEmpty(questIdProp.stringValue))
            {
                EditorGUILayout.HelpBox("Quest ID is required. Please enter a unique identifier.", MessageType.Error);
            }

            EditorGUILayout.Space(5);

            // Title
            SerializedProperty titleProp = serializedObject.FindProperty("title");
            EditorGUILayout.PropertyField(titleProp);

            // Description
            SerializedProperty descriptionProp = serializedObject.FindProperty("description");
            EditorGUILayout.LabelField("Description");
            descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, GUILayout.Height(60));

            EditorGUILayout.Space(10);

            // Requirements
            EditorGUILayout.LabelField("Requirements", EditorStyles.boldLabel);
            SerializedProperty minLevelProp = serializedObject.FindProperty("minimumLevel");
            EditorGUILayout.PropertyField(minLevelProp);

            SerializedProperty repeatableProp = serializedObject.FindProperty("isRepeatable");
            EditorGUILayout.PropertyField(repeatableProp);

            EditorGUILayout.Space(10);

            // Prerequisites
            SerializedProperty prerequisitesProp = serializedObject.FindProperty("prerequisiteQuestIds");
            showPrerequisites = EditorGUILayout.Foldout(showPrerequisites, $"Prerequisites ({prerequisitesProp.arraySize})", true);
            if (showPrerequisites)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prerequisitesProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Objectives
            SerializedProperty objectivesProp = serializedObject.FindProperty("objectives");
            showObjectives = EditorGUILayout.Foldout(showObjectives, $"Objectives ({objectivesProp.arraySize})", true);
            if (showObjectives)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(objectivesProp, true);

                if (objectivesProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Quest has no objectives. Add at least one objective.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Rewards
            SerializedProperty rewardsProp = serializedObject.FindProperty("rewards");
            showRewards = EditorGUILayout.Foldout(showRewards, $"Rewards ({rewardsProp.arraySize})", true);
            if (showRewards)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(rewardsProp, true);

                if (rewardsProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Quest has no rewards. Consider adding rewards to motivate players.", MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
