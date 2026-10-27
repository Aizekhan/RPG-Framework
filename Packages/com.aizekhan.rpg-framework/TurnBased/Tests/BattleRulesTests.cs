using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The rules of one fight through the real modules: turn order by speed (ties to the party, then the slot), the
    /// actions (attack, ability with mana and cooldown, defend, item, flee) and their refusals, which change nothing.
    /// </summary>
    public sealed class BattleRulesTests
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

        private Combatant Enemy(Battle battle, int slot) => battle.Enemies[slot];

        // ---- Turn order ----

        [Test]
        public void TurnOrder_IsBySpeed_FastestFirst()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();

            Assert.That(battle.Round, Is.EqualTo(1));
            Assert.That(battle.TurnOrder.Select(c => c.Name), Is.EqualTo(new[] { "Mage", "Cleric", "Goblin A", "Goblin B", "Knight" }));
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Mage));
        }

        [Test]
        public void TurnOrder_Ties_GoToTheParty_ThenTheEarlierSlot()
        {
            _game.EnemiesDefend();
            _game.KnightData.Speed = 9;
            _game.ClericData.Speed = 9;
            var orders = new List<IReadOnlyList<int>>();
            _game.Start();
            _game.Bus.Subscribe<RoundStartedEvent>(e => orders.Add(e.TurnOrder));

            var battle = _game.World.StartBattle();

            var names = orders[0].Select(id => battle.Get(id).Name);
            Assert.That(names, Is.EqualTo(new[] { "Mage", "Knight", "Cleric", "Goblin A", "Goblin B" }));
        }

        [Test]
        public void TurnOrder_EveryoneActsOncePerRound_ThenANewRoundStarts()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var turns = new List<string>();
            _game.Bus.Subscribe<TurnStartedEvent>(e => turns.Add(battle.Get(e.CombatantId).Name));

            for (int i = 0; i < 3; i++)
                Expect(battle.Perform(BattleAction.Defend()));

            Assert.That(turns, Is.EqualTo(new[] { "Cleric", "Goblin A", "Goblin B", "Knight", "Mage" }));
            Assert.That(battle.Round, Is.EqualTo(2));
        }

        [Test]
        public void DefeatedCombatant_LeavesTheTurnOrder()
        {
            _game.EnemiesDefend();
            _game.GoblinData.MaxHealth = 3;
            var battle = StartBattle();
            var goblinA = Enemy(battle, 0);

            Expect(battle.Perform(BattleAction.Attack(goblinA.Id)));

            Assert.That(goblinA.IsAlive, Is.False);
            Assert.That(battle.TurnOrder.Select(c => c.Name), Is.EqualTo(new[] { "Cleric", "Goblin B", "Knight" }));
        }

        // ---- Refusals ----

        [Test]
        public void Refused_WhenItIsNotThatCombatantsTurn_AndNothingChanges()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var knight = _game.Member(Knight);
            var goblin = Enemy(battle, 0);

            var result = battle.Perform(BattleAction.Attack(goblin.Id).By(knight.Id));

            Assert.That(result.Done, Is.False);
            Assert.That(result.Refusal, Is.EqualTo("It is not Knight's turn; it is Mage's."));
            Assert.That(goblin.Health, Is.EqualTo(40));
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Mage));
        }

        [Test]
        public void Refused_WithoutEnoughMana_AndNothingIsPaid()
        {
            _game.EnemiesDefend();
            _game.MageData.MaxMana = 5;
            var battle = StartBattle();
            var mage = _game.Member(Mage);

            var result = battle.Perform(BattleAction.Ability(Fire, Enemy(battle, 0).Id));

            Assert.That(result.Refusal, Is.EqualTo("Not enough mana: Fire costs 6 MP and Mage has 5."));
            Assert.That(mage.Mana, Is.EqualTo(5));
            Assert.That(Enemy(battle, 0).Health, Is.EqualTo(40));
            Assert.That(battle.Current, Is.SameAs(mage));
        }

        [Test]
        public void Cooldown_CountsRounds_AndRefusesUntilItIsOver()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var goblin = Enemy(battle, 0);

            DefendUntil(battle, Knight);
            Expect(battle.Perform(BattleAction.Ability(PowerStrike, goblin.Id)));
            Assert.That(goblin.Health, Is.EqualTo(40 - 14), "20 x 1.5 - 2, halved: the goblin defends");

            DefendUntil(battle, Knight);
            Assert.That(battle.Round, Is.EqualTo(2));
            Assert.That(battle.CooldownLeft(battle.Current.Id, PowerStrike), Is.EqualTo(1));
            Assert.That(battle.Perform(BattleAction.Ability(PowerStrike, goblin.Id)).Refusal,
                Is.EqualTo("Power strike is not ready: 1 more round."));
            Expect(battle.Perform(BattleAction.Defend()));

            DefendUntil(battle, Knight);
            Assert.That(battle.Round, Is.EqualTo(3));
            Assert.That(battle.CanPerform(BattleAction.Ability(PowerStrike, goblin.Id)), Is.True);
        }

        [Test]
        public void Refused_OnATargetThatIsAlreadyDown()
        {
            _game.EnemiesDefend();
            _game.GoblinData.MaxHealth = 3;
            var battle = StartBattle();
            var goblinA = Enemy(battle, 0);
            Expect(battle.Perform(BattleAction.Attack(goblinA.Id)));

            var result = battle.Perform(BattleAction.Attack(goblinA.Id));

            Assert.That(result.Refusal, Is.EqualTo("Goblin A is already down."));
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Cleric));
        }

        [Test]
        public void Refused_AttackingAnAlly_OrAnAbilityItDoesNotKnow()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();

            Assert.That(battle.WhyNot(BattleAction.Attack(_game.Member(Knight).Id)), Is.EqualTo("Mage cannot attack an ally."));
            Assert.That(battle.WhyNot(BattleAction.Ability(Heal, _game.Member(Knight).Id)), Is.EqualTo("Mage does not know Heal."));
            Assert.That(battle.WhyNot(BattleAction.Ability(Fire)), Is.EqualTo("Choose a target."));
        }

        [Test]
        public void Flee_IsRefused_WhenTheEncounterForbidsIt()
        {
            _game.EnemiesDefend();
            _game.Settings.Encounters[0].CanFlee = false;
            var battle = StartBattle();

            var result = battle.Perform(BattleAction.Flee());

            Assert.That(result.Refusal, Is.EqualTo("There is no running from this fight."));
            Assert.That(battle.IsOver, Is.False);
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Mage));
        }

        [Test]
        public void Refused_OnceTheBattleIsOver()
        {
            _game.EnemiesDefend();
            _game.Settings.FleeChance = 1f;
            var battle = StartBattle();
            Expect(battle.Perform(BattleAction.Flee()));

            Assert.That(battle.Perform(BattleAction.Defend()).Refusal, Is.EqualTo("The battle is over."));
        }

        // ---- Damage ----

        [Test]
        public void Attack_DealsAttackMinusHalfDefense()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var goblin = Enemy(battle, 0);

            var result = Expect(battle.Perform(BattleAction.Attack(goblin.Id)));

            Assert.That(result.Targets.Single().Damage, Is.EqualTo(4));
            Assert.That(goblin.Health, Is.EqualTo(36));
        }

        [Test]
        public void Ability_PaysItsMana_AndDealsMagicDamage()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var mage = _game.Member(Mage);
            var goblin = Enemy(battle, 0);

            var result = Expect(battle.Perform(BattleAction.Ability(Fire, goblin.Id)));

            Assert.That(mage.Mana, Is.EqualTo(40 - 6));
            Assert.That(result.Targets.Single().Damage, Is.EqualTo(26), "18 x 1.5 - 4 x 0.25");
            Assert.That(goblin.Health, Is.EqualTo(14));
        }

        [Test]
        public void AbilityOnAllEnemies_HitsEveryoneStanding()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();

            var result = Expect(battle.Perform(BattleAction.Ability(Blizzard)));

            Assert.That(result.Targets.Select(t => t.Damage), Is.EqualTo(new[] { 17, 17 }));
            Assert.That(battle.Enemies.Select(e => e.Health), Is.EqualTo(new[] { 23, 23 }));
        }

        [Test]
        public void SameSeed_SameBattle_WithMissesAndSpread()
        {
            string Play(int seed)
            {
                using (var game = new TurnBasedTestGame(seed))
                {
                    game.Settings.Formula = new DefaultBattleFormula { MissChance = 0.2f, Variance = 0.2f };
                    game.Start();
                    var battle = game.World.StartBattle();
                    PlayToTheEnd(battle);
                    return string.Join("|", battle.Log);
                }
            }

            string first = Play(11);
            Assert.That(Play(11), Is.EqualTo(first));
            Assert.That(first, Does.Contain("misses"), "a 20% miss chance shows up over a whole fight");
        }

        // ---- Defend ----

        [Test]
        public void Defend_HalvesDamage_UntilItsNextTurn()
        {
            _game.EnemiesAttack(Knight);
            var battle = StartBattle();
            var knight = _game.Member(Knight);

            // Round 1: the goblins hit the knight before it defends (7 each)
            DefendUntil(battle, Knight);
            Assert.That(knight.Health, Is.EqualTo(120 - 14));
            Expect(battle.Perform(BattleAction.Defend()));
            Assert.That(knight.IsDefending, Is.True);

            // Round 2: it is still defending: 7 halved = 4 each
            DefendUntil(battle, Knight);
            Assert.That(knight.Health, Is.EqualTo(106 - 8));
            Assert.That(knight.IsDefending, Is.False, "defending ends when its next turn starts");
        }

        // ---- Items ----

        [Test]
        public void Potion_HealsAnAlly_AndIsUsedUp()
        {
            _game.EnemiesAttack(Knight);
            var battle = StartBattle();
            var knight = _game.Member(Knight);
            DefendUntil(battle, Knight);
            Assume.That(knight.Health, Is.EqualTo(106));

            var result = Expect(battle.Perform(BattleAction.Item(Potion, knight.Id)));

            Assert.That(result.Targets.Single().Healing, Is.EqualTo(14), "capped at the maximum");
            Assert.That(knight.Health, Is.EqualTo(120));
            Assert.That(_game.World.CountOf(Potion), Is.EqualTo(1));
        }

        [Test]
        public void Items_Ether_Bomb_AndAStatusEffectItem()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();
            var mage = _game.Member(Mage);
            var goblin = Enemy(battle, 0);
            Expect(battle.Perform(BattleAction.Ability(Fire, goblin.Id)));

            Expect(battle.Perform(BattleAction.Item(Ether, mage.Id)));
            Assert.That(mage.Mana, Is.EqualTo(40), "34 + 20, capped");

            DefendUntil(battle, Knight);
            Expect(battle.Perform(BattleAction.Item(Bomb, Enemy(battle, 1).Id)));
            Assert.That(Enemy(battle, 1).Health, Is.EqualTo(10));

            Expect(battle.Perform(BattleAction.Item(PoisonVial, Enemy(battle, 1).Id)));
            Assert.That(_game.World.StatusEffects.HasEffect(Enemy(battle, 1).Id, Poison), Is.True);
            Assert.That(_game.World.CountOf(Ether) + _game.World.CountOf(Bomb) + _game.World.CountOf(PoisonVial), Is.EqualTo(0));
        }

        [Test]
        public void PhoenixDown_BringsAFallenAllyBack_ToTheNextRound()
        {
            _game.MageData.MaxHealth = 5;
            _game.EnemiesAttack(Mage);
            var battle = StartBattle();
            var mage = _game.Member(Mage);
            DefendUntil(battle, Knight);
            Assume.That(mage.IsAlive, Is.False);

            Assert.That(battle.WhyNot(BattleAction.Item(Potion, mage.Id)), Is.EqualTo("Mage is already down."));
            var result = Expect(battle.Perform(BattleAction.Item(Phoenix, mage.Id)));

            Assert.That(result.Targets.Single().Revived, Is.True);
            Assert.That(mage.Health, Is.EqualTo(3), "half of 5, rounded");
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Mage), "back in the turn order from the next round");
        }

        [Test]
        public void Item_IsRefused_WhenThePartyHasNone()
        {
            _game.EnemiesDefend();
            _game.Settings.StartingItems.Clear();
            var battle = StartBattle();

            Assert.That(battle.Perform(BattleAction.Item(Potion, _game.Member(Mage).Id)).Refusal, Is.EqualTo("The party has no Potion."));
        }

        // ---- Flee ----

        [Test]
        public void Flee_Works_WithTheChance_AndTheSameEncounterComesNext()
        {
            _game.EnemiesDefend();
            _game.Settings.FleeChance = 1f;
            var battle = StartBattle();
            var ended = new List<BattleEndedEvent>();
            _game.Bus.Subscribe<BattleEndedEvent>(ended.Add);

            var result = Expect(battle.Perform(BattleAction.Flee()));

            Assert.That(result.Escaped, Is.True);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Fled));
            Assert.That(ended.Single().Outcome, Is.EqualTo(BattleOutcome.Fled));
            Assert.That(_game.World.EncounterIndex, Is.EqualTo(0));
            Assert.That(_game.World.NextEncounter.Id, Is.EqualTo("pack"));
            Assert.That(_game.World.StartBattle(), Is.Not.Null, "the party can try again");
        }

        [Test]
        public void Flee_ThatFails_UsesTheTurn()
        {
            _game.EnemiesDefend();
            _game.Settings.FleeChance = 0f;
            var battle = StartBattle();

            var result = Expect(battle.Perform(BattleAction.Flee()));

            Assert.That(result.Escaped, Is.False);
            Assert.That(battle.IsOver, Is.False);
            Assert.That(battle.Current.CharacterId, Is.EqualTo(Cleric));
        }
    }
}
