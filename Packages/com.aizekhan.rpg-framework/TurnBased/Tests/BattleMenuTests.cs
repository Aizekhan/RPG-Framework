using System.Linq;
using NUnit.Framework;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The battle menu: every command of the current party member with its targets, or nothing to aim, or why it is
    /// closed, as the battle's rules say; an aimed command is taken by the battle.
    /// </summary>
    public sealed class BattleMenuTests
    {
        private TurnBasedTestGame _game;

        [SetUp]
        public void SetUp() => _game = new TurnBasedTestGame();

        [TearDown]
        public void TearDown() => _game.Dispose();

        [Test]
        public void TheMenu_ListsCommandsWithTargets_OrWhyTheyAreClosed()
        {
            _game.EnemiesDefend();
            _game.MageData.MaxMana = 8;
            var world = _game.Start();
            var battle = world.StartBattle();
            DefendUntil(battle, Mage);

            var menu = BattleMenu.For(battle, world.Inventory.GetAllItems());

            var attack = menu.First(c => c.Kind == BattleActionKind.Attack);
            Assert.That(attack.NeedsTarget, Is.True);
            Assert.That(attack.Targets.Select(t => t.Side), Is.All.EqualTo(BattleSide.Enemies));
            Assert.That(attack.Targets.Count, Is.EqualTo(2));

            var fire = menu.First(c => c.Action.AbilityId == Fire);
            Assert.That(fire.IsAvailable && fire.NeedsTarget, Is.True, "6 of 8 mana");
            var blizzard = menu.First(c => c.Action.AbilityId == Blizzard);
            Assert.That(blizzard.IsAvailable, Is.False, "12 mana");
            Assert.That(blizzard.Refusal, Is.Not.Empty);

            var potion = menu.First(c => c.Action.ItemId == Potion);
            Assert.That(potion.Label, Is.EqualTo("Potion x2"));
            Assert.That(potion.Targets.Select(t => t.Side), Is.All.EqualTo(BattleSide.Party));
            Assert.That(menu.First(c => c.Action.ItemId == Phoenix).IsAvailable, Is.False, "nobody has fallen");
            Assert.That(menu.Any(c => c.Kind == BattleActionKind.Defend && c.IsAvailable && !c.NeedsTarget), Is.True);
            Assert.That(menu.Any(c => c.Kind == BattleActionKind.Flee), Is.True);
            Assert.That(menu.Any(c => c.Action.ItemId == Ear), Is.False, "not for battle");

            var goblin = attack.Targets[0];
            int before = goblin.Health;
            Assert.That(battle.Perform(attack.With(goblin)).Done, Is.True);
            Assert.That(goblin.Health, Is.LessThan(before));
        }

        [Test]
        public void NoMenu_OnAnEnemysTurn_OrAfterTheBattle()
        {
            Assert.That(BattleMenu.For(null), Is.Empty);
            _game.EnemiesDefend();
            var world = _game.Start();
            var battle = world.StartBattle();
            for (int turn = 0; turn < 200 && !battle.IsOver; turn++)
                battle.Perform(battle.SuggestAction());
            Assert.That(BattleMenu.For(battle, world.Inventory.GetAllItems()), Is.Empty);
        }
    }
}
