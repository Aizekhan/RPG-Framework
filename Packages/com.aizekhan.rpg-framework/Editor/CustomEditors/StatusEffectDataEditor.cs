using UnityEditor;
using UnityEngine;
using RPGFramework.StatusEffects;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for StatusEffectData with visual effect properties.
    /// </summary>
    [CustomEditor(typeof(StatusEffectData))]
    public class StatusEffectDataEditor : UnityEditor.Editor
    {
        private bool showModifiers = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Status Effect Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Effect ID
            SerializedProperty effectIdProp = serializedObject.FindProperty("effectId");
            EditorGUILayout.PropertyField(effectIdProp);

            if (string.IsNullOrEmpty(effectIdProp.stringValue))
            {
                EditorGUILayout.HelpBox("Effect ID is required. Please enter a unique identifier.", MessageType.Error);
            }

            EditorGUILayout.Space(5);

            // Name
            SerializedProperty nameProp = serializedObject.FindProperty("name");
            EditorGUILayout.PropertyField(nameProp);

            // Description
            SerializedProperty descriptionProp = serializedObject.FindProperty("description");
            EditorGUILayout.LabelField("Description");
            descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, GUILayout.Height(60));

            EditorGUILayout.Space(10);

            // Effect Properties
            EditorGUILayout.LabelField("Effect Properties", EditorStyles.boldLabel);

            SerializedProperty effectTypeProp = serializedObject.FindProperty("effectType");
            EditorGUILayout.PropertyField(effectTypeProp);

            SerializedProperty durationProp = serializedObject.FindProperty("durationSeconds");
            EditorGUILayout.PropertyField(durationProp);

            if (durationProp.floatValue <= 0)
            {
                EditorGUILayout.HelpBox("Duration <= 0 means this effect is permanent or instant.", MessageType.Info);
            }

            SerializedProperty stacksProp = serializedObject.FindProperty("maxStacks");
            EditorGUILayout.PropertyField(stacksProp);

            SerializedProperty isDebuffProp = serializedObject.FindProperty("isDebuff");
            EditorGUILayout.PropertyField(isDebuffProp);

            // Visual indicator for buff/debuff
            Color indicatorColor = isDebuffProp.boolValue ? new Color(1f, 0.3f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f, 0.3f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = indicatorColor;
            EditorGUILayout.LabelField(isDebuffProp.boolValue ? "DEBUFF (Negative Effect)" : "BUFF (Positive Effect)", EditorStyles.boldLabel);
            GUI.backgroundColor = originalBg;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Modifiers
            SerializedProperty modifiersProp = serializedObject.FindProperty("modifiers");
            showModifiers = EditorGUILayout.Foldout(showModifiers, $"Modifiers ({modifiersProp.arraySize})", true);
            if (showModifiers)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(modifiersProp, true);

                if (modifiersProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No modifiers defined. This effect won't change any stats.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
