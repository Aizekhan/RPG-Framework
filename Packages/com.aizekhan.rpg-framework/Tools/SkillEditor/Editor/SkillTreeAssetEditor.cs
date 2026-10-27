using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace RPGFramework.Tools.SkillEditor.Editor
{
    /// <summary>
    /// Inspector for a <see cref="SkillTreeAsset"/>: the tree's problems (or "Ready") at the top, then its fields,
    /// then a read-only outline by tier: skills with no prerequisites first, then the skills that build on them.
    /// </summary>
    [CustomEditor(typeof(SkillTreeAsset))]
    public class SkillTreeAssetEditor : UnityEditor.Editor
    {
        private bool showOutline = true;

        public override void OnInspectorGUI()
        {
            var tree = (SkillTreeAsset)target;
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Skill Tree Designer", EditorStyles.boldLabel);
            DrawValidation(tree);
            EditorGUILayout.Space(10);

            DrawPropertiesExcluding(serializedObject, "m_Script");

            EditorGUILayout.Space(10);
            showOutline = EditorGUILayout.Foldout(showOutline, "Outline", true);
            if (showOutline)
                DrawOutline(tree);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawValidation(SkillTreeAsset tree)
        {
            var errors = tree.Validate();
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Ready", MessageType.Info);
                return;
            }

            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private static void DrawOutline(SkillTreeAsset tree)
        {
            var tiers = tree.GetTiers();
            var placed = new HashSet<SkillAsset>(tiers.SelectMany(t => t));
            var stuck = (tree.skills ?? new List<SkillAsset>())
                .Where(s => s != null && !placed.Contains(s))
                .Distinct()
                .ToList();

            if (tiers.Count == 0 && stuck.Count == 0)
            {
                EditorGUILayout.LabelField("No skills yet");
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    EditorGUILayout.LabelField(i == 0 ? "Tier 1 (no prerequisites)" : $"Tier {i + 1}",
                        EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    foreach (var skill in tiers[i])
                        EditorGUILayout.ObjectField(Describe(skill), skill, typeof(SkillAsset), false);
                    EditorGUI.indentLevel--;
                }

                if (stuck.Count > 0)
                {
                    EditorGUILayout.LabelField("In a prerequisite loop", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    foreach (var skill in stuck)
                        EditorGUILayout.ObjectField(Describe(skill), skill, typeof(SkillAsset), false);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private static string Describe(SkillAsset skill)
        {
            var needs = (skill.prerequisites ?? new SkillPrerequisiteEntry[0])
                .Where(p => p != null && p.skill != null)
                .Select(p => $"{p.skill.skillId} {p.requiredLevel}")
                .ToList();
            string label = $"{skill.skillId} (max {skill.maxLevel})";
            return needs.Count == 0 ? label : $"{label} needs {string.Join(", ", needs)}";
        }
    }
}
