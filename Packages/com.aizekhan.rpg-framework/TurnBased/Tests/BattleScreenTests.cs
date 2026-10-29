using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.TurnBased.Unity;
using UnityEngine;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The battle screen built from code: between battles it offers the next encounter, in a battle the current
    /// member's commands, a command that needs a target asks for one, and the choice is taken by the battle.
    /// </summary>
    public sealed class BattleScreenTests
    {
        private TurnBasedTestGame _game;
        private readonly List<GameObject> _cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp() => _game = new TurnBasedTestGame();

        [TearDown]
        public void TearDown()
        {
            foreach (var piece in _cleanup)
                Object.DestroyImmediate(piece);
            _game.Dispose();
        }

        private BattleScreen Screen(TurnBasedWorld world)
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _cleanup.Add(canvas);
            var screen = canvas.AddComponent<BattleScreen>();
            screen.Bind(world);
            return screen;
        }

        private static int Row(BattleScreen screen, string startsWith)
        {
            for (int i = 0; i < screen.Menu.RowCount; i++)
            {
                if (screen.Menu.TextAt(i).StartsWith(startsWith))
                    return i;
            }
            Assert.Fail("No row starting with '" + startsWith + "'; rows: " +
                string.Join(" | ", Enumerable.Range(0, screen.Menu.RowCount).Select(screen.Menu.TextAt)));
            return -1;
        }

        [Test]
        public void TheScreen_StartsABattle_AimsACommand_AndTheBattleTakesIt()
        {
            _game.EnemiesDefend();
            var world = _game.Start();
            var screen = Screen(world);

            Assert.That(screen.Menu.TextAt(0), Is.EqualTo("Next: Goblin Pack"));
            screen.Menu.ButtonAt(0, 0).onClick.Invoke();
            screen.Refresh();
            Assert.That(world.IsInBattle, Is.True);

            var battle = world.Battle;
            string name = battle.Current.Name;
            Assert.That(screen.Menu.TextAt(0), Is.EqualTo(name + "'s turn"));
            screen.Menu.ButtonAt(Row(screen, "Attack"), 0).onClick.Invoke();
            screen.Refresh();
            Assert.That(screen.Menu.TextAt(0), Is.EqualTo(name + ": Attack on whom?"));

            int before = battle.Enemies.Sum(e => e.Health);
            screen.Menu.ButtonAt(Row(screen, battle.Enemies[0].Name), 0).onClick.Invoke();
            screen.Refresh();
            Assert.That(battle.Enemies.Sum(e => e.Health), Is.LessThan(before));
            Assert.That(screen.Menu.TextAt(0), Does.EndWith("'s turn"), "the next member's menu");

            screen.Menu.ButtonAt(Row(screen, "Attack"), 0).onClick.Invoke();
            screen.Refresh();
            screen.Menu.ButtonAt(screen.Menu.RowCount - 1, 0).onClick.Invoke();
            screen.Refresh();
            Assert.That(screen.Menu.TextAt(0), Does.EndWith("'s turn"), "Back to the commands");
        }
    }
}
