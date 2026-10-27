using UnityEditor;
using RPGFramework.Tools.DialogueEditor;

namespace RPGFramework.Tools.DialogueEditor.Editor
{
    /// <summary>
    /// Inspector for a <see cref="DialogueDatabase"/>: what is wrong with it (or "Ready") at the top, then its list.
    /// </summary>
    [CustomEditor(typeof(DialogueDatabase))]
    public class DialogueDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (DialogueDatabase)target;

            var errors = database.ValidateDatabase();
            if (errors.Count == 0)
                EditorGUILayout.HelpBox("Ready: every dialogue can be played", MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);

            EditorGUILayout.Space(5);
            DrawDefaultInspector();
        }
    }
}
