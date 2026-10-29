using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Quests;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The party's quests in a run: beaten enemies, items got and won encounters advance them, the rewards are paid
    /// once (experience to everyone standing, gold, items), a new run takes the starting quests again and a save
    /// keeps them.
    /// </summary>
    public sealed class TurnBasedQuestsTests
    {
        private const string Hunt = "hunt";
        private const string Cull = "cull";
        private const string Clear = "clear";

        private readonly List<TurnBasedTestGame> _games = new List<TurnBasedTestGame>();
        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "RPGFrameworkTests_TurnBasedQuests_" + Guid.NewGuid());

        [TearDown]
        public void TearDown()
        {
            foreach (var game in _games)
                game.Dispose();
            _games.Clear();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private TurnBasedTestGame NewGame()
        {
            var game = new TurnBasedTestGame();
            game.EnemiesDefend();
            game.Content.Quests.Add(new Quest(Hunt, "The Hunt", "Two goblins and their ears",
                new List<IQuestObjective>
                {
                    new QuestObjective("slay", ObjectiveType.Kill, Goblin, 2, "Slay goblins"),
                    new QuestObjective("ears", ObjectiveType.Collect, Ear, 2, "Bring their ears")
                },
                new List<IQuestReward>
                {
                    new QuestReward(RewardType.Experience, null, 30), new QuestReward(RewardType.Currency, null, 15),
                    new QuestReward(RewardType.Item, Potion, 1)
                },
                new List<string>(), minimumLevel: 1, isRepeatable: false));
            game.Content.Quests.Add(new Quest(Cull, "The Cull", "Five goblins",
                new List<IQuestObjective> { new QuestObjective("slay", ObjectiveType.Kill, Goblin, 5, "Slay goblins") },
                new List<IQuestReward>(), new List<string>(), minimumLevel: 1, isRepeatable: false));
            game.Content.Quests.Add(new Quest(Clear, "Clear the Pack", "Win at the goblin pack",
                new List<IQuestObjective> { new QuestObjective("win", ObjectiveType.Explore, "pack", 1, "Beat the pack") },
                new List<IQuestReward> { new QuestReward(RewardType.Currency, null, 7) },
                new List<string>(), minimumLevel: 1, isRepeatable: false));
            game.Settings.StartingQuests.AddRange(new[] { Hunt, Cull });
            _games.Add(game);
            return game;
        }

        private static int ProgressOf(TurnBasedWorld world, string questId, string objectiveId) =>
            world.ActiveQuests.Single(q => q.Quest.Id == questId).Progress.ObjectiveProgress.TryGetValue(objectiveId, out int done) ? done : 0;

        [Test]
        public void KillsAndLoot_CompleteAQuest_AndItsRewardsArePaidOnce()
        {
            var game = NewGame();
            var world = game.Start();
            Assert.That(world.HasQuests, Is.True);
            Assert.That(world.ActiveQuests.Select(q => q.Quest.Id), Is.EquivalentTo(new[] { Hunt, Cull }));

            PlayToTheEnd(world.StartBattle());

            Assert.That(world.CompletedQuests, Is.EqualTo(new[] { Hunt }));
            Assert.That(ProgressOf(world, Cull, "slay"), Is.EqualTo(2));
            Assert.That(world.Gold, Is.EqualTo(20 + 10 + 15), "starting, the goblins', the quest's");
            Assert.That(world.CountOf(Potion), Is.EqualTo(3));
            Assert.That(world.Party.Select(m => world.ExperienceOf(m.Id)), Is.All.EqualTo(100 + 30));
            Assert.That(world.Log.Any(line => line.Contains("Quest complete: The Hunt")), Is.True);
        }

        [Test]
        public void AWonEncounter_AdvancesExplore_AndAHandInWaitsForTheGame()
        {
            var game = NewGame();
            game.Settings.CompleteQuestsAutomatically = false;
            var world = game.Start();
            Assert.That(world.AcceptQuest(Clear), Is.True);
            Assert.That(world.AcceptQuest(Clear), Is.False, "taken already");
            Assert.That(world.AcceptQuest("nope"), Is.False);

            PlayToTheEnd(world.StartBattle());

            Assert.That(world.CompletedQuests, Is.Empty);
            Assert.That(ProgressOf(world, Clear, "win"), Is.EqualTo(1));
            Assert.That(world.CompleteQuest(Cull), Is.False, "two of five");
            Assert.That(world.CompleteQuest(Clear), Is.True);
            Assert.That(world.CompleteQuest(Hunt), Is.True);
            Assert.That(world.Gold, Is.EqualTo(20 + 10 + 7 + 15));
            Assert.That(world.ReportQuestProgress(ObjectiveType.Kill, Goblin, 3), Is.EqualTo(1));
            Assert.That(world.CompleteQuest(Cull), Is.True);
        }

        [Test]
        public void ANewRun_TakesTheStartingQuestsAgain_ButNotTheDoneOnes()
        {
            var game = NewGame();
            var world = game.Start();
            PlayToTheEnd(world.StartBattle());
            Assume.That(world.CompletedQuests, Is.EqualTo(new[] { Hunt }));

            world.Restart();

            Assert.That(world.ActiveQuests.Select(q => q.Quest.Id), Is.EqualTo(new[] { Cull }));
            Assert.That(ProgressOf(world, Cull, "slay"), Is.EqualTo(0));
            Assert.That(world.CompletedQuests, Is.EqualTo(new[] { Hunt }));
        }

        [Test]
        public void ASave_KeepsTheQuests_WithoutPayingAgain()
        {
            var first = NewGame();
            var world = first.Start();
            PlayToTheEnd(world.StartBattle());
            int gold = world.Gold;
            Assert.That(new TurnBasedSaves(world, _directory).Save(), Is.Null);

            var second = NewGame();
            var restored = second.Start();
            Assert.That(new TurnBasedSaves(restored, _directory).Load(), Is.Null);

            Assert.That(restored.CompletedQuests, Is.EqualTo(new[] { Hunt }));
            Assert.That(restored.ActiveQuests.Select(q => q.Quest.Id), Is.EqualTo(new[] { Cull }));
            Assert.That(ProgressOf(restored, Cull, "slay"), Is.EqualTo(2));
            Assert.That(restored.Gold, Is.EqualTo(gold), "the hunt's gold is not paid again");
            Assert.That(restored.CountOf(Potion), Is.EqualTo(world.CountOf(Potion)));
        }
    }
}
