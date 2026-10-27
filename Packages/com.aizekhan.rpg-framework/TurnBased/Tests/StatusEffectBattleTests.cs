using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.StatusEffects;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// Status effects in battle run through the StatusEffects module on the combatants' own turns: damage over time
    /// ticks at the start of the affected combatant's turn, durations count its turns, a stun skips a turn, stat
    /// changes reach the stats and come off when the effect expires or the battle ends.
    /// </summary>
    public sealed class StatusEffectBattleTests
    {
        private TurnBasedTestGame _game;

        [SetUp]
        public void SetUp() => _game = new TurnBasedTestGame();

        [TearDown]
        public void TearDown() => _game.Dispose();

        private Battle StartBattle()
        {
            _game.Start();
            return _game.World.StartBattle();
        }

        private static void PlayUntilRound(Battle battle, int round, string characterId)
        {
            for (int guard = 0; guard < 100 && !(battle.Round == round && battle.Current.CharacterId == characterId); guard++)
                Expect(battle.Perform(BattleAction.Defend()));
        }

        [Test]
        public void Poison_TicksAtTheStartOfItsTurn_ThreeTimes_ThenExpires()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var goblin = battle.Enemies[0];
            var log = new List<string>();
            _game.Bus.Subscribe<TurnStartedEvent>(e => log.Add("turn " + battle.Get(e.CombatantId).Name));
            _game.Bus.Subscribe<EffectTickEvent>(e => log.Add($"tick {e.EffectId} {e.Damage} on {battle.Get(e.EntityId).Name}"));
            _game.Bus.Subscribe<EffectExpiredEvent>(e => log.Add($"expired {e.EffectId} on {battle.Get(e.EntityId).Name}"));

            Expect(battle.Perform(BattleAction.Ability(PoisonDart, goblin.Id)));
            Assert.That(_game.World.StatusEffects.HasEffect(goblin.Id, Poison), Is.True);

            PlayUntilRound(battle, 2, Mage);
            Assert.That(goblin.Health, Is.EqualTo(35));
            int turn = log.IndexOf("turn Goblin A");
            Assert.That(log[turn + 1], Is.EqualTo("tick poison 5 on Goblin A"), "the tick comes right as its turn starts");

            PlayUntilRound(battle, 5, Mage);
            Assert.That(goblin.Health, Is.EqualTo(25), "three ticks of 5");
            Assert.That(log.Count(l => l.StartsWith("tick")), Is.EqualTo(3));
            Assert.That(log.Count(l => l == "expired poison on Goblin A"), Is.EqualTo(1));
            Assert.That(_game.World.StatusEffects.HasEffect(goblin.Id, Poison), Is.False);
        }

        [Test]
        public void Poison_ThatDefeats_TakesTheCombatantOutOfTheFight()
        {
            _game.EnemiesDefend();
            _game.GoblinData.MaxHealth = 5;
            var battle = StartBattle();
            var goblin = battle.Enemies[0];
            var defeated = new List<CombatantDefeatedEvent>();
            _game.Bus.Subscribe<CombatantDefeatedEvent>(defeated.Add);

            Expect(battle.Perform(BattleAction.Ability(PoisonDart, goblin.Id)));
            Expect(battle.Perform(BattleAction.Defend()));

            Assert.That(goblin.IsAlive, Is.False);
            Assert.That(defeated.Single().CombatantId, Is.EqualTo(goblin.Id));
            Assert.That(defeated.Single().DefeatedById, Is.EqualTo(-1), "no one dealt the blow");
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Knight), "its turn is gone, Goblin B acted");
        }

        [Test]
        public void Stun_SkipsOneTurn_ThenWearsOff()
        {
            _game.EnemiesAttack(Knight);
            var battle = StartBattle();
            var goblinA = battle.Enemies[0];
            var knight = _game.Member(Knight);
            var skipped = new List<int>();
            var attacksByA = 0;
            _game.Bus.Subscribe<TurnSkippedEvent>(e => skipped.Add(e.CombatantId));
            _game.Bus.Subscribe<ActionPerformedEvent>(e => { if (e.ActorId == goblinA.Id) attacksByA++; });

            Expect(battle.Perform(BattleAction.Ability(StunBolt, goblinA.Id)));
            Assert.That(goblinA.IsStunned, Is.True);
            Expect(battle.Perform(BattleAction.Defend()));

            Assert.That(skipped, Is.EqualTo(new[] { goblinA.Id }));
            Assert.That(attacksByA, Is.EqualTo(0));
            Assert.That(knight.Health, Is.EqualTo(120 - 7), "only Goblin B hit");
            Assert.That(goblinA.IsStunned, Is.False, "a 1-turn stun is over after the turn it took");

            PlayUntilRound(battle, 3, Mage);
            Assert.That(attacksByA, Is.EqualTo(1));
            Assert.That(knight.Health, Is.EqualTo(113 - 8), "both hit, halved: the knight defended in round 1");
        }

        [Test]
        public void AttackUp_RaisesTheStat_ForItsTurns_ThenComesOff()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var knight = _game.Member(Knight);
            var mage = _game.Member(Mage);
            var goblin = battle.Enemies[0];

            Expect(battle.Perform(BattleAction.Defend()));
            Expect(battle.Perform(BattleAction.Ability(Bless)));
            Assert.That(knight.Attack, Is.EqualTo(30f));
            Assert.That(mage.Attack, Is.EqualTo(9f));

            var hit = Expect(battle.Perform(BattleAction.Attack(goblin.Id)));
            Assert.That(hit.Targets.Single().Damage, Is.EqualTo(14), "30 - 2, halved: the goblin defends");

            // Three of the knight's turns: rounds 1, 2 and 3
            PlayUntilRound(battle, 3, Knight);
            Assert.That(knight.Attack, Is.EqualTo(30f));
            Expect(battle.Perform(BattleAction.Defend()));
            Assert.That(knight.Attack, Is.EqualTo(20f));
            Assert.That(mage.Attack, Is.EqualTo(9f), "the mage got it after its round-1 turn, so it lasts a round longer");

            PlayUntilRound(battle, 5, Mage);
            Assert.That(mage.Attack, Is.EqualTo(6f));
        }

        [Test]
        public void Effects_EndWithTheBattle()
        {
            _game.EnemiesDefend();
            _game.Settings.FleeChance = 1f;
            var battle = StartBattle();
            var knight = _game.Member(Knight);

            Expect(battle.Perform(BattleAction.Defend()));
            Expect(battle.Perform(BattleAction.Ability(Bless)));
            Assume.That(knight.Attack, Is.EqualTo(30f));
            Expect(battle.Perform(BattleAction.Flee()));

            Assert.That(knight.Attack, Is.EqualTo(20f));
            Assert.That(_game.World.StatusEffects.GetActiveEffects(knight.Id), Is.Empty);
        }

        [Test]
        public void Antidote_CuresPoison()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var knight = _game.Member(Knight);

            Expect(battle.Perform(BattleAction.Item(PoisonVial, knight.Id)));
            Assume.That(_game.World.StatusEffects.HasEffect(knight.Id, Poison), Is.True);
            Expect(battle.Perform(BattleAction.Item(Antidote, knight.Id)));

            Assert.That(_game.World.StatusEffects.HasEffect(knight.Id, Poison), Is.False);
            PlayUntilRound(battle, 2, Mage);
            Assert.That(knight.Health, Is.EqualTo(120), "no tick after the cure");
        }

        [Test]
        public void Raise_BringsAFallenAllyBack()
        {
            _game.MageData.MaxHealth = 5;
            _game.EnemiesAttack(Mage);
            var battle = StartBattle();
            var mage = _game.Member(Mage);
            PlayUntilRound(battle, 2, Cleric);
            Assume.That(mage.IsAlive, Is.False);

            var result = Expect(battle.Perform(BattleAction.Ability(Raise, mage.Id)));

            Assert.That(result.Targets.Single().Revived, Is.True);
            Assert.That(result.Targets.Single().Healing, Is.EqualTo(5), "14 magic heals more than the 5 it can hold");
            Assert.That(_game.Member(Cleric).Mana, Is.EqualTo(30 - 15));
        }
    }
}
