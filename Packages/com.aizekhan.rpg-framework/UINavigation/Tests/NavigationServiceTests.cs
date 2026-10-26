using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Scenes;

namespace RPGFramework.UINavigation.Tests
{
    internal class FakeView : INavigableView
    {
        private readonly List<string> _log;

        public FakeView(string id, List<string> log)
        {
            Id = id;
            _log = log;
        }

        public string Id { get; }
        public bool IsVisible { get; private set; }
        public UniTaskCompletionSource OpenGate { get; set; }
        public Exception OpenFailure { get; set; }
        public Exception NavigatedFromFailure { get; set; }
        public NavigationParameters LastParameters { get; private set; }

        public void Show()
        {
            IsVisible = true;
            _log.Add($"show {Id}");
        }

        public void Hide()
        {
            IsVisible = false;
            _log.Add($"hide {Id}");
        }

        public async UniTask OnOpenedAsync(NavigationParameters parameters, CancellationToken cancellationToken)
        {
            _log.Add($"open {Id}");
            LastParameters = parameters;
            if (OpenGate != null)
                await OpenGate.Task.AttachExternalCancellation(cancellationToken);
            if (OpenFailure != null)
                throw OpenFailure;
        }

        public UniTask OnNavigatedToAsync(NavigationParameters parameters, CancellationToken cancellationToken)
        {
            _log.Add($"to {Id}");
            LastParameters = parameters;
            return UniTask.CompletedTask;
        }

        public UniTask OnNavigatedFromAsync(CancellationToken cancellationToken)
        {
            _log.Add($"from {Id}");
            if (NavigatedFromFailure != null)
                throw NavigatedFromFailure;
            return UniTask.CompletedTask;
        }

        public UniTask OnClosedAsync(CancellationToken cancellationToken)
        {
            _log.Add($"close {Id}");
            return UniTask.CompletedTask;
        }
    }

    internal sealed class FakeModal : FakeView, IModalView<string>
    {
        private Action<string> _complete;

        public FakeModal(string id, List<string> log) : base(id, log)
        {
        }

        public void BindResult(Action<string> complete) => _complete = complete;

        public void Complete(string result) => _complete(result);
    }

    internal sealed class FakeFactory : IViewFactory
    {
        private readonly List<string> _log;

        public FakeFactory(List<string> log)
        {
            _log = log;
        }

        public Dictionary<string, FakeView> Views { get; } = new Dictionary<string, FakeView>();
        public List<string> Released { get; } = new List<string>();

        public FakeView Add(string id) => Views[id] = new FakeView(id, _log);

        public FakeModal AddModal(string id)
        {
            var modal = new FakeModal(id, _log);
            Views[id] = modal;
            return modal;
        }

        public UniTask<INavigableView> CreateAsync(string viewId, CancellationToken cancellationToken)
        {
            if (!Views.TryGetValue(viewId, out var view))
                throw new KeyNotFoundException(viewId);
            return UniTask.FromResult<INavigableView>(view);
        }

        public void Release(string viewId, INavigableView view) => Released.Add(viewId);
    }

    internal sealed class RecordingTransition : IViewTransition
    {
        public List<TransitionRequest> Requests { get; } = new List<TransitionRequest>();
        public bool Fail { get; set; }

