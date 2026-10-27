using UnityEditor;

namespace RPGFramework.Tools.SkillEditor.Editor
{
    /// <summary>
    /// Inspector for a <see cref="SkillAsset"/>: its problems (or "Ready") at the top, then its fields.
    /// </summary>
    [CustomEditor(typeof(SkillAsset))]
    public class SkillAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var skill = (SkillAsset)target;
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Skill Designer", EditorStyles.boldLabel);
            DrawValidation(skill);
            EditorGUILayout.Space(10);

            DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawValidation(SkillAsset skill)
        {
            var errors = skill.Validate();
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
