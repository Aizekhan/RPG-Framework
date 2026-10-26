using System.Collections.Generic;
using System.IO;
using RPGFramework.ActionRpg;
using RPGFramework.Items;
using RPGFramework.Quests;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Tools.LootTableDesigner;
using RPGFramework.Tools.QuestEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGFramework.GameKit.Editor
{
    /// <summary>
    /// The content a new game starts with, as ordinary assets a designer edits in the inspector: a hero, a goblin,
    /// two items, the goblin's loot table, a first quest, the Game Setup that ties them together and a ground
    /// material. The characters have no prefabs, so they show as capsules until a model is dragged in.
    /// </summary>
    public static class StarterContent
    {
        public const string SetupReferenceInScene = "_setup: {fileID: 0}";
        public const string GroundMaterialInScene = "{fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}";

        /// <summary>
        /// Creates the assets under <c>Assets/&lt;game&gt;/Content</c>.
        /// </summary>
        public static GameSetup Create(string game, out Material ground)
        {
            string root = $"Assets/{game}/Content";
            foreach (var folder in new[] { "Characters", "Items", "Loot", "Quests", "Materials" })
                EnsureFolder($"{root}/{folder}");

            var ear = Save(Item("goblin_ear", "Goblin Ear", "Proof of a slain goblin", ItemType.Crafting, 99), $"{root}/Items/GoblinEar.asset");
            var dagger = Save(Item("rusty_dagger", "Rusty Dagger", "Better than nothing", ItemType.Equipment, 1), $"{root}/Items/RustyDagger.asset");

            var loot = ScriptableObject.CreateInstance<LootTableAsset>();
            loot.tableId = "goblin_loot";
            loot.tableName = "Goblin";
            loot.minDrops = 0;
            loot.maxDrops = 1;
            loot.entries = new List<LootEntryData> { new LootEntryData { itemId = dagger.itemId, weight = 100, dropChance = 0.25f } };
            loot.guaranteedDrops = new List<string> { ear.itemId };
            Save(loot, $"{root}/Loot/GoblinLoot.asset");

            var hero = CharacterDefinition.Create("hero", "Hero", 120f, 14f, 4f);
            hero.SetGrowth(20f, 3f, 1f);
            hero.SetMovement(6f, 2.4f, 0.4f);
            hero.SetPlaceholderColor(new Color(0.25f, 0.5f, 0.95f));
            Save(hero, $"{root}/Characters/Hero.asset");

            var goblin = CharacterDefinition.Create("goblin", "Goblin", 30f, 6f, 2f, 40, loot: loot);
            goblin.SetMovement(3.2f, 1.6f, 1.2f);
            goblin.SetPlaceholderColor(new Color(0.35f, 0.7f, 0.25f));
            Save(goblin, $"{root}/Characters/Goblin.asset");

            var quest = ScriptableObject.CreateInstance<QuestAsset>();
            quest.questId = "first_hunt";
            quest.title = "First Hunt";
            quest.description = "Goblins are raiding the fields. Slay five of them.";
            quest.objectives = new[]
            {
                new QuestObjectiveEntry
                {
                    objectiveId = "slay_goblins", type = ObjectiveType.Kill, targetId = goblin.Id, requiredCount = 5,
                    description = "Slay goblins"
                }
            };
            quest.rewards = new[] { new QuestRewardEntry { type = RewardType.Experience, rewardId = "xp", amount = 150 } };
            Save(quest, $"{root}/Quests/FirstHunt.asset");

            var setup = GameSetup.Create(hero, new[] { goblin });
            setup.Items.Add(ear);
            setup.Items.Add(dagger);
            setup.StartingQuests.Add(quest);
            Save(setup, $"{root}/{game}Setup.asset");

            ground = Save(GroundMaterial(), $"{root}/Materials/Ground.mat");
            AssetDatabase.SaveAssets();
            return setup;
        }

        /// <summary>
        /// Points the scene's host at the setup and its ground at the material (scene text written by the scaffold).
        /// </summary>
        public static void LinkScene(string scenePath, GameSetup setup, Material ground)
        {
            string text = File.ReadAllText(scenePath);
            text = text.Replace(SetupReferenceInScene, $"_setup: {Reference(setup, 11400000)}");
            if (ground != null)
                text = text.Replace(GroundMaterialInScene, Reference(ground, 2100000));
            File.WriteAllText(scenePath, text);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
        }

        private static string Reference(Object asset, long fileId) =>
            $"{{fileID: {fileId}, guid: {AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))}, type: 2}}";

        private static ItemAsset Item(string id, string name, string description, ItemType type, int stack)
        {
            var item = ScriptableObject.CreateInstance<ItemAsset>();
            item.itemId = id;
            item.itemName = name;
            item.description = description;
            item.itemType = type;
            item.maxStackSize = stack;
            return item;
        }

        private static Material GroundMaterial()
        {
            // The render pipeline's own default shader, so the ground is not pink in URP or HDRP projects
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var shader = pipeline != null && pipeline.defaultMaterial != null ? pipeline.defaultMaterial.shader : Shader.Find("Standard");
            var material = new Material(shader);
            var color = new Color(0.36f, 0.5f, 0.28f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            return material;
        }

        private static T Save<T>(T asset, string path) where T : Object
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (parent != null)
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