        public UniTask PlayAsync(TransitionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Fail)
                throw new InvalidOperationException("animation broke");
            InstantTransition.Apply(request);
            return UniTask.CompletedTask;
        }
    }

    public sealed class NavigationServiceTests
    {
        private List<string> _log;
        private FakeFactory _factory;
        private RecordingTransition _transition;
        private EventBus _bus;
        private MemoryLogSink _sink;
        private NavigationService _nav;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _factory = new FakeFactory(_log);
            foreach (var id in new[] { "Menu", "Lobby", "Shop", "Game" })
                _factory.Add(id);
            _transition = new RecordingTransition();
            _bus = new EventBus();
            _sink = new MemoryLogSink();
            _nav = new NavigationService(_factory, _transition, _bus, new Logger(LogSeverity.Trace, _sink));
        }

        private static T Done<T>(UniTask<T> task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            return task.GetAwaiter().GetResult();
        }

        private static void Done(UniTask task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            task.GetAwaiter().GetResult();
        }

        private bool HasError() => _sink.Snapshot().Any(e => e.Severity == LogSeverity.Error);

        [Test]
        public void Navigate_OpensBeforeShowing_AndCoversPreviousScreen()
        {
            Done(_nav.NavigateToAsync("Menu"));
            _log.Clear();

            var view = Done(_nav.NavigateToAsync("Lobby", NavigationParameters.With("party", 3)));

            Assert.That(view, Is.SameAs(_factory.Views["Lobby"]));
            CollectionAssert.AreEqual(new[] { "open Lobby", "from Menu", "hide Menu", "show Lobby", "to Lobby" }, _log);
            Assert.That(_factory.Views["Lobby"].LastParameters.Get<int>("party"), Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "Menu", "Lobby" }, _nav.History);
            Assert.That(_nav.CurrentViewId, Is.EqualTo("Lobby"));
            Assert.That(_factory.Released, Is.Empty);
        }

        [Test]
        public void GoBack_ReturnsToPreviousScreenWithItsParameters_AndClosesTheLeavingOne()
        {
            Done(_nav.NavigateToAsync("Menu", NavigationParameters.With("tab", "play")));
            Done(_nav.NavigateToAsync("Lobby"));
            _log.Clear();

            Assert.That(Done(_nav.GoBackAsync()), Is.True);

            CollectionAssert.AreEqual(new[] { "from Lobby", "hide Lobby", "show Menu", "close Lobby", "to Menu" }, _log);
            Assert.That(_factory.Views["Menu"].LastParameters.Get<string>("tab"), Is.EqualTo("play"));
            CollectionAssert.AreEqual(new[] { "Lobby" }, _factory.Released);
            Assert.That(_transition.Requests.Last().Direction, Is.EqualTo(NavigationDirection.Back));

            Assert.That(Done(_nav.GoBackAsync()), Is.False, "root cannot go back");
            Assert.That(_nav.CanGoBack, Is.False);
        }

        [Test]
        public void Replace_And_ClearHistory_DropScreensFromHistory()
        {
            Done(_nav.NavigateToAsync("Menu"));
            Done(_nav.NavigateToAsync("Lobby", options: new NavigationOptions { Replace = true }));
            CollectionAssert.AreEqual(new[] { "Lobby" }, _nav.History);
            CollectionAssert.AreEqual(new[] { "Menu" }, _factory.Released);

            Done(_nav.NavigateToAsync("Shop"));
            Done(_nav.NavigateToAsync("Game", options: new NavigationOptions { ClearHistory = true }));

            CollectionAssert.AreEqual(new[] { "Game" }, _nav.History);
            CollectionAssert.AreEqual(new[] { "Menu", "Shop", "Lobby" }, _factory.Released);
        }

        [Test]
        public void GoToRoot_ClosesEverythingAboveRoot()
        {
            Done(_nav.NavigateToAsync("Menu"));
            Done(_nav.NavigateToAsync("Lobby"));
            Done(_nav.NavigateToAsync("Shop"));
            _log.Clear();

            Done(_nav.GoToRootAsync());

            CollectionAssert.AreEqual(new[] { "from Shop", "hide Shop", "show Menu", "close Shop", "close Lobby", "to Menu" }, _log);
            CollectionAssert.AreEqual(new[] { "Menu" }, _nav.History);
        }

        [Test]
        public void MaxDepth_ClosesOldestScreenAboveRoot()
        {
            _nav.MaxDepth = 3;
            Done(_nav.NavigateToAsync("Menu"));
            Done(_nav.NavigateToAsync("Lobby"));
            Done(_nav.NavigateToAsync("Shop"));
            Done(_nav.NavigateToAsync("Game"));

            CollectionAssert.AreEqual(new[] { "Menu", "Shop", "Game" }, _nav.History);
            CollectionAssert.AreEqual(new[] { "Lobby" }, _factory.Released);
            Assert.Throws<ArgumentOutOfRangeException>(() => _nav.MaxDepth = 1);
        }

        [Test]
        public void Clear_HidesAndClosesAllScreens()
        {
            Done(_nav.NavigateToAsync("Menu"));
            Done(_nav.NavigateToAsync("Lobby"));

            Done(_nav.ClearAsync());

            Assert.That(_nav.History, Is.Empty);
            Assert.That(_nav.CurrentView, Is.Null);
            Assert.That(_factory.Views["Lobby"].IsVisible, Is.False);
            CollectionAssert.AreEqual(new[] { "Lobby", "Menu" }, _factory.Released);
        }

        [Test]
        public void OpenFailure_AbortsNavigation_ReleasesView_KeepsCurrentScreen()
        {
            Done(_nav.NavigateToAsync("Menu"));
            _factory.Views["Lobby"].OpenFailure = new InvalidOperationException("no data");

            var failed = _nav.NavigateToAsync("Lobby");

            Assert.Throws<InvalidOperationException>(() => failed.GetAwaiter().GetResult());
            Assert.That(_nav.CurrentViewId, Is.EqualTo("Menu"));
            Assert.That(_factory.Views["Menu"].IsVisible, Is.True);
            CollectionAssert.AreEqual(new[] { "Lobby" }, _factory.Released);
            Assert.That(_nav.IsBusy, Is.False);
            Assert.That(HasError(), Is.True);
        }

        [Test]
        public void FailingHookOrTransition_IsLogged_SwitchStillCompletes()
        {
            Done(_nav.NavigateToAsync("Menu"));
            _factory.Views["Menu"].NavigatedFromFailure = new InvalidOperationException("save failed");
            _transition.Fail = true;

            Done(_nav.NavigateToAsync("Lobby"));

            Assert.That(_nav.CurrentViewId, Is.EqualTo("Lobby"));
            Assert.That(_factory.Views["Menu"].IsVisible, Is.False);
            Assert.That(_factory.Views["Lobby"].IsVisible, Is.True);
            Assert.That(HasError(), Is.True);
        }

        [Test]
        public void RequestsDuringNavigation_RunAfterIt()
        {
            _factory.Views["Menu"].OpenGate = new UniTaskCompletionSource();
            var toMenu = _nav.NavigateToAsync("Menu");
            var toLobby = _nav.NavigateToAsync("Lobby");

            Assert.That(_nav.IsBusy, Is.True);
            Assert.That(_nav.History, Is.Empty);

            _factory.Views["Menu"].OpenGate.TrySetResult();
            Done(toMenu);
            Done(toLobby);

            CollectionAssert.AreEqual(new[] { "Menu", "Lobby" }, _nav.History);
        }

        [Test]
        public void CancelWhileOpening_LeavesHistoryUnchanged()
        {
            Done(_nav.NavigateToAsync("Menu"));
            _factory.Views["Lobby"].OpenGate = new UniTaskCompletionSource();
            var cts = new CancellationTokenSource();

            var navigation = _nav.NavigateToAsync("Lobby", cancellationToken: cts.Token);
            cts.Cancel();

            Assert.That(navigation.Status, Is.EqualTo(UniTaskStatus.Canceled));
            CollectionAssert.AreEqual(new[] { "Menu" }, _nav.History);
            CollectionAssert.AreEqual(new[] { "Lobby" }, _factory.Released);
            Assert.That(HasError(), Is.False, "cancellation is not an error");
        }

        [Test]
        public void Modal_ReturnsResult_AndStaysOverCurrentScreen()
        {
            var dialog = _factory.AddModal("Confirm");
            Done(_nav.NavigateToAsync("Menu"));
            _log.Clear();

            var result = _nav.ShowModalAsync<string>("Confirm", NavigationParameters.With("text", "Quit?"));

            Assert.That(result.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_nav.ModalCount, Is.EqualTo(1));
            Assert.That(_factory.Views["Menu"].IsVisible, Is.True);
            Assert.That(dialog.LastParameters.Get<string>("text"), Is.EqualTo("Quit?"));

            dialog.Complete("yes");

            Assert.That(Done(result), Is.EqualTo("yes"));
            Assert.That(_nav.ModalCount, Is.Zero);
            CollectionAssert.AreEqual(new[] { "open Confirm", "show Confirm", "to Confirm", "from Confirm", "hide Confirm", "close Confirm" }, _log);
            CollectionAssert.AreEqual(new[] { "Confirm" }, _factory.Released);
        }

        [Test]
        public void Modal_DismissedByBackTokenOrNavigation_IsCancelled()
        {
            var dialog = _factory.AddModal("Confirm");
            Done(_nav.NavigateToAsync("Menu"));

            var byBack = _nav.ShowModalAsync<string>("Confirm");
            Assert.That(_nav.CanGoBack, Is.True);
            Assert.That(Done(_nav.GoBackAsync()), Is.True);
            Assert.That(byBack.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_nav.CurrentViewId, Is.EqualTo("Menu"), "back only closed the modal");

            var cts = new CancellationTokenSource();
            var byToken = _nav.ShowModalAsync<string>("Confirm", cancellationToken: cts.Token);
            cts.Cancel();
            Assert.That(byToken.Status, Is.EqualTo(UniTaskStatus.Canceled));

            var byNavigation = _nav.ShowModalAsync<string>("Confirm");
            Done(_nav.NavigateToAsync("Lobby"));
            Assert.That(byNavigation.Status, Is.EqualTo(UniTaskStatus.Canceled));

            Assert.That(_nav.ModalCount, Is.Zero);
            Assert.That(dialog.IsVisible, Is.False);
            dialog.Complete("late");
            Assert.That(_factory.Released.Count(id => id == "Confirm"), Is.EqualTo(3), "late result is ignored");
        }

        [Test]
        public void Modal_WrongResultType_Throws_AndReleasesView()
        {
            _factory.AddModal("Confirm");

            var wrong = _nav.ShowModalAsync<int>("Confirm");

            Assert.Throws<InvalidOperationException>(() => wrong.GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "Confirm" }, _factory.Released);
        }

        [Test]
        public void TransitionRules_ResolveSpecificPairsBeforeDefaults()
        {
            _nav.Transitions.Set("Menu", "Lobby", TransitionStyle.SlideUp).Set(null, "Shop", TransitionStyle.Scale);
            _nav.Transitions.DefaultBack = TransitionStyle.SlideRight;

            Done(_nav.NavigateToAsync("Menu"));
            Done(_nav.NavigateToAsync("Lobby"));
            Done(_nav.NavigateToAsync("Shop"));
            Done(_nav.NavigateToAsync("Game", options: new NavigationOptions { Transition = TransitionStyle.None }));
            Done(_nav.GoBackAsync()); // Game -> Shop: the any -> Shop rule also applies going back
            Done(_nav.GoBackAsync()); // Shop -> Lobby: no rule, back default

            CollectionAssert.AreEqual(
                new[] { TransitionStyle.Fade, TransitionStyle.SlideUp, TransitionStyle.Scale, TransitionStyle.None, TransitionStyle.Scale, TransitionStyle.SlideRight },
                _transition.Requests.Select(r => r.Style));
        }

        [Test]
        public void Events_DescribeEveryChange()
        {
            var events = new List<NavigationChangedEvent>();
            _bus.Subscribe<NavigationChangedEvent>(events.Add);
            _factory.AddModal("Confirm");

            Done(_nav.NavigateToAsync("Menu"));
            Done(_nav.NavigateToAsync("Lobby"));
            var modal = _nav.ShowModalAsync<string>("Confirm");
            Done(_nav.GoBackAsync());
            Done(_nav.GoBackAsync());

            CollectionAssert.AreEqual(
                new[] { NavigationAction.Push, NavigationAction.Push, NavigationAction.ModalOpened, NavigationAction.ModalClosed, NavigationAction.Back },
                events.Select(e => e.Action));
            Assert.That(events[1].FromViewId, Is.EqualTo("Menu"));
            Assert.That(events[1].Depth, Is.EqualTo(2));
            Assert.That(events.Last().ToViewId, Is.EqualTo("Menu"));
            Assert.That(modal.Status, Is.EqualTo(UniTaskStatus.Canceled));
        }

        [Test]
        public void SceneRoot_ClearsHistoryAndOpensMappedView()
        {
            _nav.MapSceneRoot("LobbyScene", "Lobby");
            Done(_nav.NavigateToAsync("Menu"));

            var view = Done(_nav.ShowSceneRootAsync("LobbyScene"));

            Assert.That(view, Is.SameAs(_factory.Views["Lobby"]));
            CollectionAssert.AreEqual(new[] { "Lobby" }, _nav.History);
            Assert.That(_transition.Requests.Last().Style, Is.EqualTo(TransitionStyle.None));

            Assert.That(Done(_nav.ShowSceneRootAsync("Unmapped")), Is.Null);
            Assert.That(_nav.History, Is.Empty);
        }

        [Test]
        public void SceneNavigationHandler_FollowsSingleSceneLoads()
        {
            _nav.MapSceneRoot("Game", "Game");
            var scenes = new SceneDispatcher(new InstantSceneBackend());
            scenes.AddTransitionHandler(new SceneNavigationHandler(_nav));
            Done(_nav.NavigateToAsync("Menu"));

            Done(scenes.LoadAdditiveAsync("Overlay"));
            CollectionAssert.AreEqual(new[] { "Menu" }, _nav.History, "additive loads leave navigation alone");

            Done(scenes.LoadAsync("Game"));
            CollectionAssert.AreEqual(new[] { "Game" }, _nav.History);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentException>(() => _nav.NavigateToAsync(""));
            Assert.Throws<ArgumentException>(() => _nav.MapSceneRoot(null, "Menu"));
            Assert.Throws<ArgumentException>(() => _nav.Transitions.Set(null, null, TransitionStyle.Fade));
            Assert.Throws<ArgumentException>(() => _nav.Transitions.Set("a", "b", TransitionStyle.Default));
            Assert.Throws<ArgumentNullException>(() => new NavigationService(null));

            var missing = _nav.NavigateToAsync("Nope");
            Assert.Throws<KeyNotFoundException>(() => missing.GetAwaiter().GetResult());
            Assert.That(_nav.IsBusy, Is.False);
        }

        private sealed class InstantSceneBackend : ISceneBackend
        {
            private readonly HashSet<string> _loaded = new HashSet<string>();

            public string ActiveScene { get; private set; }
            public bool IsLoaded(string sceneName) => _loaded.Contains(sceneName);

            public UniTask LoadAsync(string sceneName, bool additive, IProgress<float> progress, CancellationToken cancellationToken)
            {
                if (!additive)
                    _loaded.Clear();
                _loaded.Add(sceneName);
                return UniTask.CompletedTask;
            }

            public UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken)
            {
                _loaded.Remove(sceneName);
                return UniTask.CompletedTask;
            }

            public void SetActive(string sceneName) => ActiveScene = sceneName;
        }
    }
}
