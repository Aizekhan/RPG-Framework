using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Items;
using RPGFramework.Stats;
using RPGFramework.UI.Panels;
using RPGFramework.UINavigation;
using RPGFramework.UINavigation.Unity;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RPGFramework.UI.Tests
{
    /// <summary>
    /// Panels that build themselves with no prefab: stats from a stat container, equipment slots from the game's
    /// own list with icons from the game's function, and screens made in code opened through the navigation.
    /// </summary>
    public sealed class SelfBuildingPanelsTests
    {
        private enum RunnerStat
        {
            Speed,
            Luck
        }

        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private T Panel<T>() where T : Component
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _cleanup.Add(canvas);
            var screen = new GameObject("Screen", typeof(RectTransform));
            screen.transform.SetParent(canvas.transform, false);
            return screen.AddComponent<T>();
        }

        private Sprite NewSprite()
        {
            var texture = new Texture2D(4, 4);
            _cleanup.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
            _cleanup.Add(sprite);
            return sprite;
        }

        [Test]
        public void StatList_ShowsEveryStatOfTheContainer_AndFollowsItsValues()
        {
            IStatContainer<RunnerStat, float> stats = new StatContainer<RunnerStat, float>();
            stats.SetBaseStat(RunnerStat.Speed, 12.4f);
            stats.SetBaseStat(RunnerStat.Luck, 3f);
            var panel = Panel<StatListPanel>();

            panel.BindStats(() => stats, stat => stat == RunnerStat.Speed ? "Top speed" : null);
            panel.Title = "Runner";

            Assert.That(panel.RowCount, Is.EqualTo(2));
            var texts = panel.GetComponentsInChildren<Text>(true).Select(t => t.text).ToList();
            Assert.That(texts, Does.Contain("Top speed").And.Contain("Runner"));
            Assert.That(Enumerable.Range(0, 2).Select(panel.ValueAt), Is.EquivalentTo(new[] { "12", "3" }));

            stats.SetBaseStat(RunnerStat.Luck, 7f);
            panel.Refresh();
            Assert.That(Enumerable.Range(0, 2).Select(panel.ValueAt), Does.Contain("7"));

            long gold = 40;
            panel.Bind(new (string, System.Func<string>)[] { ("Gold", () => gold.ToString()) });
            Assert.That(panel.RowCount, Is.EqualTo(1), "rows of your own replace the stats");
            gold = 55;
            panel.Refresh();
            Assert.That(panel.ValueAt(0), Is.EqualTo("55"));
        }

        [Test]
        public void EquipmentSlots_ComeFromTheGamesList_WithTheGamesIcons()
        {
            var board = new Item("board", "Hoverboard", "Fast", ItemType.Equipment);
            var worn = new Dictionary<string, IItem> { ["board"] = board };
            var boardIcon = NewSprite();
            var panel = Panel<EquipmentSlotsPanel>();
            var clicked = new List<string>();
            panel.SlotClicked += clicked.Add;

            panel.Bind(new[] { "board", "charm" }, slot => worn.TryGetValue(slot, out var item) ? item : null,
                item => item.ItemId == "board" ? boardIcon : null, slot => slot.ToUpperInvariant());

            Assert.That(panel.SlotIds, Is.EqualTo(new[] { "board", "charm" }));
            var boardSlot = panel.SlotOf("board");
            Assert.That(boardSlot.CurrentItem, Is.SameAs(board));
            var icon = boardSlot.transform.Find("Icon").GetComponent<Image>();
            Assert.That(icon.sprite, Is.SameAs(boardIcon));
            Assert.That(icon.enabled, Is.True);
            Assert.That(panel.SlotOf("charm").CurrentItem, Is.Null);
            Assert.That(panel.GetComponentsInChildren<Text>(true).Select(t => t.text), Does.Contain("CHARM"));

            worn["charm"] = new Item("lucky", "Lucky charm", "", ItemType.Equipment);
            worn.Remove("board");
            panel.Refresh();
            Assert.That(panel.SlotOf("board").CurrentItem, Is.Null);
            Assert.That(icon.enabled, Is.False);
            Assert.That(panel.SlotOf("charm").CurrentItem.ItemId, Is.EqualTo("lucky"));
            Assert.That(panel.SlotOf("charm").transform.Find("Icon").GetComponent<Image>().enabled, Is.False,
                "no picture for it: the icon stays blank");

            panel.SlotOf("charm").GetComponent<Button>().onClick.Invoke();
            Assert.That(clicked, Is.EqualTo(new[] { "charm" }));
        }

        [Test]
        public void ScreensMadeInCode_OpenThroughTheNavigation_AndAreReused()
        {
            var root = new GameObject("Screens", typeof(RectTransform), typeof(Canvas));
            _cleanup.Add(root);
            var screens = new CodeViewFactory(root.transform);
            int made = 0;
            screens.Register<StatListPanel>("stats", panel =>
            {
                made++;
                panel.Bind(new (string, System.Func<string>)[] { ("Level", () => "3") });
            });
            var navigation = new NavigationService(screens);
            screens.ViewCreated = (id, view) => ((UIViewBase)view).Navigation = navigation;

            var opened = Run(navigation.NavigateToAsync("stats"));
            Assert.That(opened, Is.InstanceOf<StatListPanel>());
            Assert.That(opened.IsVisible, Is.True);
            Assert.That(((StatListPanel)opened).ValueAt(0), Is.EqualTo("3"));

            Assert.That(((UIViewBase)opened).Navigation, Is.SameAs(navigation));
            var clear = navigation.ClearAsync();
            Assert.That(clear.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(opened.IsVisible, Is.False);
            Run(navigation.NavigateToAsync("stats"));
            Assert.That(made, Is.EqualTo(1), "made once, then reused");
            Assert.That(() => Run(navigation.NavigateToAsync("unknown")), Throws.InvalidOperationException);
            screens.Dispose();
        }

        private static T Run<T>(UniTask<T> task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            return task.GetAwaiter().GetResult();
        }
    }
}
