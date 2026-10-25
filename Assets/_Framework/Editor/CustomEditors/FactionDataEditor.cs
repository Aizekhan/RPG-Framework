using UnityEditor;
using UnityEngine;
using RPGFramework.Factions;
using RPGFramework.Editor.PropertyDrawers;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for Faction data with reputation visualization.
    /// </summary>
    [CustomEditor(typeof(Faction))]
    public class FactionDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Faction Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Faction ID
            SerializedProperty factionIdProp = serializedObject.FindProperty("_factionId");
            if (factionIdProp != null)
            {
                EditorGUILayout.PropertyField(factionIdProp, new GUIContent("Faction ID"));

                if (string.IsNullOrEmpty(factionIdProp.stringValue))
                {
                    EditorGUILayout.HelpBox("Faction ID is required. Please enter a unique identifier.", MessageType.Error);
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

            // Faction Type
            SerializedProperty factionTypeProp = serializedObject.FindProperty("_factionType");
            if (factionTypeProp != null)
            {
                EditorGUILayout.PropertyField(factionTypeProp, new GUIContent("Faction Type"));
            }

            // Default Reputation
            SerializedProperty defaultRepProp = serializedObject.FindProperty("_defaultReputation");
            if (defaultRepProp != null)
            {
                EditorGUILayout.PropertyField(defaultRepProp, new GUIContent("Default Reputation"));

                // Show reputation bar
                Rect barRect = GUILayoutUtility.GetRect(18, 20);
                float normalizedRep = (defaultRepProp.intValue + 100) / 200f; // Assuming -100 to +100 range
                Color repColor = GetReputationColor(defaultRepProp.intValue);
                ProgressBarDrawer.DrawProgressBar(barRect, normalizedRep, "Reputation", repColor);
            }

            EditorGUILayout.Space(5);

            // Active
            SerializedProperty activeProp = serializedObject.FindProperty("_isActive");
            if (activeProp != null)
            {
                EditorGUILayout.PropertyField(activeProp, new GUIContent("Is Active"));

                if (!activeProp.boolValue)
                {
                    EditorGUILayout.HelpBox("This faction is inactive and will not be available in the game.", MessageType.Info);
                }
            }

            EditorGUILayout.Space(10);

            // Faction Info
            DrawFactionInfo(defaultRepProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFactionInfo(SerializedProperty defaultRepProp)
        {
            if (defaultRepProp == null) return;

            EditorGUILayout.LabelField("Reputation Info", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int reputation = defaultRepProp.intValue;
            FactionStanding standing = GetStandingFromReputation(reputation);

            EditorGUILayout.LabelField($"Default Standing: {standing}");
            EditorGUILayout.LabelField($"Reputation Value: {reputation}");

            EditorGUILayout.EndVertical();
        }

        private FactionStanding GetStandingFromReputation(int reputation)
        {
            if (reputation < -75) return FactionStanding.Hated;
            if (reputation < -50) return FactionStanding.Hostile;
            if (reputation < -25) return FactionStanding.Unfriendly;
            if (reputation < 25) return FactionStanding.Neutral;
            if (reputation < 50) return FactionStanding.Friendly;
            if (reputation < 75) return FactionStanding.Honored;
            if (reputation < 90) return FactionStanding.Revered;
            return FactionStanding.Exalted;
        }

        private Color GetReputationColor(int reputation)
        {
            FactionStanding standing = GetStandingFromReputation(reputation);
            return standing switch
            {
                FactionStanding.Hated => new Color(0.8f, 0.1f, 0.1f),
                FactionStanding.Hostile => new Color(0.9f, 0.3f, 0.2f),
                FactionStanding.Unfriendly => new Color(0.9f, 0.5f, 0.3f),
                FactionStanding.Neutral => new Color(0.7f, 0.7f, 0.5f),
                FactionStanding.Friendly => new Color(0.5f, 0.8f, 0.5f),
                FactionStanding.Honored => new Color(0.3f, 0.7f, 0.9f),
                FactionStanding.Revered => new Color(0.5f, 0.4f, 0.9f),
                FactionStanding.Exalted => new Color(0.9f, 0.7f, 0.2f),
                _ => Color.white
            };
        }
    }
}
