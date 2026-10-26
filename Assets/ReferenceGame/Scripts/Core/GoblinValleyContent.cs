using System.Collections.Generic;
using RPGFramework.Items;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.Quests;

namespace ReferenceGame
{
    /// <summary>
    /// Game data of Goblin Valley. A real game would load this from ScriptableObjects or JSON; the registration calls
    /// stay the same.
    /// </summary>
    public static class GoblinValleyContent
    {
        public const string PlayerKind = "player";
        public const string GoblinKind = "goblin";

        public const string GoblinEar = "goblin_ear";
        public const string RustyDagger = "rusty_dagger";
        public const string ValleyMedal = "valley_medal";

        public const string GoblinLootTable = "goblin";
        public const string ClearTheValley = "clear_the_valley";
        public const int GoblinsToClear = 3;

        public const long GoblinKillXp = 40;
        public const long QuestXp = 150;

        public const float GoblinHealth = 30f;
        public const float GoblinAttack = 6f;
        public const float GoblinDefense = 2f;

        public static void RegisterItems(IItemRegistry items)
        {
            items.RegisterItem(new Item(GoblinEar, "Goblin Ear", "Proof of a slain goblin", ItemType.Crafting, maxStackSize: 99));
            items.RegisterItem(new Item(RustyDagger, "Rusty Dagger", "Better than nothing", ItemType.Equipment));
            items.RegisterItem(new Item(ValleyMedal, "Valley Medal", "Awarded for clearing Goblin Valley", ItemType.Quest,
                ItemRarity.Rare));
        }

        public static void RegisterLoot(ILootTableRegistry loot)
        {
            loot.RegisterTable(LootTableBuilder.Create(GoblinLootTable, "Goblin")
                .AddGuaranteedDrop(GoblinEar)
                .WithDrops(0, 1)
                .AddEntry(RustyDagger, dropChance: 0.25f)
                .Build());
        }

        public static void RegisterQuests(IQuestRegistry quests)
        {
            quests.RegisterQuest(new Quest(
                ClearTheValley,
                "Clear the Valley",
                $"Slay {GoblinsToClear} goblins.",
                new List<IQuestObjective>
                {
                    new QuestObjective("slay_goblins", ObjectiveType.Kill, GoblinKind, GoblinsToClear, $"Slay {GoblinsToClear} goblins")
                },
                new List<IQuestReward>
                {
                    new QuestReward(RewardType.Experience, null, (int)QuestXp),
                    new QuestReward(RewardType.Item, ValleyMedal, 1)
                },
                new List<string>(),
                minimumLevel: 1,
                isRepeatable: false));
        }

        /// <summary>
        /// Player stat growth; <see cref="StatGrowthPublisher"/> turns level-ups into <see cref="StatIncreasedEvent"/>s.
        /// Stat ids are <see cref="CombatStat"/> names.
        /// </summary>
        public static void RegisterStatGrowth(IStatGrowthFormula formula)
        {
            formula.RegisterStatGrowth(new StatGrowthData(nameof(CombatStat.MaxHealth), 100f, 20f));
            formula.RegisterStatGrowth(new StatGrowthData(nameof(CombatStat.Attack), 12f, 3f));
            formula.RegisterStatGrowth(new StatGrowthData(nameof(CombatStat.Defense), 5f, 2f));
        }
    }
}
