using UnityEditor;
using UnityEngine;

namespace RPGFramework.Tools.AbilityEditor.Editor
{
    /// <summary>
    /// Inspector for an ability database: what is wrong with it at the top, then the list of abilities.
    /// </summary>
    [CustomEditor(typeof(AbilityDatabase))]
    public class AbilityDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (AbilityDatabase)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Ability Database", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var errors = database.Validate();
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox($"Ready - {database.abilities.Count} abilities", MessageType.Info);
            }
            else
            {
                foreach (var error in errors)
                    EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("abilities"), true);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Remove Empty Slots"))
            {
                Undo.RecordObject(database, "Remove Empty Ability Slots");
                database.RemoveEmptySlots();
                EditorUtility.SetDirty(database);
            }
        }
    }
}
