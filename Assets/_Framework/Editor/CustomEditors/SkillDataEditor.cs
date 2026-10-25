using UnityEditor;
using UnityEngine;
using RPGFramework.Skills;
using RPGFramework.Editor.PropertyDrawers;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for Skill data with progress visualization.
    /// </summary>
    public class SkillDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Skill Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Skill ID
            SerializedProperty skillIdProp = serializedObject.FindProperty("_skillId");
            if (skillIdProp != null)
            {
                EditorGUILayout.PropertyField(skillIdProp, new GUIContent("Skill ID"));

                if (string.IsNullOrEmpty(skillIdProp.stringValue))
                {
                    EditorGUILayout.HelpBox("Skill ID is required. Please enter a unique identifier.", MessageType.Error);
                }
            }

            EditorGUILayout.Space(5);

            // Name
            SerializedProperty nameProp = serializedObject.FindProperty("_name");
            if (nameProp != null)
            {
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            }

            // Description
            SerializedProperty descriptionProp = serializedObject.FindProperty("_description");
            if (descriptionProp != null)
            {
                EditorGUILayout.LabelField("Description");
                descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, GUILayout.Height(60));
            }

            EditorGUILayout.Space(10);

            // Skill Properties
            EditorGUILayout.LabelField("Skill Properties", EditorStyles.boldLabel);

            SerializedProperty skillTypeProp = serializedObject.FindProperty("_skillType");
            if (skillTypeProp != null)
            {
                EditorGUILayout.PropertyField(skillTypeProp, new GUIContent("Skill Type"));
            }

            SerializedProperty maxLevelProp = serializedObject.FindProperty("_maxLevel");
            if (maxLevelProp != null)
            {
                EditorGUILayout.PropertyField(maxLevelProp, new GUIContent("Max Level"));

                if (maxLevelProp.intValue <= 0)
                {
                    EditorGUILayout.HelpBox("Max Level must be at least 1.", MessageType.Warning);
                }
            }

            SerializedProperty experienceProp = serializedObject.FindProperty("_experiencePerLevel");
            if (experienceProp != null)
            {
                EditorGUILayout.PropertyField(experienceProp, new GUIContent("Experience Per Level"));
            }

            EditorGUILayout.Space(10);

            // Skill Progression Visualization
            DrawSkillProgression(maxLevelProp, experienceProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSkillProgression(SerializedProperty maxLevelProp, SerializedProperty experienceProp)
        {
            if (maxLevelProp == null || experienceProp == null) return;

            EditorGUILayout.LabelField("Skill Progression Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int maxLevel = maxLevelProp.intValue;
            float expPerLevel = experienceProp.floatValue;

            EditorGUILayout.LabelField($"Max Level: {maxLevel}");
            EditorGUILayout.LabelField($"Total Experience Required: {maxLevel * expPerLevel:F0}");

            // Draw sample progress bars for first 3 levels
            int previewLevels = Mathf.Min(3, maxLevel);
            for (int i = 1; i <= previewLevels; i++)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Level {i} Progress Example:");

                Rect barRect = GUILayoutUtility.GetRect(18, 20);
                float progress = 0.5f; // Sample 50% progress
                ProgressBarDrawer.DrawProgressBar(barRect, progress * expPerLevel, expPerLevel, $"Level {i}", new Color(0.3f, 0.7f, 0.3f));
            }

            EditorGUILayout.EndVertical();
        }
    }
}
