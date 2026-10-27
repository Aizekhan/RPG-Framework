using UnityEditor;
using UnityEngine;

namespace RPGFramework.Tools.AbilityEditor.Editor
{
    /// <summary>
    /// Inspector for an ability asset: what is wrong with it at the top, then its fields in groups.
    /// </summary>
    [CustomEditor(typeof(AbilityAsset))]
    public class AbilityAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ability = (AbilityAsset)target;
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Ability Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawValidation(ability);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            DrawProperty("abilityId");
            DrawProperty("abilityName");
            DrawProperty("description");
            DrawProperty("iconId");
            DrawProperty("abilityType");
            DrawProperty("minLevel");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Costs", EditorStyles.boldLabel);
            DrawProperty("costs");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            DrawProperty("cooldown");
            DrawProperty("castTime");
            DrawProperty("channelDuration");
            DrawProperty("isInterruptible");
            DrawProperty("canMoveWhileCasting");
            DrawProperty("maxCharges");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Targeting", EditorStyles.boldLabel);
            DrawProperty("targetType");
            DrawProperty("range");
            DrawProperty("aoeRadius");

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "What the ability does when it lands is your game's code: the ability system calls it with this ability's id.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
        }

        private static void DrawValidation(AbilityAsset ability)
        {
            var errors = ability.Validate();

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
