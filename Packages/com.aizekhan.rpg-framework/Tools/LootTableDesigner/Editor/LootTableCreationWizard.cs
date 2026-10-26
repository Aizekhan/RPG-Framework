using UnityEngine;
using UnityEditor;
using RPGFramework.Tools.LootTableDesigner;
using System.IO;
using System.Collections.Generic;

namespace RPGFramework.Tools.LootTableDesigner.Editor
{
    /// <summary>
    /// Wizard for quick loot table creation with templates.
    /// </summary>
    public class LootTableCreationWizard : EditorWindow
    {
        private string tableName = "New Loot Table";
        private LootTableTemplate selectedTemplate = LootTableTemplate.Custom;
        private string savePath = "Assets/LootTables";
        private bool addToDatabase = true;
        private LootTableDatabase targetDatabase;

        private enum LootTableTemplate
        {
            Custom,
            CommonEnemy,
            EliteEnemy,
            Boss,
            WoodenChest,
            GoldenChest,
            ResourceNode
        }

        [MenuItem("Tools/RPG Framework/Loot Table Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<LootTableCreationWizard>("Loot Table Wizard");
            window.minSize = new Vector2(420, 500);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Loot Table Creation Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Create a new loot table asset quickly using templates.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Template selection
            EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
            selectedTemplate = (LootTableTemplate)EditorGUILayout.EnumPopup("Loot Table Type", selectedTemplate);

            EditorGUILayout.Space(5);

            // Template description
            DrawTemplateDescription();

            EditorGUILayout.Space(10);

            // Basic info
            EditorGUILayout.LabelField("Loot Table Details", EditorStyles.boldLabel);
            tableName = EditorGUILayout.TextField("Table Name", tableName);

            EditorGUILayout.Space(10);

            // Save options
            EditorGUILayout.LabelField("Save Options", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save Path", GUILayout.Width(70));
            savePath = EditorGUILayout.TextField(savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Loot Table Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    savePath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            addToDatabase = EditorGUILayout.Toggle("Add to Database", addToDatabase);
            if (addToDatabase)
            {
                targetDatabase = (LootTableDatabase)EditorGUILayout.ObjectField(
                    "Target Database",
                    targetDatabase,
                    typeof(LootTableDatabase),
                    false);
            }

            EditorGUILayout.Space(20);

            // Create button
            GUI.enabled = !string.IsNullOrWhiteSpace(tableName);
            if (GUILayout.Button("Create Loot Table", GUILayout.Height(40)))
            {
                CreateLootTable();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);
        }

        private void DrawTemplateDescription()
        {
            string description = selectedTemplate switch
            {
                LootTableTemplate.CommonEnemy => "Common enemy loot\n• 1-2 drops\n• Common items, small gold\n• Low weight entries",
                LootTableTemplate.EliteEnemy => "Elite enemy loot\n• 2-4 drops\n• Mix of common/uncommon\n• Medium gold, potions",
                LootTableTemplate.Boss => "Boss loot\n• 5-8 drops\n• Rare/epic items guaranteed\n• High gold, unique items",
                LootTableTemplate.WoodenChest => "Basic chest loot\n• 2-4 drops\n• Consumables and equipment\n• No duplicates",
                LootTableTemplate.GoldenChest => "Rare chest loot\n• 4-6 drops\n• Guaranteed rare items\n• High value loot",
                LootTableTemplate.ResourceNode => "Resource node drops\n• 1-3 drops\n• Crafting materials\n• Quantity variation",
                LootTableTemplate.Custom => "Empty loot table\n• Customize all fields manually\n• No pre-configured entries",
                _ => ""
            };

            EditorGUILayout.HelpBox(description, MessageType.None);
        }

        private void CreateLootTable()
        {
            // Ensure save path exists
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            // Create asset
            LootTableAsset lootTable = CreateInstance<LootTableAsset>();
            lootTable.tableId = "loot_" + tableName.ToLower().Replace(" ", "_");
            lootTable.tableName = tableName;

            // Apply template
            ApplyTemplate(lootTable);

            // Save asset
            string fileName = tableName.Replace(" ", "") + ".asset";
            string assetPath = Path.Combine(savePath, fileName);
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(lootTable, assetPath);
            AssetDatabase.SaveAssets();

            // Add to database
            if (addToDatabase && targetDatabase != null)
            {
                Undo.RecordObject(targetDatabase, "Add Loot Table to Database");
                if (!targetDatabase.lootTables.Contains(lootTable))
                {
                    targetDatabase.lootTables.Add(lootTable);
                    targetDatabase.UpdateStatistics();
                    EditorUtility.SetDirty(targetDatabase);
                }
            }

            // Select and ping
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = lootTable;
            EditorGUIUtility.PingObject(lootTable);

            // Show confirmation
            EditorUtility.DisplayDialog(
                "Loot Table Created",
                $"Loot table '{tableName}' created successfully at:\n{assetPath}",
                "OK");

            // Reset form
            tableName = "New Loot Table";
            selectedTemplate = LootTableTemplate.Custom;
        }

        private void ApplyTemplate(LootTableAsset lootTable)
        {
            switch (selectedTemplate)
            {
                case LootTableTemplate.CommonEnemy:
                    lootTable.minDrops = 1;
                    lootTable.maxDrops = 2;
                    lootTable.allowDuplicates = true;
                    lootTable.entries = new List<LootEntryData>
                    {
                        new LootEntryData { itemId = "gold_coins", weight = 100, dropChance = 0.8f, minQuantity = 5, maxQuantity = 15 },
                        new LootEntryData { itemId = "health_potion_minor", weight = 60, dropChance = 0.5f, minQuantity = 1, maxQuantity = 2 },
                        new LootEntryData { itemId = "leather_scrap", weight = 40, dropChance = 0.3f, minQuantity = 1, maxQuantity = 3 }
                    };
                    lootTable.guaranteedDrops = new List<string> { "exp_orb_small" };
                    break;

                case LootTableTemplate.EliteEnemy:
                    lootTable.minDrops = 2;
                    lootTable.maxDrops = 4;
                    lootTable.allowDuplicates = true;
                    lootTable.entries = new List<LootEntryData>
                    {
                        new LootEntryData { itemId = "gold_coins", weight = 100, dropChance = 1f, minQuantity = 25, maxQuantity = 50 },
                        new LootEntryData { itemId = "health_potion", weight = 80, dropChance = 0.7f, minQuantity = 1, maxQuantity = 3 },
                        new LootEntryData { itemId = "iron_sword", weight = 40, dropChance = 0.4f, minQuantity = 1, maxQuantity = 1 },
                        new LootEntryData { itemId = "enchanted_ring", weight = 20, dropChance = 0.2f, minQuantity = 1, maxQuantity = 1 }
                    };
                    lootTable.guaranteedDrops = new List<string> { "exp_orb_medium" };
                    break;

                case LootTableTemplate.Boss:
                    lootTable.minDrops = 5;
                    lootTable.maxDrops = 8;
                    lootTable.allowDuplicates = false;
                    lootTable.entries = new List<LootEntryData>
                    {
                        new LootEntryData { itemId = "gold_coins", weight = 100, dropChance = 1f, minQuantity = 500, maxQuantity = 1000 },
                        new LootEntryData { itemId = "legendary_weapon", weight = 30, dropChance = 0.8f, minQuantity = 1, maxQuantity = 1 },
                        new LootEntryData { itemId = "epic_armor", weight = 50, dropChance = 1f, minQuantity = 1, maxQuantity = 1 },
                        new LootEntryData { itemId = "rare_gem", weight = 70, dropChance = 0.9f, minQuantity = 2, maxQuantity = 5 }
                    };
                    lootTable.guaranteedDrops = new List<string> { "exp_orb_huge", "boss_soul_fragment" };
                    break;

                case LootTableTemplate.WoodenChest:
                    lootTable.minDrops = 2;
                    lootTable.maxDrops = 4;
                    lootTable.allowDuplicates = false;
                    lootTable.entries = new List<LootEntryData>
                    {
                        new LootEntryData { itemId = "gold_coins", weight = 100, dropChance = 1f, minQuantity = 10, maxQuantity = 30 },
                        new LootEntryData { itemId = "health_potion", weight = 80, dropChance = 0.8f, minQuantity = 1, maxQuantity = 3 },
                        new LootEntryData { itemId = "mana_potion", weight = 60, dropChance = 0.6f, minQuantity = 1, maxQuantity = 2 },
                        new LootEntryData { itemId = "iron_dagger", weight = 40, dropChance = 0.4f, minQuantity = 1, maxQuantity = 1 }
                    };
                    break;

                case LootTableTemplate.GoldenChest:
                    lootTable.minDrops = 4;
                    lootTable.maxDrops = 6;
                    lootTable.allowDuplicates = false;
                    lootTable.entries = new List<LootEntryData>
                    {
                        new LootEntryData { itemId = "gold_coins", weight = 100, dropChance = 1f, minQuantity = 200, maxQuantity = 500 },
                        new LootEntryData { itemId = "rare_gem", weight = 80, dropChance = 1f, minQuantity = 2, maxQuantity = 4 },
                        new LootEntryData { itemId = "enchanted_weapon", weight = 60, dropChance = 0.8f, minQuantity = 1, maxQuantity = 1 },
                        new LootEntryData { itemId = "ancient_artifact", weight = 30, dropChance = 0.5f, minQuantity = 1, maxQuantity = 1 }
                    };
                    lootTable.guaranteedDrops = new List<string> { "rare_gem_ruby" };
                    break;

                case LootTableTemplate.ResourceNode:
                    lootTable.minDrops = 1;
                    lootTable.maxDrops = 3;
                    lootTable.allowDuplicates = true;
                    lootTable.entries = new List<LootEntryData>
                    {
                        new LootEntryData { itemId = "iron_ore", weight = 100, dropChance = 0.9f, minQuantity = 2, maxQuantity = 5 },
                        new LootEntryData { itemId = "copper_ore", weight = 80, dropChance = 0.7f, minQuantity = 3, maxQuantity = 7 },
                        new LootEntryData { itemId = "silver_ore", weight = 40, dropChance = 0.4f, minQuantity = 1, maxQuantity = 3 },
                        new LootEntryData { itemId = "gold_ore", weight = 20, dropChance = 0.2f, minQuantity = 1, maxQuantity = 2 }
                    };
                    break;

                case LootTableTemplate.Custom:
                default:
                    lootTable.minDrops = 1;
                    lootTable.maxDrops = 3;
                    lootTable.allowDuplicates = true;
                    lootTable.entries = new List<LootEntryData>
                    {
                        new LootEntryData { itemId = "item_id", weight = 100, dropChance = 1f, minQuantity = 1, maxQuantity = 1 }
                    };
                    lootTable.guaranteedDrops = new List<string>();
                    break;
            }
        }
    }
}
