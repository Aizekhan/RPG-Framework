using UnityEditor;
using UnityEngine;
using RPGFramework.Tools.StatusEffectEditor;

namespace RPGFramework.Tools.StatusEffectEditor.Editor
{
    /// <summary>
    /// Inspector for a status effect: its problems (or "Ready") at the top, a one-line summary, then the fields in groups.
    /// </summary>
    [CustomEditor(typeof(StatusEffectAsset))]
    public class StatusEffectAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var effect = (StatusEffectAsset)target;
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Status Effect Designer", EditorStyles.boldLabel);

            DrawValidation(effect);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(effect.Describe(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            DrawProperty("effectId");
            DrawProperty("effectName");
            DrawProperty("description");
            DrawProperty("effectType");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Duration", EditorStyles.boldLabel);
            DrawProperty("length");
            if (effect.length == StatusEffectLength.Timed)
                DrawProperty("duration");
            DrawProperty("tickInterval");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Stacking", EditorStyles.boldLabel);
            DrawProperty("isStackable");
            DrawProperty("maxStacks");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Modifiers", EditorStyles.boldLabel);
            DrawProperty("modifiers");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
        }

        private static void DrawValidation(StatusEffectAsset effect)
        {
            var errors = effect.Validate();

            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Ready", MessageType.Info);
                return;
            }

            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
