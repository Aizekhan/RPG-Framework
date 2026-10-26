using UnityEngine;
using UnityEditor;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Items;
using System.IO;

namespace RPGFramework.Tools.ItemDatabaseManager.Editor
{
    public class ItemCreationWizard : EditorWindow
    {
        private string itemName = "New Item";
        private ItemTemplate selectedTemplate = ItemTemplate.Custom;
        private string savePath = "Assets/Items";
        private bool addToDatabase = true;
        private ItemDatabase targetDatabase;

        private enum ItemTemplate
        {
            Custom,
            Weapon,
            Armor,
            Accessory,
            HealthPotion,
            ManaPotion,
            CraftingMaterial,
            QuestItem
        }

        [MenuItem("Tools/RPG Framework/Item Creation Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<ItemCreationWizard>("Item Wizard");
            window.minSize = new Vector2(400, 450);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Item Creation Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox("Create a new item asset quickly using templates.", MessageType.Info);

            EditorGUILayout.Space(10);

            // Template
            EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
            selectedTemplate = (ItemTemplate)EditorGUILayout.EnumPopup("Item Type", selectedTemplate);

            EditorGUILayout.Space(5);
            DrawTemplateDescription();

            EditorGUILayout.Space(10);

            // Basic info
            EditorGUILayout.LabelField("Item Details", EditorStyles.boldLabel);
            itemName = EditorGUILayout.TextField("Item Name", itemName);

            EditorGUILayout.Space(10);

            // Save options
            EditorGUILayout.LabelField("Save Options", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save Path", GUILayout.Width(70));
            savePath = EditorGUILayout.TextField(savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Item Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    savePath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            addToDatabase = EditorGUILayout.Toggle("Add to Database", addToDatabase);
            if (addToDatabase)
            {
                targetDatabase = (ItemDatabase)EditorGUILayout.ObjectField(
                    "Target Database", targetDatabase, typeof(ItemDatabase), false);
            }

            EditorGUILayout.Space(20);

            GUI.enabled = !string.IsNullOrWhiteSpace(itemName);
            if (GUILayout.Button("Create Item", GUILayout.Height(40)))
            {
                CreateItem();
            }
            GUI.enabled = true;
        }

        private void DrawTemplateDescription()
        {
            string description = selectedTemplate switch
            {
                ItemTemplate.Weapon => "Weapon\n• Equipment type\n• Damage stat modifier\n• Non-stackable",
                ItemTemplate.Armor => "Armor\n• Equipment type\n• Defense stat modifier\n• Non-stackable",
                ItemTemplate.Accessory => "Accessory\n• Equipment type\n• Multiple stat modifiers\n• Non-stackable",
                ItemTemplate.HealthPotion => "Health Potion\n• Consumable type\n• Stackable\n• Instant use",
                ItemTemplate.ManaPotion => "Mana Potion\n• Consumable type\n• Stackable\n• Instant use",
                ItemTemplate.CraftingMaterial => "Crafting Material\n• Crafting type\n• Highly stackable\n• Common rarity",
                ItemTemplate.QuestItem => "Quest Item\n• Quest type\n• Non-stackable\n• Cannot be traded",
                ItemTemplate.Custom => "Custom Item\n• Configure manually\n• No pre-filled properties",
                _ => ""
            };

            EditorGUILayout.HelpBox(description, MessageType.None);
        }

        private void CreateItem()
        {
            if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

            ItemAsset item = CreateInstance<ItemAsset>();
            item.itemId = "item_" + itemName.ToLower().Replace(" ", "_");
            item.itemName = itemName;

            ApplyTemplate(item);

            string fileName = itemName.Replace(" ", "") + ".asset";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(savePath, fileName));

            AssetDatabase.CreateAsset(item, assetPath);
            AssetDatabase.SaveAssets();

            if (addToDatabase && targetDatabase != null)
            {
                Undo.RecordObject(targetDatabase, "Add Item to Database");
                if (!targetDatabase.items.Contains(item))
                {
                    targetDatabase.items.Add(item);
                    targetDatabase.UpdateStatistics();
                    EditorUtility.SetDirty(targetDatabase);
                }
            }

            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);

            EditorUtility.DisplayDialog("Item Created", $"Item '{itemName}' created successfully at:\n{assetPath}", "OK");

            itemName = "New Item";
            selectedTemplate = ItemTemplate.Custom;
        }

        private void ApplyTemplate(ItemAsset item)
        {
            switch (selectedTemplate)
            {
                case ItemTemplate.Weapon:
                    item.itemType = ItemType.Equipment;
                    item.rarity = ItemRarity.Common;
                    item.equipmentSlot = "MainHand";
                    item.maxStackSize = 1;
                    item.statModifiers = new ItemStatModifierEntry[]
                    {
                        new ItemStatModifierEntry { statName = "Damage", value = 10 }
                    };
                    break;

                case ItemTemplate.Armor:
                    item.itemType = ItemType.Equipment;
                    item.rarity = ItemRarity.Common;
                    item.equipmentSlot = "Chest";
                    item.maxStackSize = 1;
                    item.statModifiers = new ItemStatModifierEntry[]
                    {
                        new ItemStatModifierEntry { statName = "Defense", value = 15 }
                    };
                    break;

                case ItemTemplate.Accessory:
                    item.itemType = ItemType.Equipment;
                    item.rarity = ItemRarity.Uncommon;
                    item.equipmentSlot = "Ring";
                    item.maxStackSize = 1;
                    item.statModifiers = new ItemStatModifierEntry[]
                    {
                        new ItemStatModifierEntry { statName = "Health", value = 20 },
                        new ItemStatModifierEntry { statName = "Mana", value = 10 }
                    };
                    break;

                case ItemTemplate.HealthPotion:
                    item.itemType = ItemType.Consumable;
                    item.rarity = ItemRarity.Common;
                    item.maxStackSize = 99;
                    item.useDuration = 0f;
                    item.cooldown = 30f;
                    item.consumeOnUse = true;
                    break;

                case ItemTemplate.ManaPotion:
                    item.itemType = ItemType.Consumable;
                    item.rarity = ItemRarity.Common;
                    item.maxStackSize = 99;
                    item.useDuration = 0f;
                    item.cooldown = 30f;
                    item.consumeOnUse = true;
                    break;

                case ItemTemplate.CraftingMaterial:
                    item.itemType = ItemType.Crafting;
                    item.rarity = ItemRarity.Common;
                    item.maxStackSize = 999;
                    break;

                case ItemTemplate.QuestItem:
                    item.itemType = ItemType.Quest;
                    item.rarity = ItemRarity.Rare;
                    item.maxStackSize = 1;
                    break;
            }
        }
    }
}
