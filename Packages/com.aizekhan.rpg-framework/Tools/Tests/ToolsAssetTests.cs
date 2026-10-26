using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Items;
using RPGFramework.Quests;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Tools.LootTableDesigner;
using RPGFramework.Tools.QuestEditor;
using UnityEngine;

namespace RPGFramework.Tools.Tests
{
    /// <summary>
    /// The designer assets (items, loot tables, quests and their databases) become exactly the framework objects the
    /// game runs on, and their checks catch what would break a game: a missing id, a stackable sword, a loot entry that
    /// can never drop, a quest chain that loops. Assets made in code (tests, generators) convert as well as ones made
    /// in the inspector, and an empty slot in a list never locks a quest.
    /// </summary>
    public sealed class ToolsAssetTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _made)
                Object.DestroyImmediate(asset);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _made.Add(asset);
            return asset;
        }

        private ItemAsset Item(string id, ItemType type = ItemType.Crafting, int stack = 10)
        {
            var item = Make<ItemAsset>();
            item.itemId = id;
            item.itemName = id;
            item.itemType = type;
            item.maxStackSize = stack;
            return item;
        }

        private QuestAsset Quest(string id, params QuestAsset[] prerequisites)
        {
            var quest = Make<QuestAsset>();
            quest.questId = id;
            quest.title = id;
            quest.objectives = new[] { new QuestObjectiveEntry() };
            quest.rewards = new[] { new QuestRewardEntry() };
            quest.prerequisiteQuests = prerequisites;
            return quest;
        }

        // ---- Items ----

        [Test]
        public void AnEquipmentAsset_BecomesAnUnstackableItem_WithItsDetails()
        {
            var sword = Item("iron_sword", ItemType.Equipment, stack: 1);
            sword.rarity = ItemRarity.Rare;
            sword.itemLevel = 5;
            sword.iconId = "icon_sword";
            sword.description = "Sharp";

            var item = sword.ToItem();

            Assert.That(item.ItemId, Is.EqualTo("iron_sword"));
            Assert.That(item.ItemType, Is.EqualTo(ItemType.Equipment));
            Assert.That(item.MaxStackSize, Is.EqualTo(1));
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(item.ItemLevel, Is.EqualTo(5));
            Assert.That(item.IconId, Is.EqualTo("icon_sword"));
            Assert.That(item.Description, Is.EqualTo("Sharp"));
        }

        [Test]
        public void AConsumableAsset_KeepsItsEffectCooldownAndUse()
        {
            var potion = Item("potion", ItemType.Consumable, stack: 20);
            potion.effectId = "heal";
            potion.effectMagnitude = 25f;
            potion.cooldown = 3f;
            potion.consumeOnUse = false;

            var consumable = potion.ToItem() as IConsumable;

            Assert.That(consumable, Is.Not.Null);
            Assert.That(consumable.EffectId, Is.EqualTo("heal"));
            Assert.That(consumable.EffectMagnitude, Is.EqualTo(25f));
            Assert.That(consumable.CooldownDuration, Is.EqualTo(3f));
            Assert.That(consumable.IsConsumedOnUse, Is.False);
            Assert.That(consumable.MaxStackSize, Is.EqualTo(20));
        }

        [Test]
        public void OtherItems_KeepTheirStackSize()
        {
            var item = Item("wolf_pelt", ItemType.Crafting, stack: 50).ToItem();

            Assert.That(item.ItemType, Is.EqualTo(ItemType.Crafting));
            Assert.That(item.MaxStackSize, Is.EqualTo(50));
        }

        [Test]
        public void ItemChecks_FindWhatWouldBreakTheGame()
        {
            Assert.That(Item("fine").Validate(), Is.Empty);

            var broken = Item(" ", ItemType.Equipment, stack: 5);
            broken.itemName = "";
            broken.equipmentSlot = "";
            var errors = broken.Validate();

            Assert.That(errors, Has.Some.Contains("Item ID"));
            Assert.That(errors, Has.Some.Contains("Item Name"));
            Assert.That(errors, Has.Some.Contains("Equipment Slot"));
            Assert.That(errors, Has.Some.Contains("stackable"));
        }

        [Test]
        public void AnItemDatabase_FindsItems_AndReportsDuplicatesAndEmptySlots()
        {
            var database = Make<ItemDatabase>();
            var pelt = Item("wolf_pelt");
            database.items = new List<ItemAsset> { pelt, Item("potion", ItemType.Consumable), null, Item("wolf_pelt") };

            Assert.That(database.GetItem("potion").itemId, Is.EqualTo("potion"));
            Assert.That(database.GetItem("missing"), Is.Null);
            Assert.That(database.GetItemsByType(ItemType.Consumable).Count, Is.EqualTo(1));

            var errors = database.ValidateDatabase();
            Assert.That(errors, Has.Some.Contains("Duplicate item IDs: wolf_pelt"));
            Assert.That(errors, Has.Some.Contains("1 null item"));

            database.items.RemoveAt(3);
            database.RemoveNullEntries();
            Assert.That(database.ValidateDatabase(), Is.Empty);
            Assert.That(database.ToItemRegistry().GetItem("wolf_pelt").ItemId, Is.EqualTo("wolf_pelt"));
        }

        // ---- Loot ----

        [Test]
        public void ALootTableAsset_BecomesTheTableTheGameRolls()
        {
            var asset = Make<LootTableAsset>();
            asset.tableId = "wolf";
            asset.tableName = "Wolf";
            asset.minDrops = 1;
            asset.maxDrops = 2;
            asset.allowDuplicates = false;
            asset.entries = new List<LootEntryData>
            {
                new LootEntryData { itemId = "wolf_pelt", weight = 75, dropChance = 0.5f, minQuantity = 1, maxQuantity = 3, minLevel = 2, maxLevel = 9 },
                new LootEntryData { itemId = "fang", weight = 25 },
                new LootEntryData { itemId = " " }, // an entry the designer has not filled in yet
                null
            };
            asset.guaranteedDrops = new List<string> { "bone" };

            var table = asset.ToLootTable();

            Assert.That(table.TableId, Is.EqualTo("wolf"));
            Assert.That(table.MaxDrops, Is.EqualTo(2));
            Assert.That(table.AllowDuplicates, Is.False);
            Assert.That(table.GuaranteedDrops, Is.EqualTo(new[] { "bone" }));
            Assert.That(table.Entries.Select(e => e.ItemId), Is.EqualTo(new[] { "wolf_pelt", "fang" }));
            var pelt = table.Entries[0];
            Assert.That(new object[] { pelt.Weight, pelt.DropChance, pelt.MinQuantity, pelt.MaxQuantity, pelt.MinLevel, pelt.MaxLevel },
                Is.EqualTo(new object[] { 75, 0.5f, 1, 3, 2, 9 }));
            Assert.That(asset.GetDropRatePercentage(asset.entries[0]), Is.EqualTo(75f).Within(0.01f));
        }

        [Test]
        public void LootChecks_FindEntriesThatCanNeverDrop()
        {
            var asset = Make<LootTableAsset>();
            asset.tableId = "chest";
            asset.tableName = "Chest";
            asset.entries = new List<LootEntryData> { new LootEntryData { itemId = "gold" } };
            Assert.That(asset.Validate(), Is.Empty);

            asset.entries = new List<LootEntryData>
            {
                new LootEntryData { itemId = "gold", minLevel = 10, maxLevel = 5 }, // no level is both
                new LootEntryData { itemId = "gem", minQuantity = 3, maxQuantity = 1 },
                new LootEntryData { itemId = "gem", dropChance = 1.5f }
            };
            asset.minDrops = 3;
            asset.maxDrops = 2;
            var errors = asset.Validate();

            Assert.That(errors, Has.Some.Contains("Max level"));
            Assert.That(errors, Has.Some.Contains("Max quantity"));
            Assert.That(errors, Has.Some.Contains("Drop chance"));
            Assert.That(errors, Has.Some.Contains("Duplicate item IDs found: gem"));
            Assert.That(errors, Has.Some.Contains("Max Drops"));
        }

        [Test]
        public void ALootTableDatabase_FindsTables_AndReportsDuplicates()
        {
            var database = Make<LootTableDatabase>();
            var wolf = Make<LootTableAsset>();
            wolf.tableId = "wolf";
            var copy = Make<LootTableAsset>();
            copy.tableId = "wolf";
            database.lootTables = new List<LootTableAsset> { wolf, copy };

            Assert.That(database.GetLootTable("wolf"), Is.SameAs(wolf));
            Assert.That(database.ValidateDatabase(), Has.Some.Contains("Duplicate table IDs: wolf"));
        }

        // ---- Quests ----

        [Test]
        public void AQuestAsset_BecomesTheQuestTheGameRuns()
        {
            var first = Quest("first_steps");
            var second = Quest("into_the_woods", first);
            second.minimumLevel = 3;
            second.isRepeatable = true;
            second.objectives = new[]
            {
                new QuestObjectiveEntry { objectiveId = "wolves", type = ObjectiveType.Kill, targetId = "wolf", requiredCount = 5, isOptional = true }
            };
            second.rewards = new[] { new QuestRewardEntry { type = RewardType.Currency, rewardId = "gold", amount = 50 } };

            var data = second.ToQuestData();

            Assert.That(data.Id, Is.EqualTo("into_the_woods"));
            Assert.That(data.MinimumLevel, Is.EqualTo(3));
            Assert.That(data.IsRepeatable, Is.True);
            Assert.That(data.PrerequisiteQuestIds, Is.EqualTo(new[] { "first_steps" }));
            Assert.That(data.Objectives.Single().TargetId, Is.EqualTo("wolf"));
            Assert.That(data.Objectives.Single().RequiredCount, Is.EqualTo(5));
            Assert.That(data.Objectives.Single().IsOptional, Is.True);
            Assert.That(data.Rewards.Single().Amount, Is.EqualTo(50));
            Assert.That(new Quest(data).Id, Is.EqualTo("into_the_woods"));
        }

        [Test]
        public void AQuestAssetMadeInCode_Converts()
        {
            // Not opened in the inspector: its first objective and reward slots are still empty
            var quest = Make<QuestAsset>();

            var data = quest.ToQuestData();

            Assert.That(data.Objectives, Is.Empty);
            Assert.That(data.Rewards, Is.Empty);
            Assert.That(data.PrerequisiteQuestIds, Is.Empty);
        }

        [Test]
        public void AnEmptyPrerequisiteSlot_DoesNotLockTheQuestForever()
        {
            var first = Quest("first_steps");
            var second = Quest("second", null, first); // the designer added a slot and left it empty

            Assert.That(second.ToQuestData().PrerequisiteQuestIds, Is.EqualTo(new[] { "first_steps" }));
        }

        [Test]
        public void AQuestDatabase_FindsLoopsMissingPrerequisitesAndDuplicates()
        {
            var database = Make<QuestDatabase>();
            var a = Quest("a");
            var b = Quest("b", a);
            a.prerequisiteQuests = new[] { b };
            var outsider = Quest("outsider");
            var c = Quest("c", outsider);
            database.quests = new List<QuestAsset> { a, b, c, Quest("c") };

            var errors = database.ValidateDatabase();

            Assert.That(errors, Has.Some.Contains("Circular dependency detected for quest: a"));
            Assert.That(errors, Has.Some.Contains("Circular dependency detected for quest: b"));
            Assert.That(errors, Has.None.Contains("Circular dependency detected for quest: c"));
            Assert.That(errors, Has.Some.Contains("'outsider' not in database"));
            Assert.That(errors, Has.Some.Contains("Duplicate quest ID: c"));
            Assert.That(database.GetDependentQuests(a), Is.EqualTo(new[] { b }));
            Assert.That(database.IsQuestIdUnique("c"), Is.False);
            Assert.That(database.IsQuestIdUnique("d"), Is.True);
        }

        [Test]
        public void ALongQuestChainWithBranches_IsCheckedQuickly()
        {
            // 20 chapters of two quests each, every quest needing both quests of the chapter before
            var database = Make<QuestDatabase>();
            var previous = new[] { Quest("start") };
            database.quests.Add(previous[0]);
            for (int chapter = 1; chapter <= 20; chapter++)
            {
                var current = new[] { Quest($"c{chapter}_main", previous), Quest($"c{chapter}_side", previous) };
                database.quests.AddRange(current);
                previous = current;
            }

            var clock = Stopwatch.StartNew();
            var errors = database.ValidateDatabase();
            clock.Stop();

            Assert.That(errors, Is.Empty);
            Assert.That(clock.ElapsedMilliseconds, Is.LessThan(500), "an editor check must not freeze the editor");
        }
    }
}
