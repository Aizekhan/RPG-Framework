using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Quests;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The run's pages between battles: the quest log counts and hands in, the standing reads the reputation, and
    /// the game's words replace the English.
    /// </summary>
    public sealed class PartyRowsTests
    {
        private TurnBasedTestGame _game;

        [TearDown]
        public void TearDown()
        {
            _game?.Dispose();
            PartyRows.Text = null;
        }

        [Test]
        public void TheQuestLog_CountsAndHandsIn_AndTheStandingReadsTheReputation()
        {
            _game = new TurnBasedTestGame();
            _game.EnemiesDefend();
            _game.Settings.CompleteQuestsAutomatically = false;
            _game.Content.Quests.Add(new Quest("hunt", "The Hunt", "",
                new List<IQuestObjective> { new QuestObjective("slay", ObjectiveType.Kill, Goblin, 2, "Goblins") },
                new List<IQuestReward>(), new List<string>(), minimumLevel: 1, isRepeatable: false));
            _game.Settings.StartingQuests.Add("hunt");
            _game.Content.Factions.Add(new RPGFramework.Factions.Faction("village", "The Village", "", RPGFramework.Factions.FactionType.Kingdom,
                defaultReputation: 30));
            var world = _game.Start();

            var quests = PartyRows.Quests(world);
            Assert.That(quests.Select(r => r.Text()), Is.EqualTo(new[] { "<b>The Hunt</b>  Goblins 0/2" }));
            Assert.That(quests[0].Buttons[0].Enabled(), Is.False);
            PlayToTheEnd(world.StartBattle());
            Assert.That(quests[0].Text(), Is.EqualTo("<b>The Hunt</b>  Goblins 2/2"));
            Assert.That(quests[0].Buttons[0].Enabled(), Is.True);
            quests[0].Buttons[0].Click();
            Assert.That(world.CompletedQuests, Is.EqualTo(new[] { "hunt" }));
            Assert.That(PartyRows.Quests(world).Select(r => r.Text()), Is.EqualTo(new[] { "hunt  ✓" }));

            PartyRows.Text = (key, english) => key == "party.standing.Friendly" ? "Дружні" : english;
            Assert.That(PartyRows.Reputation(world).Single().Text(), Is.EqualTo("The Village  <b>Дружні</b> (30)"));
        }
    }
}
