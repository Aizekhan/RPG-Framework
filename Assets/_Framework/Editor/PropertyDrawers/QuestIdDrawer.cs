using UnityEditor;
using UnityEngine;

namespace RPGFramework.Editor.PropertyDrawers
{
    /// <summary>
    /// Property drawer for quest ID fields to provide validation and UI hints.
    /// </summary>
    [CustomPropertyDrawer(typeof(string))]
    public class QuestIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Only apply to fields with "quest" or "Quest" in the name
            if (property.name.ToLower().Contains("quest") && property.name.ToLower().Contains("id"))
            {
                EditorGUI.BeginProperty(position, label, property);

                Rect textFieldRect = new Rect(position.x, position.y, position.width - 25, position.height);
                Rect iconRect = new Rect(position.x + position.width - 22, position.y, 20, position.height);

                property.stringValue = EditorGUI.TextField(textFieldRect, label, property.stringValue);

                // Show icon if ID is not empty
                if (!string.IsNullOrEmpty(property.stringValue))
                {
                    GUI.Label(iconRect, EditorGUIUtility.IconContent("TestPassed"));
                }
                else
                {
                    GUI.Label(iconRect, EditorGUIUtility.IconContent("console.warnicon.sml"));
                }

                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }
    }
}
