using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.AssetManagement;
using RPGFramework.UINavigation.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.UINavigation.Tests
{
    public sealed class PrefabViewFactoryTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private Transform _root;
        private PrefabViewFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _root = Track(new GameObject("UIRoot", typeof(RectTransform))).transform;
            _factory = new PrefabViewFactory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
            foreach (var obj in _cleanup)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _cleanup.Add(obj);
            return obj;
        }

        private GameObject Prefab<TView>(string name) where TView : Component
        {
            var prefab = Track(new GameObject(name, typeof(RectTransform), typeof(TView)));
            prefab.SetActive(false);
            return prefab;
        }

        private static T Done<T>(UniTask<T> task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            return task.GetAwaiter().GetResult();
        }

        [Test]
        public void Create_InstantiatesHiddenViewUnderItsLayer()
        {
            var modals = Track(new GameObject("Modals", typeof(RectTransform))).transform;
            _factory.SetLayer("Modals", modals);
            _factory.Register(ViewConfig.Create("Menu", Prefab<TestScreen>("MenuPrefab")));
            _factory.Register(ViewConfig.Create("Confirm", Prefab<TestDialog>("ConfirmPrefab"), layer: "Modals"));
            var created = new List<GameObject>();
            _factory.InstanceCreated = created.Add;

            var menu = (TestScreen)Done(_factory.CreateAsync("Menu", CancellationToken.None));
            var confirm = (TestDialog)Done(_factory.CreateAsync("Confirm", CancellationToken.None));

            Assert.That(menu.transform.parent, Is.SameAs(_root));
            Assert.That(confirm.transform.parent, Is.SameAs(modals));
            Assert.That(menu.IsVisible, Is.False);
            CollectionAssert.AreEqual(new[] { menu.gameObject, confirm.gameObject }, created);
        }

        [Test]
        public void KeptInstances_AreReused_OthersAreDestroyed()
        {
            _factory.Register(ViewConfig.Create("Menu", Prefab<TestScreen>("MenuPrefab")));
            _factory.Register(ViewConfig.Create("Shop", Prefab<TestScreen>("ShopPrefab"), keepInstance: false));

            var menu = Done(_factory.CreateAsync("Menu", CancellationToken.None));
            _factory.Release("Menu", menu);
            Assert.That(_factory.IdleCount("Menu"), Is.EqualTo(1));
            Assert.That(Done(_factory.CreateAsync("Menu", CancellationToken.None)), Is.SameAs(menu));

            var shop = (TestScreen)Done(_factory.CreateAsync("Shop", CancellationToken.None));
            _factory.Release("Shop", shop);
            Assert.That(shop == null, Is.True, "destroyed");
            Assert.That(_factory.IdleCount("Shop"), Is.Zero);
        }

        [Test]
        public void Preload_ParksOneInstance_ClearCacheDestroysIt()
        {
            _factory.Register(ViewConfig.Create("Menu", Prefab<TestScreen>("MenuPrefab")));

            _factory.PreloadAsync(new[] { "Menu" }).GetAwaiter().GetResult();
            Assert.That(_factory.IdleCount("Menu"), Is.EqualTo(1));
            var parked = _root.GetChild(0).gameObject;

            _factory.ClearCache();

            Assert.That(parked == null, Is.True);
            Assert.That(_factory.IdleCount("Menu"), Is.Zero);
        }

        [Test]
        public void AssetKey_LoadsThroughAssetManager_AndReleasesWithLastInstance()
        {
            var prefab = Prefab<TestScreen>("RemoteMenu");
            var assets = new AssetManager(null, new SingleAssetProvider("ui/menu", prefab));
            _factory.Dispose();
            _factory = new PrefabViewFactory(_root, assets);
            _factory.Register(ViewConfig.Create("Menu", assetKey: "ui/menu", keepInstance: false));

            var menu = Done(_factory.CreateAsync("Menu", CancellationToken.None));
            Assert.That(assets.GetReferenceCount("ui/menu"), Is.EqualTo(1));

            _factory.Release("Menu", menu);
            Assert.That(assets.GetReferenceCount("ui/menu"), Is.Zero);
        }

        [Test]
        public void PrefabWithoutView_Throws_AndLeavesNothingBehind()
        {
            var plain = Track(new GameObject("Plain", typeof(RectTransform)));
            _factory.Register(ViewConfig.Create("Plain", plain));

            var create = _factory.CreateAsync("Plain", CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() => create.GetAwaiter().GetResult());
            Assert.That(_root.childCount, Is.Zero);
        }

        [Test]
        public void Register_RejectsInvalidConfigs()
        {
            var prefab = Prefab<TestScreen>("MenuPrefab");
            _factory.Register(ViewConfig.Create("Menu", prefab));

            Assert.Throws<InvalidOperationException>(() => _factory.Register(ViewConfig.Create("Menu", prefab)));
            Assert.Throws<ArgumentException>(() => _factory.Register(ViewConfig.Create("", prefab)));
            Assert.Throws<ArgumentException>(() => _factory.Register(ViewConfig.Create("Empty")));
            Assert.Throws<ArgumentException>(() => _factory.Register(ViewConfig.Create("Keyed", assetKey: "ui/x")), "no asset manager");
            Assert.Throws<KeyNotFoundException>(() => _factory.CreateAsync("Unknown", CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void EndToEnd_NavigationWithPrefabsAndModalResult()
        {
            _factory.Register(ViewConfig.Create("Menu", Prefab<TestScreen>("MenuPrefab")));
            _factory.Register(ViewConfig.Create("Lobby", Prefab<TestScreen>("LobbyPrefab")));
            _factory.Register(ViewConfig.Create("Confirm", Prefab<TestDialog>("ConfirmPrefab")));
            var nav = new NavigationService(_factory, new CanvasGroupTransition());

            var menu = (TestScreen)Done(nav.NavigateToAsync("Menu"));
            var lobby = (TestScreen)Done(nav.NavigateToAsync("Lobby"));
            Assert.That(menu.IsVisible, Is.False);
            Assert.That(lobby.IsVisible, Is.True);
            Assert.That(lobby.CanvasGroup.alpha, Is.EqualTo(1f));

            var answer = nav.ShowModalAsync<bool>("Confirm");
            var dialog = _root.GetComponentInChildren<TestDialog>();
            Assert.That(dialog.CanComplete, Is.True);
            dialog.Answer(true);

            Assert.That(Done(answer), Is.True);
            Assert.That(dialog.IsVisible, Is.False);
            Assert.That(dialog.CanComplete, Is.False);

            Assert.That(Done(nav.GoBackAsync()), Is.True);
            Assert.That(menu.IsVisible, Is.True);
            Assert.That(_factory.IdleCount("Lobby"), Is.EqualTo(1));
        }

        private sealed class SingleAssetProvider : IAssetProvider
        {
            private readonly string _key;
            private readonly Object _asset;

            public SingleAssetProvider(string key, Object asset)
            {
                _key = key;
                _asset = asset;
            }

            public string Name => "Test";
            public int Priority => 0;

            public UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class =>
                UniTask.FromResult(key == _key ? _asset as T : null);

            public void Release(string key, object asset)
            {
            }
        }
    }
}
