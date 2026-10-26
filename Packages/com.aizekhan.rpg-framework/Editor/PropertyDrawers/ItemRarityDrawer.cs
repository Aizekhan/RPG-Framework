using UnityEditor;
using UnityEngine;
using RPGFramework.Items;

namespace RPGFramework.Editor.PropertyDrawers
{
    /// <summary>
    /// Property drawer for ItemRarity enum with color-coded display.
    /// </summary>
    [CustomPropertyDrawer(typeof(ItemRarity))]
    public class ItemRarityDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Color originalColor = GUI.backgroundColor;
            Color rarityColor = GetRarityColor((ItemRarity)property.enumValueIndex);

            // Color the background based on rarity
            GUI.backgroundColor = rarityColor;

            Rect boxRect = new Rect(position.x, position.y, position.width, position.height);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

            GUI.backgroundColor = originalColor;

            // Draw the enum popup
            Rect enumRect = new Rect(position.x + 2, position.y + 2, position.width - 4, position.height - 4);
            property.enumValueIndex = EditorGUI.Popup(enumRect, label.text, property.enumValueIndex, property.enumDisplayNames);

            EditorGUI.EndProperty();
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f, 0.3f),
                ItemRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1.0f, 0.3f),
                ItemRarity.Epic => new Color(0.7f, 0.3f, 1.0f, 0.3f),
                ItemRarity.Legendary => new Color(1.0f, 0.5f, 0.0f, 0.3f),
                ItemRarity.Mythic => new Color(1.0f, 0.3f, 0.3f, 0.3f),
                _ => Color.white
            };
        }
    }
}
