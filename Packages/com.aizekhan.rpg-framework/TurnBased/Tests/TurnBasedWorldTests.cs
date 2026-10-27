using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Abilities;
using RPGFramework.Items;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The run around the battles, through the real modules: rewards on victory (experience through Progression,
    /// level-ups that grow the stats, gold, loot through Loot into the inventory), rest, defeat ending the run, a
    /// whole run of three encounters, and the order of the events a view listens to.
    /// </summary>
    public sealed class TurnBasedWorldTests
    {
        private TurnBasedTestGame _game;

        [SetUp]
        public void SetUp() => _game = new TurnBasedTestGame();

        [TearDown]
        public void TearDown() => _game.Dispose();

        [Test]
        public void Start_CreatesTheParty_WithItsItems_AndRegistersTheContent()
        {
            var world = _game.Start();

            Assert.That(world.Party.Select(m => m.Name), Is.EqualTo(new[] { "Knight", "Mage", "Cleric" }));
            Assert.That(world.Party.All(m => m.Level == 1 && m.Health == m.MaxHealth && m.Mana == m.MaxMana), Is.True);
            Assert.That(world.Inventory.Count, Is.EqualTo(7));
            Assert.That(world.Gold, Is.EqualTo(20));
            Assert.That(world.NextEncounter.Id, Is.EqualTo("pack"));
            Assert.That(_game.Host.Container.Resolve<IAbilityRegistry>().HasAbility(Fire), Is.True);
            Assert.That(_game.Host.Container.Resolve<IItemRegistry>().HasItem(Potion), Is.True);
            Assert.That(world.StatusEffects.GetEffect(Poison), Is.Not.Null);
            Assert.That(_game.Settings.Validate(_game.Content), Is.Empty);
        }

        [Test]
        public void Validate_NamesTheMistakes()
        {
            _game.Settings.Encounters[0].EnemyIds.Add("dragon");
            _game.MageData.AbilityIds.Add("meteor");
            _game.GoblinData.LootTableId = "nothing";

            var problems = _game.Settings.Validate(_game.Content);

            Assert.That(problems, Does.Contain("Encounter 'Goblin Pack' lists 'dragon', which is not an enemy in the setup."));
            Assert.That(problems, Does.Contain("Mage knows 'meteor', which is not an ability in the setup."));
            Assert.That(problems, Does.Contain("Goblin: loot table 'nothing' is not in the setup."));
        }

        [Test]
        public void Victory_GivesExperienceToEveryone_LevelsThemUp_AndPaysGoldAndLoot()
        {
            _game.EnemiesDefend();
            var world = _game.Start();
            var ended = new List<BattleEndedEvent>();
            var levelUps = new List<MemberLevelUpEvent>();
            _game.Bus.Subscribe<BattleEndedEvent>(ended.Add);
            _game.Bus.Subscribe<MemberLevelUpEvent>(levelUps.Add);
            var knight = _game.Member(Knight);

            var battle = world.StartBattle();
            PlayToTheEnd(battle);

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            var rewards = ended.Single().Rewards;
            Assert.That(rewards.Experience, Is.EqualTo(100));
            Assert.That(rewards.Gold, Is.EqualTo(10));
            Assert.That(rewards.Items, Is.EqualTo(new[] { Ear, Ear }));
            Assert.That(rewards.LeveledUp.Count, Is.EqualTo(3));

            Assert.That(world.Party.Select(m => world.ExperienceOf(m.Id)), Is.EqualTo(new[] { 100L, 100L, 100L }));
            Assert.That(world.Party.Select(m => m.Level), Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(levelUps.Select(l => l.NewLevel), Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(knight.MaxHealth, Is.EqualTo(140));
            Assert.That(knight.Health, Is.EqualTo(140), "health grows with the maximum");
            Assert.That(knight.Attack, Is.EqualTo(23f));
            Assert.That(world.Gold, Is.EqualTo(30));
            Assert.That(world.CountOf(Ear), Is.EqualTo(2));
            Assert.That(world.EncounterIndex, Is.EqualTo(1));
            Assert.That(world.NextEncounter.Id, Is.EqualTo("camp"));
            Assert.That(world.IsInBattle, Is.False);
        }

        [Test]
        public void Victory_GivesNoExperienceToTheFallen_AndTheyStayDownWithoutRest()
        {
            _game.MageData.MaxHealth = 5;
            _game.EnemiesAttack(Mage);
            var world = _game.Start();
            var mage = _game.Member(Mage);

            PlayToTheEnd(world.StartBattle());

            Assert.That(world.ExperienceOf(mage.Id), Is.EqualTo(0L));
            Assert.That(mage.Level, Is.EqualTo(1));
            Assert.That(world.ExperienceOf(_game.Member(Knight).Id), Is.EqualTo(100L));
            Assert.That(mage.IsAlive, Is.False);

            var next = world.StartBattle();
            Assert.That(next.TurnOrder, Has.No.Member(mage));
        }

        [Test]
        public void RestAfterVictory_GetsTheFallenUp_AndGivesBackAShare()
        {
            _game.MageData.MaxHealth = 10;
            _game.Settings.RestAfterVictory = 0.5f;
            _game.EnemiesAttack(Mage);
            var world = _game.Start();
            var mage = _game.Member(Mage);
            var cleric = _game.Member(Cleric);

            PlayToTheEnd(world.StartBattle());

            Assert.That(mage.IsAlive, Is.True);
            Assert.That(mage.Health, Is.EqualTo(5));
            Assert.That(cleric.Health, Is.EqualTo(cleric.MaxHealth));
        }

        [Test]
        public void Defeat_EndsTheRun_UntilItIsRestarted()
        {
            foreach (var hero in _game.Settings.Party)
                hero.MaxHealth = 1;
            var world = _game.Start();
            var ended = new List<BattleEndedEvent>();
            _game.Bus.Subscribe<BattleEndedEvent>(ended.Add);

            var battle = world.StartBattle();
            for (int turn = 0; turn < 20 && !battle.IsOver; turn++)
                Expect(battle.Perform(BattleAction.Defend()));

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(ended.Single().Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(ended.Single().Rewards.Experience, Is.EqualTo(0L));
            Assert.That(world.IsRunOver, Is.True);
            Assert.That(world.WhyCannotStartBattle(), Is.EqualTo("The party was defeated; restart the run."));
            Assert.Throws<InvalidOperationException>(() => world.StartBattle());

            world.Restart();

            Assert.That(world.IsRunOver, Is.False);
            Assert.That(world.Party.All(m => m.IsAlive && m.Level == 1), Is.True);
            Assert.That(world.EncounterIndex, Is.EqualTo(0));
            Assert.That(world.Inventory.Count, Is.EqualTo(7));
            Assert.That(world.Gold, Is.EqualTo(20));
            Assert.That(world.StartBattle().Encounter.Id, Is.EqualTo("pack"));
        }

        [Test]
        public void Restart_PutsTheLevelsBack()
        {
            _game.EnemiesDefend();
            var world = _game.Start();
            PlayToTheEnd(world.StartBattle());
            Assume.That(_game.Member(Knight).Level, Is.EqualTo(2));

            world.Restart();

            Assert.That(world.Party.Select(m => m.Level), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(world.ExperienceOf(_game.Member(Knight).Id), Is.EqualTo(0L));
            Assert.That(_game.Member(Knight).MaxHealth, Is.EqualTo(120));
            Assert.That(world.CountOf(Ear), Is.EqualTo(0));
        }

        [Test]
        public void AWholeRun_OfThreeEncounters_PlayedByAScriptedParty()
        {
            _game.Settings.Formula = null;
            _game.Settings.RestAfterVictory = 0.5f;
            var world = _game.Start();
            var outcomes = new List<BattleOutcome>();
            var completed = new List<RunCompletedEvent>();
            _game.Bus.Subscribe<BattleEndedEvent>(e => outcomes.Add(e.Outcome));
            _game.Bus.Subscribe<RunCompletedEvent>(completed.Add);

            for (int fight = 0; fight < 3; fight++)
                PlayToTheEnd(world.StartBattle());

            Assert.That(outcomes, Is.EqualTo(new[] { BattleOutcome.Victory, BattleOutcome.Victory, BattleOutcome.Victory }));
            Assert.That(world.IsRunComplete, Is.True);
            Assert.That(completed.Single().EncountersWon, Is.EqualTo(3));
            Assert.That(world.Gold, Is.EqualTo(20 + 10 + 15 + 50));
            Assert.That(world.Party.All(m => m.Level >= 3), Is.True, "100 + 110 + 200 experience");
            Assert.That(world.WhyCannotStartBattle(), Is.EqualTo("Every encounter has been won."));
            Assert.That(world.NextEncounter, Is.Null);
        }

        [Test]
        public void Events_ComeInOrder()
        {
            _game.EnemiesDefend();
            _game.Settings.Encounters.RemoveRange(1, 2);
            _game.Settings.Encounters[0].EnemyIds.RemoveAt(1);
            _game.GoblinData.MaxHealth = 3;
            _game.GoblinData.ExperienceReward = 100;
            var world = _game.Start();
            var events = new List<string>();
            _game.Bus.Subscribe<BattleStartedEvent>(e => events.Add("battle started"));
            _game.Bus.Subscribe<RoundStartedEvent>(e => events.Add($"round {e.Round}"));
            _game.Bus.Subscribe<TurnStartedEvent>(e => events.Add($"turn {world.Battle.Get(e.CombatantId).Name}"));
            _game.Bus.Subscribe<ActionPerformedEvent>(e => events.Add($"{e.Kind} by {world.Battle.Get(e.ActorId).Name}: {e.Targets.Single().Damage}"));
            _game.Bus.Subscribe<CombatantDefeatedEvent>(e => events.Add($"defeated {e.CharacterId}"));
            _game.Bus.Subscribe<MemberLevelUpEvent>(e => events.Add($"level up {e.CharacterId}"));
            _game.Bus.Subscribe<BattleEndedEvent>(e => events.Add($"battle ended {e.Outcome}"));
            _game.Bus.Subscribe<RunCompletedEvent>(e => events.Add("run completed"));

            var battle = world.StartBattle();
            Expect(battle.Perform(BattleAction.Attack(battle.Enemies[0].Id)));

            Assert.That(events, Is.EqualTo(new[]
            {
                "battle started", "round 1", "turn Mage",
                "Attack by Mage: 3", "defeated goblin",
                "level up knight", "level up mage", "level up cleric",
                "battle ended Victory", "run completed"
            }));
        }
    }
}
