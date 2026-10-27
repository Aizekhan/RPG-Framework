using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The default enemy AI (<see cref="SimpleBattleAi"/>): heal a hurt ally when it can, otherwise the strongest
    /// damage it can afford on the weakest hero; a choice the battle refuses becomes a plain attack.
    /// </summary>
    public sealed class BattleAiTests
    {
        private TurnBasedTestGame _game;
        private readonly List<ActionPerformedEvent> _actions = new List<ActionPerformedEvent>();

        [SetUp]
        public void SetUp()
        {
            _game = new TurnBasedTestGame();
            _game.Settings.Encounters[0].EnemyIds.Clear();
            _game.Settings.Encounters[0].EnemyIds.AddRange(new[] { Goblin, Shaman });
            _actions.Clear();
        }

        [TearDown]
        public void TearDown() => _game.Dispose();

        // Order: Mage 12, Goblin Shaman 11, Cleric 10, Goblin 9, Knight 8
        private Battle StartBattle()
        {
            _game.Start();
            _game.Bus.Subscribe<ActionPerformedEvent>(_actions.Add);
            return _game.World.StartBattle();
        }

        private ActionPerformedEvent ActionOf(Combatant actor) => _actions.First(a => a.ActorId == actor.Id);

        [Test]
        public void HealsAHurtAlly_WhenItCan()
        {
            var battle = StartBattle();
            var goblin = battle.Enemies[0];
            var shaman = battle.Enemies[1];

            Expect(battle.Perform(BattleAction.Ability(Fire, goblin.Id)));

            var heal = ActionOf(shaman);
            Assert.That(heal.AbilityId, Is.EqualTo(Mend));
            Assert.That(heal.Targets.Single().TargetId, Is.EqualTo(goblin.Id));
            Assert.That(heal.Targets.Single().Healing, Is.EqualTo(18), "12 magic x 1.5");
            Assert.That(goblin.Health, Is.EqualTo(14 + 18));
            Assert.That(shaman.Mana, Is.EqualTo(30 - 5));
        }

        [Test]
        public void UsesTheStrongestAffordableAttack_OnTheWeakestHero()
        {
            var battle = StartBattle();
            var shaman = battle.Enemies[1];
            var mage = _game.Member(Mage);

            Expect(battle.Perform(BattleAction.Defend()));

            var spark = ActionOf(shaman);
            Assert.That(spark.AbilityId, Is.EqualTo(Spark), "12 magic beats a 6 attack");
            Assert.That(spark.Targets.Single().TargetId, Is.EqualTo(mage.Id), "the mage has the least health");
            Assert.That(spark.Targets.Single().Damage, Is.EqualTo(6), "12 - 4 x 0.25, halved: the mage defends");
        }

        [Test]
        public void AttacksPlainly_WhenItCannotAffordItsAbilities()
        {
            _game.Settings.Enemies.First(e => e.Id == Shaman).MaxMana = 2;
            var battle = StartBattle();
            var shaman = battle.Enemies[1];

            Expect(battle.Perform(BattleAction.Attack(battle.Enemies[0].Id)));

            var attack = ActionOf(shaman);
            Assert.That(attack.Kind, Is.EqualTo(BattleActionKind.Attack));
            Assert.That(attack.Targets.Single().TargetId, Is.EqualTo(_game.Member(Mage).Id));
            Assert.That(attack.Targets.Single().Damage, Is.EqualTo(4), "6 - 4 x 0.5");
        }

        [Test]
        public void RandomTargets_RepeatWithTheSeed()
        {
            List<int> Targets(int seed)
            {
                using (var game = new TurnBasedTestGame(seed))
                {
                    game.Settings.EnemyAi = new SimpleBattleAi { TargetWeakest = false };
                    game.Start();
                    var targets = new List<int>();
                    game.Bus.Subscribe<ActionPerformedEvent>(e =>
                    {
                        if (e.Kind != BattleActionKind.Defend && game.World.Battle.Get(e.ActorId)?.IsParty == false)
                            targets.Add(game.World.Battle.Get(e.Targets[0].TargetId).Slot);
                    });
                    var battle = game.World.StartBattle();
                    for (int turn = 0; turn < 12 && !battle.IsOver; turn++)
                        Expect(battle.Perform(BattleAction.Defend()));
                    return targets;
                }
            }

            var first = Targets(5);
            Assert.That(first.Count, Is.AtLeast(6));
            Assert.That(Targets(5), Is.EqualTo(first));
            Assert.That(first.Distinct().Count(), Is.GreaterThan(1), "not always the same hero");
        }

        [Test]
        public void AChoiceTheBattleRefuses_BecomesAPlainAttack()
        {
            _game.Settings.EnemyAi = new ScriptedAi((battle, actor) => BattleAction.Attack(actor.Id));
            var battle = StartBattle();
            var shaman = battle.Enemies[1];

            Expect(battle.Perform(BattleAction.Defend()));

            var attack = ActionOf(shaman);
            Assert.That(attack.Kind, Is.EqualTo(BattleActionKind.Attack));
            Assert.That(battle.Get(attack.Targets.Single().TargetId).IsParty, Is.True);
        }

        [Test]
        public void EnemiesWait_WhenTheyDoNotActOnTheirOwn()
        {
            _game.Settings.EnemiesActAutomatically = false;
            var battle = StartBattle();
            var shaman = battle.Enemies[1];

            Expect(battle.Perform(BattleAction.Defend()));
            Assert.That(battle.Current, Is.SameAs(shaman));
            Assert.That(battle.SuggestAction().AbilityId, Is.EqualTo(Spark));

            Expect(battle.PlayAiTurn());
            Assert.That(ActionOf(shaman).AbilityId, Is.EqualTo(Spark));
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Cleric));
        }
    }
}
