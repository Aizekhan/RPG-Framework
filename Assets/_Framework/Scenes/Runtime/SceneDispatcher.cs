using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Core.Async;
using RPGFramework.Events;
using RPGFramework.Logging;

namespace RPGFramework.Scenes
{
    /// <summary>
    /// Engine operations behind <see cref="SceneDispatcher"/> (Unity SceneManager, Addressables scenes, test fakes).
    /// </summary>
    public interface ISceneBackend
    {
        string ActiveScene { get; }
        bool IsLoaded(string sceneName);
        UniTask LoadAsync(string sceneName, bool additive, IProgress<float> progress, CancellationToken cancellationToken);
        UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken);
        void SetActive(string sceneName);
    }

    public enum SceneOperationKind
    {
        Load,
        LoadAdditive,
        Unload
    }

    public readonly struct SceneTransition
    {
        public SceneTransition(SceneOperationKind kind, string sceneName, string previousActiveScene)
        {
            Kind = kind;
            SceneName = sceneName;
            PreviousActiveScene = previousActiveScene;
        }

        public SceneOperationKind Kind { get; }
        public string SceneName { get; }
        public string PreviousActiveScene { get; }
    }

    /// <summary>
    /// Hook around scene operations, e.g. a loading screen or fade. Both methods are awaited.
    /// </summary>
    public interface ISceneTransitionHandler
    {
        UniTask OnTransitionStartAsync(SceneTransition transition, CancellationToken cancellationToken);
        UniTask OnTransitionEndAsync(SceneTransition transition, bool succeeded, CancellationToken cancellationToken);
    }

    public readonly struct SceneOperationStarted : IEvent
    {
        public SceneOperationStarted(SceneTransition transition) => Transition = transition;
        public SceneTransition Transition { get; }
    }

    public readonly struct SceneOperationCompleted : IEvent
    {
        public SceneOperationCompleted(SceneTransition transition, double seconds)
        {
            Transition = transition;
            Seconds = seconds;
        }

        public SceneTransition Transition { get; }
        public double Seconds { get; }
    }

    public readonly struct SceneOperationFailed : IEvent
    {
        public SceneOperationFailed(SceneTransition transition, Exception error)
        {
            Transition = transition;
            Error = error;
        }

        public SceneTransition Transition { get; }
        public Exception Error { get; }
    }

    public interface ISceneDispatcher
    {
        string ActiveScene { get; }
        bool IsBusy { get; }
        int PendingOperations { get; }

        bool IsLoaded(string sceneName);

        /// <summary>
        /// Replaces all loaded scenes with <paramref name="sceneName"/>. <paramref name="payload"/> is kept for the
        /// scene until it is unloaded/replaced (see <see cref="TryGetPayload{T}"/>).
        /// </summary>
        UniTask LoadAsync(string sceneName, object payload = null, IProgress<float> progress = null, CancellationToken cancellationToken = default);

        UniTask LoadAdditiveAsync(string sceneName, object payload = null, IProgress<float> progress = null, CancellationToken cancellationToken = default);

        UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken = default);

        bool TryGetPayload<T>(string sceneName, out T payload);

        void AddTransitionHandler(ISceneTransitionHandler handler);
        bool RemoveTransitionHandler(ISceneTransitionHandler handler);
    }

    /// <summary>
    /// Serialises scene operations (a second request waits for the first instead of racing it), runs
    /// transition handlers, keeps typed per-scene payloads and publishes start/complete/failed events.
    /// </summary>
    public sealed class SceneDispatcher : ISceneDispatcher
    {
        private const string Category = "Scenes";

        private readonly ISceneBackend _backend;
        private readonly IEventPublisher _events;
        private readonly ILogger _logger;
        private readonly List<ISceneTransitionHandler> _handlers = new List<ISceneTransitionHandler>();
        private readonly Dictionary<string, object> _payloads = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly AsyncSerialQueue _queue = new AsyncSerialQueue();

        public SceneDispatcher(ISceneBackend backend, IEventPublisher events = null, ILogger logger = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _events = events;
            _logger = logger;
        }

        public string ActiveScene => _backend.ActiveScene;
        public bool IsBusy => _queue.IsBusy;
        public int PendingOperations => _queue.PendingCount;

        public bool IsLoaded(string sceneName) => _backend.IsLoaded(sceneName);

        public UniTask LoadAsync(string sceneName, object payload = null, IProgress<float> progress = null, CancellationToken cancellationToken = default) =>
            RunAsync(SceneOperationKind.Load, sceneName, payload, progress, cancellationToken);

        public UniTask LoadAdditiveAsync(string sceneName, object payload = null, IProgress<float> progress = null, CancellationToken cancellationToken = default) =>
            RunAsync(SceneOperationKind.LoadAdditive, sceneName, payload, progress, cancellationToken);

        public UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken = default) =>
            RunAsync(SceneOperationKind.Unload, sceneName, null, null, cancellationToken);

        public bool TryGetPayload<T>(string sceneName, out T payload)
        {
            if (sceneName != null && _payloads.TryGetValue(sceneName, out var value) && value is T typed)
            {
                payload = typed;
                return true;
            }
            payload = default;
            return false;
        }

        public void AddTransitionHandler(ISceneTransitionHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!_handlers.Contains(handler))
                _handlers.Add(handler);
        }

        public bool RemoveTransitionHandler(ISceneTransitionHandler handler) => _handlers.Remove(handler);

        private async UniTask RunAsync(SceneOperationKind kind, string sceneName, object payload, IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("Scene name is required", nameof(sceneName));

            await _queue.RunAsync(() => ExecuteAsync(kind, sceneName, payload, progress, cancellationToken), cancellationToken);
        }

        private async UniTask ExecuteAsync(SceneOperationKind kind, string sceneName, object payload, IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            var transition = new SceneTransition(kind, sceneName, _backend.ActiveScene);
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInfo($"{kind} '{sceneName}' started", Category);
            _events?.Publish(new SceneOperationStarted(transition));

            int started = 0;
            bool succeeded = false;
            try
            {
                foreach (var handler in _handlers.ToArray())
                {
                    started++;
                    await handler.OnTransitionStartAsync(transition, cancellationToken);
                }

                switch (kind)
                {
                    case SceneOperationKind.Load:
                        await _backend.LoadAsync(sceneName, false, progress, cancellationToken);
                        _payloads.Clear();
                        _backend.SetActive(sceneName);
                        break;
                    case SceneOperationKind.LoadAdditive:
                        await _backend.LoadAsync(sceneName, true, progress, cancellationToken);
                        break;
                    default:
                        await _backend.UnloadAsync(sceneName, cancellationToken);
                        _payloads.Remove(sceneName);
                        break;
                }

                if (kind != SceneOperationKind.Unload)
                {
                    if (payload != null)
                        _payloads[sceneName] = payload;
                    else
                        _payloads.Remove(sceneName);
                }

                succeeded = true;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    _logger.LogWarning($"{kind} '{sceneName}' cancelled", Category);
                else
                    _logger.LogError($"{kind} '{sceneName}' failed", Category, ex);
                _events?.Publish(new SceneOperationFailed(transition, ex));
                throw;
            }
            finally
            {
                // End hooks run for every handler whose start hook ran, even on failure (hide the loading screen)
                var handlers = _handlers.ToArray();
                for (int i = Math.Min(started, handlers.Length) - 1; i >= 0; i--)
                {
                    try
                    {
                        await handlers[i].OnTransitionEndAsync(transition, succeeded, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Transition handler {handlers[i].GetType().Name} failed to end", Category, ex);
                    }
                }
            }

            _logger.LogInfo($"{kind} '{sceneName}' completed in {stopwatch.Elapsed.TotalSeconds:F2}s", Category);
            _events?.Publish(new SceneOperationCompleted(transition, stopwatch.Elapsed.TotalSeconds));
        }
    }
}
