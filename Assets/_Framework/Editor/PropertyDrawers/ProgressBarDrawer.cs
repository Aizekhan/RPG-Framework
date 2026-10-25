using UnityEditor;
using UnityEngine;

namespace RPGFramework.Editor.PropertyDrawers
{
    /// <summary>
    /// Utility drawer for displaying progress bars in custom editors.
    /// </summary>
    public static class ProgressBarDrawer
    {
        public static void DrawProgressBar(Rect rect, float value, float maxValue, string label, Color barColor)
        {
            float normalizedValue = maxValue > 0 ? value / maxValue : 0;
            normalizedValue = Mathf.Clamp01(normalizedValue);

            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            // Draw progress bar
            Rect barRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * normalizedValue, rect.height - 2);
            EditorGUI.DrawRect(barRect, barColor);

            // Draw border
            Rect borderRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, borderRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.yMax - 1, borderRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, 1, borderRect.height), Color.black);
            EditorGUI.DrawRect(new Rect(borderRect.xMax - 1, borderRect.y, 1, borderRect.height), Color.black);

            // Draw text
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;

            string displayText = $"{label}: {value:F0} / {maxValue:F0} ({normalizedValue * 100:F1}%)";
            EditorGUI.LabelField(rect, displayText, style);
        }

        public static void DrawProgressBar(Rect rect, float normalizedValue, string label, Color barColor)
        {
            normalizedValue = Mathf.Clamp01(normalizedValue);

            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            // Draw progress bar
            Rect barRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * normalizedValue, rect.height - 2);
            EditorGUI.DrawRect(barRect, barColor);

            // Draw border
            Rect borderRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, borderRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.yMax - 1, borderRect.width, 1), Color.black);
            EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, 1, borderRect.height), Color.black);
            EditorGUI.DrawRect(new Rect(borderRect.xMax - 1, borderRect.y, 1, borderRect.height), Color.black);

            // Draw text
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;

            string displayText = $"{label}: {normalizedValue * 100:F1}%";
            EditorGUI.LabelField(rect, displayText, style);
        }
    }
}
