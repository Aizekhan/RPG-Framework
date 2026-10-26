using UnityEditor;
using UnityEngine;
using System.IO;

namespace RPGFramework.Editor.Utilities
{
    /// <summary>
    /// General utility methods for RPG Framework Editor tools.
    /// </summary>
    public static class FrameworkEditorUtility
    {
        /// <summary>
        /// Get the color associated with a given rarity level.
        /// </summary>
        public static Color GetRarityColor(int rarityIndex)
        {
            return rarityIndex switch
            {
                0 => new Color(0.7f, 0.7f, 0.7f), // Common
                1 => new Color(0.3f, 0.8f, 0.3f), // Uncommon
                2 => new Color(0.3f, 0.5f, 1.0f), // Rare
                3 => new Color(0.7f, 0.3f, 1.0f), // Epic
                4 => new Color(1.0f, 0.5f, 0.0f), // Legendary
                5 => new Color(1.0f, 0.3f, 0.3f), // Mythic
                _ => Color.white
            };
        }

        /// <summary>
        /// Get the color associated with a given faction standing.
        /// </summary>
        public static Color GetStandingColor(int standingIndex)
        {
            return standingIndex switch
            {
                0 => new Color(0.8f, 0.1f, 0.1f), // Hated
                1 => new Color(0.9f, 0.3f, 0.2f), // Hostile
                2 => new Color(0.9f, 0.5f, 0.3f), // Unfriendly
                3 => new Color(0.7f, 0.7f, 0.5f), // Neutral
                4 => new Color(0.5f, 0.8f, 0.5f), // Friendly
                5 => new Color(0.3f, 0.7f, 0.9f), // Honored
                6 => new Color(0.5f, 0.4f, 0.9f), // Revered
                7 => new Color(0.9f, 0.7f, 0.2f), // Exalted
                _ => Color.white
            };
        }

        /// <summary>
        /// Draw a separator line in the Inspector.
        /// </summary>
        public static void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// Draw a header label with custom styling.
        /// </summary>
        public static void DrawHeader(string text)
        {
            EditorGUILayout.Space(5);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField(text, headerStyle);
            DrawSeparator();
        }

        /// <summary>
        /// Draw a sub-header label.
        /// </summary>
        public static void DrawSubHeader(string text)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        /// <summary>
        /// Draw a help box with an icon.
        /// </summary>
        public static void DrawHelpBox(string message, MessageType messageType)
        {
            EditorGUILayout.HelpBox(message, messageType);
        }

        /// <summary>
        /// Create a new folder if it doesn't exist.
        /// </summary>
        public static void EnsureFolderExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parentFolder = Path.GetDirectoryName(path).Replace('\\', '/');
                string folderName = Path.GetFileName(path);

                if (!string.IsNullOrEmpty(parentFolder) && !string.IsNullOrEmpty(folderName))
                {
                    EnsureFolderExists(parentFolder);
                    AssetDatabase.CreateFolder(parentFolder, folderName);
                }
            }
        }

        /// <summary>
        /// Get all assets of a specific type from the project.
        /// </summary>
        public static T[] GetAllAssetsOfType<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            T[] assets = new T[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return assets;
        }

        /// <summary>
        /// Ping an object in the Project window.
        /// </summary>
        public static void PingObject(Object obj)
        {
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
        }

        /// <summary>
        /// Create a ScriptableObject asset at the specified path.
        /// </summary>
        public static T CreateScriptableObjectAsset<T>(string path, string defaultName) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(path))
            {
                path = EditorUtility.SaveFilePanelInProject(
                    $"Create {typeof(T).Name}",
                    defaultName,
                    "asset",
                    $"Enter a name for the new {typeof(T).Name}"
                );
            }

            if (string.IsNullOrEmpty(path))
                return null;

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            return asset;
        }

        /// <summary>
        /// Duplicate an asset at a new path.
        /// </summary>
        public static bool DuplicateAsset(Object asset, out Object duplicatedAsset)
        {
            duplicatedAsset = null;

            if (asset == null)
                return false;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return false;

            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

            if (AssetDatabase.CopyAsset(path, newPath))
            {
                AssetDatabase.SaveAssets();
                duplicatedAsset = AssetDatabase.LoadAssetAtPath<Object>(newPath);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Delete an asset with confirmation.
        /// </summary>
        public static bool DeleteAssetWithConfirmation(Object asset)
        {
            if (asset == null)
                return false;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Asset",
                $"Are you sure you want to delete '{asset.name}'?\n\nThis action cannot be undone.",
                "Delete",
                "Cancel"
            );

            if (confirmed)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.SaveAssets();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Draw a progress bar in the GUI.
        /// </summary>
        public static void DrawProgressBar(float value, float maxValue, string label)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            float progress = maxValue > 0 ? value / maxValue : 0;
            EditorGUI.ProgressBar(rect, progress, $"{label}: {value:F0} / {maxValue:F0}");
        }

        /// <summary>
        /// Draw a colored progress bar with custom colors.
        /// </summary>
        public static void DrawColoredProgressBar(Rect rect, float normalizedValue, string label, Color barColor)
        {
            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            // Draw progress bar
            Rect barRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * normalizedValue, rect.height - 2);
            EditorGUI.DrawRect(barRect, barColor);

            // Draw border
            DrawRectBorder(rect, Color.black);

            // Draw text
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            EditorGUI.LabelField(rect, label, style);
        }

        /// <summary>
        /// Draw a border around a rect.
        /// </summary>
        private static void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        /// <summary>
        /// Get a styled box for drawing sections.
        /// </summary>
        public static GUIStyle GetSectionBoxStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
            return style;
        }

        /// <summary>
        /// Display a success message dialog.
        /// </summary>
        public static void ShowSuccessDialog(string title, string message)
        {
            EditorUtility.DisplayDialog(title, message, "OK");
        }

        /// <summary>
        /// Display an error message dialog.
        /// </summary>
        public static void ShowErrorDialog(string title, string message)
        {
            EditorUtility.DisplayDialog(title, message, "OK");
        }

        /// <summary>
        /// Display a confirmation dialog.
        /// </summary>
        public static bool ShowConfirmDialog(string title, string message)
        {
            return EditorUtility.DisplayDialog(title, message, "Yes", "No");
        }
    }
}
