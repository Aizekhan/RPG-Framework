using UnityEditor;
using UnityEngine;
using RPGFramework.Factions;

namespace RPGFramework.Editor.PropertyDrawers
{
    /// <summary>
    /// Property drawer for FactionStanding enum with color-coded display.
    /// </summary>
    [CustomPropertyDrawer(typeof(FactionStanding))]
    public class FactionStandingDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Color originalColor = GUI.backgroundColor;
            Color standingColor = GetStandingColor((FactionStanding)property.enumValueIndex);

            // Color the background based on standing
            GUI.backgroundColor = standingColor;

            Rect boxRect = new Rect(position.x, position.y, position.width, position.height);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

            GUI.backgroundColor = originalColor;

            // Draw the enum popup
            Rect enumRect = new Rect(position.x + 2, position.y + 2, position.width - 4, position.height - 4);
            property.enumValueIndex = EditorGUI.Popup(enumRect, label.text, property.enumValueIndex, property.enumDisplayNames);

            EditorGUI.EndProperty();
        }

        private Color GetStandingColor(FactionStanding standing)
        {
            return standing switch
            {
                FactionStanding.Hated => new Color(0.8f, 0.1f, 0.1f, 0.3f),
                FactionStanding.Hostile => new Color(0.9f, 0.3f, 0.2f, 0.3f),
                FactionStanding.Unfriendly => new Color(0.9f, 0.5f, 0.3f, 0.3f),
                FactionStanding.Neutral => new Color(0.7f, 0.7f, 0.5f, 0.3f),
                FactionStanding.Friendly => new Color(0.5f, 0.8f, 0.5f, 0.3f),
                FactionStanding.Honored => new Color(0.3f, 0.7f, 0.9f, 0.3f),
                FactionStanding.Revered => new Color(0.5f, 0.4f, 0.9f, 0.3f),
                FactionStanding.Exalted => new Color(0.9f, 0.7f, 0.2f, 0.3f),
                _ => Color.white
            };
        }
    }
}
