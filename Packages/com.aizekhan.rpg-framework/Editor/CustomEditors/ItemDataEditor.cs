using UnityEditor;
using UnityEngine;
using RPGFramework.Items;

namespace RPGFramework.Editor.CustomEditors
{
    /// <summary>
    /// Custom editor for Item data with visual item properties.
    /// </summary>
    public class ItemDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Item ID
            SerializedProperty itemIdProp = serializedObject.FindProperty("_itemId");
            if (itemIdProp != null)
            {
                EditorGUILayout.PropertyField(itemIdProp, new GUIContent("Item ID"));

                if (string.IsNullOrEmpty(itemIdProp.stringValue))
                {
                    EditorGUILayout.HelpBox("Item ID is required. Please enter a unique identifier.", MessageType.Error);
                }
            }

            EditorGUILayout.Space(5);

            // Name
            SerializedProperty nameProp = serializedObject.FindProperty("_name");
            if (nameProp != null)
            {
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            }

            // Icon ID
            SerializedProperty iconIdProp = serializedObject.FindProperty("_iconId");
            if (iconIdProp != null)
            {
                EditorGUILayout.PropertyField(iconIdProp, new GUIContent("Icon ID"));
            }

            // Description
            SerializedProperty descriptionProp = serializedObject.FindProperty("_description");
            if (descriptionProp != null)
            {
                EditorGUILayout.LabelField("Description");
                descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, GUILayout.Height(60));
            }

            EditorGUILayout.Space(10);

            // Item Properties
            EditorGUILayout.LabelField("Item Properties", EditorStyles.boldLabel);

            SerializedProperty itemTypeProp = serializedObject.FindProperty("_itemType");
            if (itemTypeProp != null)
            {
                EditorGUILayout.PropertyField(itemTypeProp, new GUIContent("Item Type"));
            }

            SerializedProperty rarityProp = serializedObject.FindProperty("_rarity");
            if (rarityProp != null)
            {
                EditorGUILayout.PropertyField(rarityProp, new GUIContent("Rarity"));
            }

            SerializedProperty qualityProp = serializedObject.FindProperty("_quality");
            if (qualityProp != null)
            {
                EditorGUILayout.PropertyField(qualityProp, new GUIContent("Quality"));
            }

            SerializedProperty itemLevelProp = serializedObject.FindProperty("_itemLevel");
            if (itemLevelProp != null)
            {
                EditorGUILayout.PropertyField(itemLevelProp, new GUIContent("Item Level"));
            }

            SerializedProperty maxStackProp = serializedObject.FindProperty("_maxStackSize");
            if (maxStackProp != null)
            {
                EditorGUILayout.PropertyField(maxStackProp, new GUIContent("Max Stack Size"));

                if (maxStackProp.intValue <= 0)
                {
                    EditorGUILayout.HelpBox("Max Stack Size must be at least 1.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            // Item Summary
            DrawItemSummary(rarityProp, itemTypeProp, maxStackProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawItemSummary(SerializedProperty rarityProp, SerializedProperty itemTypeProp, SerializedProperty maxStackProp)
        {
            EditorGUILayout.LabelField("Item Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (rarityProp != null && itemTypeProp != null && maxStackProp != null)
            {
                EditorGUILayout.LabelField($"Type: {itemTypeProp.enumDisplayNames[itemTypeProp.enumValueIndex]}");
                EditorGUILayout.LabelField($"Rarity: {rarityProp.enumDisplayNames[rarityProp.enumValueIndex]}");
                EditorGUILayout.LabelField($"Stackable: {(maxStackProp.intValue > 1 ? "Yes" : "No")}");
            }

            EditorGUILayout.EndVertical();
        }
    }
}
