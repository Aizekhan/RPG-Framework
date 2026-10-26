using UnityEditor;
using UnityEngine;
using RPGFramework.Items;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Editor.EditorWindows
{
    /// <summary>
    /// Item database browser for managing and viewing items.
    /// </summary>
    public class ItemDatabaseWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<Object> allItems = new List<Object>();
        private string searchFilter = "";
        private ItemType filterType = (ItemType)(-1); // -1 means "All"
        private ItemRarity filterRarity = (ItemRarity)(-1);
        private Object selectedItem;

        [MenuItem("RPG Framework/Tools/Item Database", false, 3)]
        public static void ShowWindow()
        {
            ItemDatabaseWindow window = GetWindow<ItemDatabaseWindow>("Item Database");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshItemList();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawFilters();

            EditorGUILayout.BeginHorizontal();

            // Left panel - Item list
            DrawItemListPanel();

            // Right panel - Item details
            DrawItemDetailsPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Item Database", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Items: {allItems.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            GUILayout.Space(10);

            // Type filter
            EditorGUILayout.LabelField("Type:", GUILayout.Width(40));
            filterType = (ItemType)EditorGUILayout.EnumPopup(filterType, EditorStyles.toolbarDropDown, GUILayout.Width(120));

            GUILayout.Space(10);

            // Rarity filter
            EditorGUILayout.LabelField("Rarity:", GUILayout.Width(45));
            filterRarity = (ItemRarity)EditorGUILayout.EnumPopup(filterRarity, EditorStyles.toolbarDropDown, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear Filters", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                searchFilter = "";
                filterType = (ItemType)(-1);
                filterRarity = (ItemRarity)(-1);
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshItemList();
            }

            if (GUILayout.Button("Create Item", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CreateNewItem();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawItemListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));

            EditorGUILayout.LabelField($"Items ({GetFilteredItems().Count})", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var filteredItems = GetFilteredItems();

            foreach (var item in filteredItems)
            {
                if (item == null) continue;

                bool isSelected = selectedItem == item;
                Color originalBg = GUI.backgroundColor;

                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                if (GUILayout.Button(item.name, EditorStyles.label))
                {
                    selectedItem = item;
                    Selection.activeObject = item;
                }

                // Show rarity indicator
                DrawRarityIndicator(item);

                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = originalBg;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawItemDetailsPanel()
        {
            EditorGUILayout.BeginVertical();

            if (selectedItem == null)
            {
                EditorGUILayout.HelpBox("Select an item from the list to view details.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Item Details: {selectedItem.name}", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);

                // Draw item inspector
                UnityEditor.Editor itemEditor = UnityEditor.Editor.CreateEditor(selectedItem);
                if (itemEditor != null)
                {
                    itemEditor.OnInspectorGUI();
                }

                EditorGUILayout.Space(10);

                // Quick actions
                EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Ping in Project"))
                {
                    EditorGUIUtility.PingObject(selectedItem);
                }

                if (GUILayout.Button("Duplicate"))
                {
                    DuplicateItem(selectedItem);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRarityIndicator(Object item)
        {
            SerializedObject so = new SerializedObject(item);
            SerializedProperty rarityProp = so.FindProperty("_rarity");

            if (rarityProp != null)
            {
                ItemRarity rarity = (ItemRarity)rarityProp.enumValueIndex;
                Color rarityColor = GetRarityColor(rarity);

                Rect colorRect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                EditorGUI.DrawRect(colorRect, rarityColor);
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f),
                ItemRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1.0f),
                ItemRarity.Epic => new Color(0.7f, 0.3f, 1.0f),
                ItemRarity.Legendary => new Color(1.0f, 0.5f, 0.0f),
                ItemRarity.Mythic => new Color(1.0f, 0.3f, 0.3f),
                _ => Color.white
            };
        }

        private List<Object> GetFilteredItems()
        {
            return allItems.Where(item =>
            {
                if (item == null) return false;

                // Apply search filter
                if (!string.IsNullOrEmpty(searchFilter) &&
                    !item.name.ToLower().Contains(searchFilter.ToLower()))
                {
                    return false;
                }

                SerializedObject so = new SerializedObject(item);

                // Apply type filter
                if ((int)filterType >= 0)
                {
                    SerializedProperty typeProp = so.FindProperty("_itemType");
                    if (typeProp != null && typeProp.enumValueIndex != (int)filterType)
                    {
                        return false;
                    }
                }

                // Apply rarity filter
                if ((int)filterRarity >= 0)
                {
                    SerializedProperty rarityProp = so.FindProperty("_rarity");
                    if (rarityProp != null && rarityProp.enumValueIndex != (int)filterRarity)
                    {
                        return false;
                    }
                }

                return true;
            }).ToList();
        }

        private void RefreshItemList()
        {
            allItems.Clear();

            // Find all items (looking for common base classes that might implement IItem)
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (obj != null && obj.GetType().Namespace != null &&
                    obj.GetType().Namespace.StartsWith("RPGFramework.Items"))
                {
                    allItems.Add(obj);
                }
            }
        }

        private void CreateNewItem()
        {
            EditorUtility.DisplayDialog("Create Item",
                "Item creation wizard to be implemented.\n\nFor now, create items manually using:\nAssets > Create > RPG Framework > Items",
                "OK");
        }

        private void DuplicateItem(Object item)
        {
            if (item == null) return;

            string path = AssetDatabase.GetAssetPath(item);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

            if (AssetDatabase.CopyAsset(path, newPath))
            {
                RefreshItemList();
                EditorUtility.DisplayDialog("Success", "Item duplicated successfully!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to duplicate item.", "OK");
            }
        }
    }
}
