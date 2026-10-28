using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Items;
using RPGFramework.UI.Panels;
using RPGFramework.UINavigation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.UI.Tests
{
    /// <summary>
    /// Hands out panels that already exist in the scene.
    /// </summary>
    internal sealed class ExistingViewFactory : IViewFactory
    {
        public Dictionary<string, INavigableView> Views { get; } = new Dictionary<string, INavigableView>();

        public UniTask<INavigableView> CreateAsync(string viewId, CancellationToken cancellationToken) =>
            UniTask.FromResult(Views[viewId]);

        public void Release(string viewId, INavigableView view) => view.Hide();
    }

    public sealed class UIViewBaseTests
    {
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

        private TestPanel Panel(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _cleanup.Add(go);
            return go.AddComponent<TestPanel>();
        }

        private static void Run<T>(UniTask<T> task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            task.GetAwaiter().GetResult();
        }

        [Test]
        public void ShowAndHide_ToggleVisibilityAndCallHooks()
        {
            var panel = Panel("Inventory");

            panel.Show();
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(panel.CanvasGroup.interactable, Is.True);
            Assert.That(panel.ShowCount, Is.EqualTo(1));

            panel.Hide();
            Assert.That(panel.IsVisible, Is.False);
            Assert.That(panel.CanvasGroup.blocksRaycasts, Is.False);
            Assert.That(panel.HideCount, Is.EqualTo(1));
        }

        [Test]
        public void APanelShownInsideAnInactiveScreen_StaysShownWhenTheScreenOpens()
        {
            var screen = new GameObject("Screen", typeof(RectTransform));
            _cleanup.Add(screen);
            screen.SetActive(false);
            var child = new GameObject("Stats", typeof(RectTransform));
            child.transform.SetParent(screen.transform, false);
            var shown = child.AddComponent<TestPanel>();
            shown.Show();
            var untouched = new GameObject("Untouched", typeof(RectTransform));
            untouched.transform.SetParent(screen.transform, false);
            untouched.AddComponent<TestPanel>();

            screen.SetActive(true);

            Assert.That(shown.IsVisible, Is.True, "shown before its Awake: stays shown");
            Assert.That(untouched.activeSelf, Is.False, "never shown: hides itself on Awake as before");
        }

        [Test]
        public void StartHidden_CanBeTurnedOffFromCode()
        {
            var screen = new GameObject("Screen", typeof(RectTransform));
            _cleanup.Add(screen);
            screen.SetActive(false);
            var child = new GameObject("Hud", typeof(RectTransform));
            child.transform.SetParent(screen.transform, false);
            var panel = child.AddComponent<TestPanel>();
            Assert.That(panel.StartHidden, Is.True);
            panel.StartHidden = false;

            screen.SetActive(true);

            Assert.That(panel.IsVisible, Is.True);
        }

        [Test]
        public void Close_WithoutNavigation_HidesThePanel()
        {
            var panel = Panel("Loot");
            panel.Show();

            panel.Close();

            Assert.That(panel.IsVisible, Is.False);
        }

        [Test]
        public void Panel_IsUsableAsNavigationScreen()
        {
            var factory = new ExistingViewFactory();
            var hud = Panel("Hud");
            var inventory = Panel("Inventory");
            factory.Views["Hud"] = hud;
            factory.Views["Inventory"] = inventory;
            var navigation = new NavigationService(factory);
            inventory.Navigation = navigation;

            Run(navigation.NavigateToAsync("Hud"));
            Run(navigation.NavigateToAsync("Inventory"));
            Assert.That(inventory.IsVisible, Is.True);
            Assert.That(navigation.CurrentViewId, Is.EqualTo("Inventory"));

            inventory.Close();

            Assert.That(navigation.CurrentViewId, Is.EqualTo("Hud"));
            Assert.That(inventory.IsVisible, Is.False);
            Assert.That(hud.IsVisible, Is.True);
        }

        [Test]
        public void Close_WhenNotTheCurrentScreen_OnlyHides()
        {
            var factory = new ExistingViewFactory();
            var hud = Panel("Hud");
            var stale = Panel("Stale");
            factory.Views["Hud"] = hud;
            var navigation = new NavigationService(factory);
            stale.Navigation = navigation;
            Run(navigation.NavigateToAsync("Hud"));
            stale.Show();

            stale.Close();

            Assert.That(stale.IsVisible, Is.False);
            Assert.That(navigation.CurrentViewId, Is.EqualTo("Hud"));
        }
    }

    public sealed class ItemSlotUITests
    {
        private GameObject _go;
        private ItemSlotUI _slot;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Slot");
            _slot = _go.AddComponent<ItemSlotUI>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private static IItem Sword() => new Item("sword", "Sword", "Sharp", default(ItemType));

        [Test]
        public void SetItem_StoresItemAndNullClears()
        {
            var sword = Sword();

            _slot.SetItem(sword);
            Assert.That(_slot.CurrentItem, Is.SameAs(sword));

            _slot.SetItem(null);
            Assert.That(_slot.CurrentItem, Is.Null);
        }

        [Test]
        public void PointerEnter_RaisesHoverOnlyForFilledSlot()
        {
            var hovered = new List<IItem>();
            _slot.OnSlotHoverEnter += hovered.Add;

            _slot.OnPointerEnter();
            _slot.SetItem(Sword());
            _slot.OnPointerEnter();

            Assert.That(hovered, Has.Count.EqualTo(1));
        }

        [Test]
        public void PointerExit_RaisesHoverExit()
        {
            int exits = 0;
            _slot.OnSlotHoverExit += () => exits++;

            _slot.OnPointerExit();

            Assert.That(exits, Is.EqualTo(1));
        }
    }
}
