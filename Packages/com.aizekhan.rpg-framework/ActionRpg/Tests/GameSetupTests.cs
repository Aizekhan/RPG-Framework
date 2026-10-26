using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Items;
using RPGFramework.Quests;
using RPGFramework.Tools.ItemDatabaseManager;
using RPGFramework.Tools.LootTableDesigner;
using RPGFramework.Tools.QuestEditor;
using UnityEngine;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The inspector assets turn into the runtime game: characters, the loot tables they reference, items and
    /// quests; mistakes a designer makes are reported in plain words.
    /// </summary>
    public sealed class GameSetupTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
            _created.Clear();
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }

        private ItemAsset Item(string id)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemAsset>());
            item.itemId = id;
            item.itemName = id;
            item.itemType = ItemType.Crafting;
            item.maxStackSize = 99;
            return item;
        }

        private LootTableAsset Loot(string id, string drops)
        {
            var table = Track(ScriptableObject.CreateInstance<LootTableAsset>());
            table.tableId = id;
            table.tableName = id;
            table.entries.Clear();
            table.guaranteedDrops.Add(drops);
            table.minDrops = 0;
            table.maxDrops = 0;
            return table;
        }

        private QuestAsset KillQuest(string id, string target)
        {
            var quest = Track(ScriptableObject.CreateInstance<QuestAsset>());
            quest.questId = id;
            quest.title = id;
            quest.objectives = new[]
            {
                new QuestObjectiveEntry { objectiveId = "kill", type = ObjectiveType.Kill, targetId = target, requiredCount = 3 }
            };
            quest.rewards = new[] { new QuestRewardEntry { type = RewardType.Experience, rewardId = "xp", amount = 50 } };
            return quest;
        }

        private GameSetup GoodSetup(out CharacterDefinition goblin)
        {
            var hero = Track(CharacterDefinition.Create("hero", "Hero", 100f, 12f, 5f));
            goblin = Track(CharacterDefinition.Create("goblin", "Goblin", 30f, 6f, 2f, 40, loot: Loot("goblin_loot", "ear")));
            var setup = Track(GameSetup.Create(hero, new[] { goblin }));
            setup.Items.Add(Item("ear"));
            setup.StartingQuests.Add(KillQuest("hunt", "goblin"));
            return setup;
        }

        [Test]
        public void GoodSetup_IsReadyToPlay()
        {
            var setup = GoodSetup(out _);

            Assert.That(setup.Validate(), Is.Empty);
        }

        [Test]
        public void BuildSettings_CarriesCharactersAndStartingQuests()
        {
            var settings = GoodSetup(out _).BuildSettings();

            Assert.That(settings.Player.Id, Is.EqualTo("hero"));
            Assert.That(settings.Enemies.Single().LootTableId, Is.EqualTo("goblin_loot"));
            Assert.That(settings.Enemies.Single().ExperienceReward, Is.EqualTo(40));
            Assert.That(settings.StartingQuests, Is.EqualTo(new[] { "hunt" }));
        }

        [Test]
        public void BuildContent_IncludesLootTablesOfCharacters()
        {
            var content = GoodSetup(out _).BuildContent();

            Assert.That(content.LootTables.Select(t => t.TableId), Is.EqualTo(new[] { "goblin_loot" }));
            Assert.That(content.Items.Select(i => i.ItemId), Is.EqualTo(new[] { "ear" }));
            Assert.That(content.Quests.Single().Objectives.Single().TargetId, Is.EqualTo("goblin"));
        }

        [Test]
        public void MissingItemAndUnknownTarget_AreReported()
        {
            var setup = GoodSetup(out _);
            setup.Items.Clear();
            setup.StartingQuests.Add(KillQuest("dragons", "dragon"));

            var problems = setup.Validate();

            Assert.That(problems, Has.Some.Contains("'ear'"));
            Assert.That(problems, Has.Some.Contains("'dragon'"));
        }

        [Test]
        public void EmptySlotsAndPlayerAsEnemy_AreReported()
        {
            var setup = GoodSetup(out _);
            setup.AddEnemy(setup.Player);
            setup.Items.Add(null);

            var problems = setup.Validate();

            Assert.That(problems, Has.Some.Contains("both the player and an enemy"));
            Assert.That(problems, Has.Some.Contains("item list has an empty slot"));
        }

        [Test]
        public void CharacterWithoutId_UsesItsAssetName()
        {
            var character = Track(ScriptableObject.CreateInstance<CharacterDefinition>());
            character.name = "Cave Troll";

            Assert.That(character.Id, Is.EqualTo("cave_troll"));
            Assert.That(character.ToData().DisplayName, Is.EqualTo("Cave Troll"));
        }
    }
}
