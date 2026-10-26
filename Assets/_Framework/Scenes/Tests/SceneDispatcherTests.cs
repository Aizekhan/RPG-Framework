using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Scenes.Tests
{
    internal sealed class FakeSceneBackend : ISceneBackend
    {
        private readonly HashSet<string> _loaded = new HashSet<string> { "Boot" };
        private readonly Dictionary<string, UniTaskCompletionSource> _gates = new Dictionary<string, UniTaskCompletionSource>();

        public string ActiveScene { get; private set; } = "Boot";
        public List<string> Log { get; } = new List<string>();
        public HashSet<string> Missing { get; } = new HashSet<string>();

        public UniTaskCompletionSource Gate(string scene)
        {
            var gate = new UniTaskCompletionSource();
            _gates[scene] = gate;
            return gate;
        }

        public bool IsLoaded(string sceneName) => _loaded.Contains(sceneName);

        public async UniTask LoadAsync(string sceneName, bool additive, IProgress<float> progress, CancellationToken cancellationToken)
        {
            Log.Add($"load {sceneName}{(additive ? " +" : "")}");
            if (_gates.TryGetValue(sceneName, out var gate))
                await gate.Task.AttachExternalCancellation(cancellationToken);
            if (Missing.Contains(sceneName))
                throw new InvalidOperationException($"no scene {sceneName}");
            if (!additive)
                _loaded.Clear();
            _loaded.Add(sceneName);
            progress?.Report(1f);
            Log.Add($"loaded {sceneName}");
        }

        public UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken)
        {
            Log.Add($"unload {sceneName}");
            _loaded.Remove(sceneName);
            return UniTask.CompletedTask;
        }

        public void SetActive(string sceneName) => ActiveScene = sceneName;
    }

    internal sealed class LoadingScreen : ISceneTransitionHandler
    {
        private readonly List<string> _log;
        public LoadingScreen(List<string> log) => _log = log;

        public UniTask OnTransitionStartAsync(SceneTransition transition, CancellationToken cancellationToken)
        {
            _log.Add($"show {transition.SceneName}");
            return UniTask.CompletedTask;
        }

        public UniTask OnTransitionEndAsync(SceneTransition transition, bool succeeded, CancellationToken cancellationToken)
        {
            _log.Add($"hide {transition.SceneName} {(succeeded ? "ok" : "failed")}");
            return UniTask.CompletedTask;
        }
    }

    internal sealed class HeroSelection
    {
        public string[] Heroes;
    }

    public sealed class SceneDispatcherTests
    {
        private FakeSceneBackend _backend;
        private EventBus _bus;
        private SceneDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeSceneBackend();
            _bus = new EventBus();
            _dispatcher = new SceneDispatcher(_backend, _bus);
        }

        private static void Complete(UniTask task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            task.GetAwaiter().GetResult();
        }

        [Test]
        public void Load_ReplacesScenes_SetsActive_StoresTypedPayload()
        {
            var selection = new HeroSelection { Heroes = new[] { "Warrior" } };

            Complete(_dispatcher.LoadAsync("Game", selection));

            Assert.That(_dispatcher.ActiveScene, Is.EqualTo("Game"));
            Assert.That(_dispatcher.IsLoaded("Boot"), Is.False);
            Assert.That(_dispatcher.TryGetPayload<HeroSelection>("Game", out var payload), Is.True);
            Assert.That(payload, Is.SameAs(selection));
            Assert.That(_dispatcher.TryGetPayload<string>("Game", out _), Is.False);
        }

        [Test]
        public void SingleLoad_ClearsPayloadsOfReplacedScenes_UnloadClearsOwnPayload()
        {
            Complete(_dispatcher.LoadAsync("Game", "a"));
            Complete(_dispatcher.LoadAdditiveAsync("Hud", "b"));

            Complete(_dispatcher.UnloadAsync("Hud"));
            Assert.That(_dispatcher.TryGetPayload<string>("Hud", out _), Is.False);
            Assert.That(_dispatcher.TryGetPayload<string>("Game", out _), Is.True);

            Complete(_dispatcher.LoadAsync("Menu"));
            Assert.That(_dispatcher.TryGetPayload<string>("Game", out _), Is.False);
        }

        [Test]
        public void ConcurrentRequests_RunOneAfterAnother_InOrder()
        {
            var gate = _backend.Gate("Game");

            var first = _dispatcher.LoadAsync("Game");
            var second = _dispatcher.LoadAdditiveAsync("Hud");
            var third = _dispatcher.UnloadAsync("Boot");

            Assert.That(_dispatcher.IsBusy, Is.True);
            Assert.That(_dispatcher.PendingOperations, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "load Game" }, _backend.Log);

            gate.TrySetResult();

            Complete(first);
            Complete(second);
            Complete(third);
            CollectionAssert.AreEqual(new[] { "load Game", "loaded Game", "load Hud +", "loaded Hud", "unload Boot" }, _backend.Log);
            Assert.That(_dispatcher.IsBusy, Is.False);
        }

        [Test]
        public void CancelledWhileQueued_IsSkipped_QueueKeepsMoving()
        {
            var gate = _backend.Gate("Game");
            var cts = new CancellationTokenSource();

            var first = _dispatcher.LoadAsync("Game");
            var cancelled = _dispatcher.LoadAdditiveAsync("Hud", cancellationToken: cts.Token);
            var third = _dispatcher.LoadAdditiveAsync("Shop");
            cts.Cancel();
            gate.TrySetResult();

            Assert.That(cancelled.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Complete(first);
            Complete(third);
            Assert.That(_backend.Log, Has.No.Member("load Hud +"));
            Assert.That(_backend.Log, Has.Member("loaded Shop"));
            Assert.That(_dispatcher.IsBusy, Is.False);
        }

        [Test]
        public void TransitionHandlers_WrapOperation_AndEndRunsOnFailure()
        {
            var log = new List<string>();
            _dispatcher.AddTransitionHandler(new LoadingScreen(log));
            _backend.Missing.Add("Broken");

            Complete(_dispatcher.LoadAsync("Game"));
            var failed = _dispatcher.LoadAsync("Broken");

            Assert.Throws<InvalidOperationException>(() => failed.GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "show Game", "hide Game ok", "show Broken", "hide Broken failed" }, log);
            Assert.That(_dispatcher.IsBusy, Is.False);
        }

        [Test]
        public void Events_ArePublished_ForSuccessAndFailure()
        {
            var seen = new List<string>();
            _bus.Subscribe<SceneOperationStarted>(e => seen.Add($"started {e.Transition.SceneName} from {e.Transition.PreviousActiveScene}"));
            _bus.Subscribe<SceneOperationCompleted>(e => seen.Add($"completed {e.Transition.SceneName}"));
            _bus.Subscribe<SceneOperationFailed>(e => seen.Add($"failed {e.Transition.SceneName}: {e.Error.Message}"));
            _backend.Missing.Add("Broken");

            Complete(_dispatcher.LoadAsync("Game"));
            try
            {
                _dispatcher.LoadAsync("Broken").GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
            }

            CollectionAssert.AreEqual(new[]
            {
                "started Game from Boot", "completed Game", "started Broken from Game", "failed Broken: no scene Broken"
            }, seen);
        }

        [Test]
        public void Progress_IsForwardedToBackend()
        {
            var reported = new List<float>();
            Complete(_dispatcher.LoadAsync("Game", progress: new SyncProgress(reported.Add)));

            CollectionAssert.AreEqual(new[] { 1f }, reported);
        }

        [Test]
        public void EmptySceneName_Throws()
        {
            Assert.Throws<ArgumentException>(() => _dispatcher.LoadAsync("").GetAwaiter().GetResult());
        }

        private sealed class SyncProgress : IProgress<float>
        {
            private readonly Action<float> _report;
            public SyncProgress(Action<float> report) => _report = report;
            public void Report(float value) => _report(value);
        }
    }
}
