using UnityEditor;
using UnityEngine;
using RPGFramework.Abilities;
using RPGFramework.Editor.PropertyDrawers;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for Ability data with visual ability properties.
    /// </summary>
    public class AbilityDataEditor : UnityEditor.Editor
    {
        private bool showCosts = true;
        private bool showCooldowns = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Ability Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Ability ID
            SerializedProperty abilityIdProp = serializedObject.FindProperty("_abilityId");
            if (abilityIdProp != null)
            {
                EditorGUILayout.PropertyField(abilityIdProp, new GUIContent("Ability ID"));

                if (string.IsNullOrEmpty(abilityIdProp.stringValue))
                {
                    EditorGUILayout.HelpBox("Ability ID is required. Please enter a unique identifier.", MessageType.Error);
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

            // Ability Properties
            EditorGUILayout.LabelField("Ability Properties", EditorStyles.boldLabel);

            SerializedProperty abilityTypeProp = serializedObject.FindProperty("_abilityType");
            if (abilityTypeProp != null)
            {
                EditorGUILayout.PropertyField(abilityTypeProp, new GUIContent("Ability Type"));
            }

            SerializedProperty targetTypeProp = serializedObject.FindProperty("_targetType");
            if (targetTypeProp != null)
            {
                EditorGUILayout.PropertyField(targetTypeProp, new GUIContent("Target Type"));
            }

            SerializedProperty castTimeProp = serializedObject.FindProperty("_castTimeSeconds");
            if (castTimeProp != null)
            {
                EditorGUILayout.PropertyField(castTimeProp, new GUIContent("Cast Time (seconds)"));

                if (castTimeProp.floatValue > 0)
                {
                    EditorGUILayout.HelpBox($"This ability requires {castTimeProp.floatValue:F1}s to cast.", MessageType.Info);
                }
            }

            EditorGUILayout.Space(10);

            // Cooldown
            SerializedProperty cooldownProp = serializedObject.FindProperty("_cooldownSeconds");
            if (cooldownProp != null)
            {
                showCooldowns = EditorGUILayout.Foldout(showCooldowns, "Cooldown", true);
                if (showCooldowns)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(cooldownProp, new GUIContent("Cooldown (seconds)"));

                    if (cooldownProp.floatValue > 0)
                    {
                        Rect barRect = GUILayoutUtility.GetRect(18, 20);
                        float maxCooldown = Mathf.Max(cooldownProp.floatValue, 60f);
                        ProgressBarDrawer.DrawProgressBar(barRect, cooldownProp.floatValue, maxCooldown, "Cooldown", new Color(0.3f, 0.5f, 0.9f));
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(10);

            // Resource Costs
            SerializedProperty costsProp = serializedObject.FindProperty("_costs");
            if (costsProp != null)
            {
                showCosts = EditorGUILayout.Foldout(showCosts, $"Resource Costs ({costsProp.arraySize})", true);
                if (showCosts)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(costsProp, true);
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
