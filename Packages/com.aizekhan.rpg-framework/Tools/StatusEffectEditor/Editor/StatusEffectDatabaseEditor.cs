using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RPGFramework.Tools.StatusEffectEditor;

namespace RPGFramework.Tools.StatusEffectEditor.Editor
{
    /// <summary>
    /// Inspector for the status effect database: its problems (or "Ready") at the top, a one-line summary of each
    /// effect, then the list, with a button that fills the list with every status effect asset in the project.
    /// </summary>
    [CustomEditor(typeof(StatusEffectDatabase))]
    public class StatusEffectDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (StatusEffectDatabase)target;
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Status Effect Database", EditorStyles.boldLabel);

            DrawValidation(database);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
            if (database.effects != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var effect in database.effects)
                {
                    EditorGUILayout.LabelField(effect != null ? effect.Describe() : "(empty slot)", EditorStyles.wordWrappedLabel);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("effects"), true);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Add All Status Effects In Project"))
                AddAllEffects(database);
        }

        private static void DrawValidation(StatusEffectDatabase database)
        {
            var errors = database.Validate();

            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Ready", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        }

        private static void AddAllEffects(StatusEffectDatabase database)
        {
            Undo.RecordObject(database, "Add All Status Effects");
            if (database.effects == null)
                database.effects = new List<StatusEffectAsset>();

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(StatusEffectAsset)))
            {
                var effect = AssetDatabase.LoadAssetAtPath<StatusEffectAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (effect != null && !database.effects.Contains(effect))
                    database.effects.Add(effect);
            }

            EditorUtility.SetDirty(database);
        }
    }
}
