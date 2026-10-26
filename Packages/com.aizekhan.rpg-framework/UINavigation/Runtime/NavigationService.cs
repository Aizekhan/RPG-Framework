using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Core.Async;
using RPGFramework.Events;
using RPGFramework.Logging;

namespace RPGFramework.UINavigation
{
    public struct NavigationOptions
    {
        public TransitionStyle Transition;

        /// <summary>
        /// The current screen is closed instead of kept for <c>GoBackAsync</c> (e.g. loading → menu).
        /// </summary>
        public bool Replace;

        /// <summary>
        /// The whole history is closed; the new screen becomes the root.
        /// </summary>
        public bool ClearHistory;
    }

    public enum NavigationAction
    {
        Push,
        Replace,
        Back,
        Root,
        Clear,
        ModalOpened,
        ModalClosed
    }

    public readonly struct NavigationChangedEvent : IEvent
    {
        public NavigationChangedEvent(NavigationAction action, string fromViewId, string toViewId, int depth)
        {
            Action = action;
            FromViewId = fromViewId;
            ToViewId = toViewId;
            Depth = depth;
        }

        public NavigationAction Action { get; }
        public string FromViewId { get; }
        public string ToViewId { get; }

        /// <summary>
        /// Screens in the history after the change (modals not included).
        /// </summary>
        public int Depth { get; }
    }

    public interface INavigationService
    {
        string CurrentViewId { get; }
        INavigableView CurrentView { get; }

        /// <summary>
        /// Screens in the history, root first.
        /// </summary>
        IReadOnlyList<string> History { get; }

        int ModalCount { get; }

        /// <summary>
        /// True when <see cref="GoBackAsync"/> would do something (dismiss a modal or return to a previous screen).
        /// </summary>
        bool CanGoBack { get; }

        bool IsBusy { get; }

        TransitionRules Transitions { get; }

        /// <summary>
        /// Opens <paramref name="viewId"/> on top of the history. Cancellation is honoured until the new view has been
        /// opened; after that the switch always completes, so the history never ends up half-changed.
        /// Open modals are dismissed: they belong to the screen under them.
        /// </summary>
        UniTask<INavigableView> NavigateToAsync(string viewId, NavigationParameters parameters = null,
            NavigationOptions options = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dismisses the top modal or returns to the previous screen. Returns false when there was nothing to go back to.
        /// </summary>
        UniTask<bool> GoBackAsync(NavigationParameters parameters = null, CancellationToken cancellationToken = default);

        UniTask GoToRootAsync(NavigationParameters parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes every modal and screen.
        /// </summary>
        UniTask ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Shows a modal over the current screen and completes with its result. Cancelling the token, going back
        /// or changing screens dismisses it and the task is cancelled.
        /// </summary>
        UniTask<TResult> ShowModalAsync<TResult>(string viewId, NavigationParameters parameters = null,
            TransitionStyle transition = TransitionStyle.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Declares the screen shown by <see cref="ShowSceneRootAsync"/> for a scene.
        /// </summary>
        void MapSceneRoot(string sceneName, string viewId);

        bool UnmapSceneRoot(string sceneName);
        bool TryGetSceneRoot(string sceneName, out string viewId);

        /// <summary>
        /// Clears the history and opens the screen mapped to the scene without a transition.
        /// Returns null when the scene has no mapping (the history is still cleared).
        /// </summary>
        UniTask<INavigableView> ShowSceneRootAsync(string sceneName, NavigationParameters parameters = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Screen history with back navigation, modals that return results, transition rules and per-scene root screens.
    /// Operations are serialised, so a request made during a transition runs after it. View lifecycle hooks and
    /// transitions that fail after a switch has started are logged and do not stop it; a failing
    /// <c>OnOpenedAsync</c> aborts the navigation and the view is released.
    /// </summary>
    public sealed class NavigationService : INavigationService
    {
        private const string Category = "Navigation";

        private readonly IViewFactory _factory;
        private readonly IViewTransition _transition;
        private readonly IEventPublisher _events;
        private readonly ILogger _logger;
        private readonly AsyncSerialQueue _queue = new AsyncSerialQueue();
        private readonly List<ScreenEntry> _stack = new List<ScreenEntry>();
        private readonly List<ModalEntry> _modals = new List<ModalEntry>();
        private readonly Dictionary<string, string> _sceneRoots = new Dictionary<string, string>(StringComparer.Ordinal);
        private int _maxDepth;

        public NavigationService(IViewFactory factory, IViewTransition transition = null, IEventPublisher events = null,
            ILogger logger = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _transition = transition ?? InstantTransition.Instance;
            _events = events;
            _logger = logger;
        }

        public TransitionRules Transitions { get; } = new TransitionRules();

        /// <summary>
        /// Maximum screens kept in the history; 0 = unlimited. When exceeded, the oldest screen above the root is closed.
        /// </summary>
        public int MaxDepth
        {
            get => _maxDepth;
            set => _maxDepth = value == 0 || value >= 2 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Use 0 (unlimited) or at least 2");
        }

        public string CurrentViewId => _stack.Count > 0 ? _stack[_stack.Count - 1].Id : null;

        public INavigableView CurrentView => _stack.Count > 0 ? _stack[_stack.Count - 1].View : null;

        public IReadOnlyList<string> History
        {
            get
            {
                var ids = new List<string>(_stack.Count);
                foreach (var entry in _stack)
                    ids.Add(entry.Id);
                return ids;
            }
        }

        public int ModalCount => _modals.Count;

        public bool CanGoBack => _modals.Count > 0 || _stack.Count > 1;

        public bool IsBusy => _queue.IsBusy;

        public UniTask<INavigableView> NavigateToAsync(string viewId, NavigationParameters parameters = null,
            NavigationOptions options = default, CancellationToken cancellationToken = default)
        {
            RequireId(viewId);
            return _queue.RunAsync(() => PushAsync(viewId, parameters, options, cancellationToken), cancellationToken);
        }

        public UniTask<bool> GoBackAsync(NavigationParameters parameters = null, CancellationToken cancellationToken = default) =>
            _queue.RunAsync(() => BackAsync(parameters), cancellationToken);

        public UniTask GoToRootAsync(NavigationParameters parameters = null, CancellationToken cancellationToken = default) =>
            _queue.RunAsync(() => RootAsync(parameters), cancellationToken);

        public UniTask ClearAsync(CancellationToken cancellationToken = default) =>
            _queue.RunAsync(ClearCoreAsync, cancellationToken);

        public async UniTask<TResult> ShowModalAsync<TResult>(string viewId, NavigationParameters parameters = null,
            TransitionStyle transition = TransitionStyle.Default, CancellationToken cancellationToken = default)
        {
            RequireId(viewId);
            // Only opening is serialised: the modal may wait for the player while navigation goes on
            var entry = await _queue.RunAsync(() => OpenModalAsync<TResult>(viewId, parameters, transition, cancellationToken),
                cancellationToken);

            using (cancellationToken.Register(() => DismissAsync(entry).Forget()))
            {
                return await entry.Completion.Task;
            }
        }

        public void MapSceneRoot(string sceneName, string viewId)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("Scene name is required", nameof(sceneName));
            RequireId(viewId);
            _sceneRoots[sceneName] = viewId;
        }

        public bool UnmapSceneRoot(string sceneName) => sceneName != null && _sceneRoots.Remove(sceneName);

        public bool TryGetSceneRoot(string sceneName, out string viewId)
        {
            viewId = null;
            return sceneName != null && _sceneRoots.TryGetValue(sceneName, out viewId);
        }

        public UniTask<INavigableView> ShowSceneRootAsync(string sceneName, NavigationParameters parameters = null,
            CancellationToken cancellationToken = default)
        {
            return _queue.RunAsync<INavigableView>(async () =>
            {
                await ClearCoreAsync();
                if (!TryGetSceneRoot(sceneName, out var viewId))
                {
                    _logger.LogDebug($"No root view mapped for scene '{sceneName}'", Category);
                    return null;
                }
                var options = new NavigationOptions { Transition = TransitionStyle.None };
                return await PushAsync(viewId, parameters, options, cancellationToken);
            }, cancellationToken);
        }

        private async UniTask<INavigableView> PushAsync(string viewId, NavigationParameters parameters, NavigationOptions options,
            CancellationToken cancellationToken)
        {
            parameters = parameters ?? new NavigationParameters();
            var view = await OpenViewAsync(viewId, parameters, cancellationToken);

            // Committed: from here on the switch completes regardless of cancellation
            await DismissAllModalsAsync();
            var current = Top;
            if (current != null)
                await HookAsync(() => current.View.OnNavigatedFromAsync(CancellationToken.None), current.Id, "navigated-from");

            var style = Transitions.Resolve(current?.Id, viewId, options.Transition, NavigationDirection.Forward);
            await TransitionAsync(current?.View, view, style, NavigationDirection.Forward);

            var action = NavigationAction.Push;
            if (options.ClearHistory)
            {
                while (_stack.Count > 0)
                    await CloseAsync(PopTop());
                action = NavigationAction.Clear;
            }
            else if (options.Replace && current != null)
            {
                await CloseAsync(PopTop());
                action = NavigationAction.Replace;
            }

            _stack.Add(new ScreenEntry(viewId, view, parameters));
            while (_maxDepth > 0 && _stack.Count > _maxDepth)
            {
                var oldest = _stack[1];
                _stack.RemoveAt(1);
                await CloseAsync(oldest);
            }

            await HookAsync(() => view.OnNavigatedToAsync(parameters, CancellationToken.None), viewId, "navigated-to");
            Changed(action, current?.Id, viewId);
            return view;
        }

        private async UniTask<bool> BackAsync(NavigationParameters parameters)
        {
            if (_modals.Count > 0)
            {
                await DismissAsync(_modals[_modals.Count - 1]);
                return true;
            }
            if (_stack.Count < 2)
                return false;

            var leaving = PopTop();
            var target = Top;
            await HookAsync(() => leaving.View.OnNavigatedFromAsync(CancellationToken.None), leaving.Id, "navigated-from");
            var style = Transitions.Resolve(leaving.Id, target.Id, TransitionStyle.Default, NavigationDirection.Back);
            await TransitionAsync(leaving.View, target.View, style, NavigationDirection.Back);
            await CloseAsync(leaving);
            await HookAsync(() => target.View.OnNavigatedToAsync(parameters ?? target.Parameters, CancellationToken.None), target.Id, "navigated-to");
            Changed(NavigationAction.Back, leaving.Id, target.Id);
            return true;
        }

        private async UniTask RootAsync(NavigationParameters parameters)
        {
            await DismissAllModalsAsync();
            if (_stack.Count < 2)
                return;

            var leaving = Top;
            var root = _stack[0];
            await HookAsync(() => leaving.View.OnNavigatedFromAsync(CancellationToken.None), leaving.Id, "navigated-from");
            var style = Transitions.Resolve(leaving.Id, root.Id, TransitionStyle.Default, NavigationDirection.Back);
            await TransitionAsync(leaving.View, root.View, style, NavigationDirection.Back);
            while (_stack.Count > 1)
                await CloseAsync(PopTop());
            await HookAsync(() => root.View.OnNavigatedToAsync(parameters ?? root.Parameters, CancellationToken.None), root.Id, "navigated-to");
            Changed(NavigationAction.Root, leaving.Id, root.Id);
        }

        private async UniTask ClearCoreAsync()
        {
            await DismissAllModalsAsync();
            if (_stack.Count == 0)
                return;

            var top = Top;
            await HookAsync(() => top.View.OnNavigatedFromAsync(CancellationToken.None), top.Id, "navigated-from");
            Safe(top.View.Hide, top.Id, "hide");
            while (_stack.Count > 0)
                await CloseAsync(PopTop());
            Changed(NavigationAction.Clear, top.Id, null);
        }

        private async UniTask<ModalEntry<TResult>> OpenModalAsync<TResult>(string viewId, NavigationParameters parameters,
            TransitionStyle transition, CancellationToken cancellationToken)
        {
            parameters = parameters ?? new NavigationParameters();
            var view = await _factory.CreateAsync(viewId, cancellationToken)
                       ?? throw new InvalidOperationException($"View factory returned no view for '{viewId}'");
            if (!(view is IModalView<TResult> modal))
            {
                Release(viewId, view);
                throw new InvalidOperationException($"View '{viewId}' does not implement IModalView<{typeof(TResult).Name}>");
            }
            await OpenAsync(viewId, view, parameters, cancellationToken);

            var entry = new ModalEntry<TResult>(viewId, view);
            _modals.Add(entry);
            var style = transition != TransitionStyle.Default ? transition : Transitions.DefaultModal;
            await TransitionAsync(null, view, style, NavigationDirection.Forward);
            await HookAsync(() => view.OnNavigatedToAsync(parameters, CancellationToken.None), viewId, "navigated-to");
            modal.BindResult(result => CompleteModalAsync(entry, result).Forget());
            Changed(NavigationAction.ModalOpened, CurrentViewId, viewId);
            return entry;
        }

        private async UniTask CompleteModalAsync<TResult>(ModalEntry<TResult> entry, TResult result)
        {
            if (!entry.BeginClose())
                return;
            await CloseModalAsync(entry);
            entry.Completion.TrySetResult(result);
        }

        private async UniTask DismissAsync(ModalEntry entry)
        {
            if (!entry.BeginClose())
                return;
            await CloseModalAsync(entry);
            entry.Cancel();
        }

        private async UniTask DismissAllModalsAsync()
        {
            for (int i = _modals.Count - 1; i >= 0; i--)
            {
                if (i < _modals.Count)
                    await DismissAsync(_modals[i]);
            }
        }

        private async UniTask CloseModalAsync(ModalEntry entry)
        {
            _modals.Remove(entry);
            await HookAsync(() => entry.View.OnNavigatedFromAsync(CancellationToken.None), entry.Id, "navigated-from");
            var style = Transitions.DefaultModal;
            await TransitionAsync(entry.View, null, style, NavigationDirection.Back);
            await HookAsync(() => entry.View.OnClosedAsync(CancellationToken.None), entry.Id, "closed");
            Release(entry.Id, entry.View);
            Changed(NavigationAction.ModalClosed, entry.Id, CurrentViewId);
        }

        private async UniTask<INavigableView> OpenViewAsync(string viewId, NavigationParameters parameters, CancellationToken cancellationToken)
        {
            var view = await _factory.CreateAsync(viewId, cancellationToken)
                       ?? throw new InvalidOperationException($"View factory returned no view for '{viewId}'");
            await OpenAsync(viewId, view, parameters, cancellationToken);
            return view;
        }

        private async UniTask OpenAsync(string viewId, INavigableView view, NavigationParameters parameters, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await view.OnOpenedAsync(parameters, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    _logger.LogError($"View '{viewId}' failed to open", Category, ex);
                Release(viewId, view);
                throw;
            }
        }

        private async UniTask CloseAsync(ScreenEntry entry)
        {
            await HookAsync(() => entry.View.OnClosedAsync(CancellationToken.None), entry.Id, "closed");
            Release(entry.Id, entry.View);
        }

        private async UniTask TransitionAsync(INavigableView from, INavigableView to, TransitionStyle style, NavigationDirection direction)
        {
            var request = new TransitionRequest(from, to, style, direction);
            try
            {
                await _transition.PlayAsync(request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Transition {style} failed; switching instantly", Category, ex);
                try
                {
                    InstantTransition.Apply(request);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError("Instant view switch failed", Category, fallbackEx);
                }
            }
        }

        private async UniTask HookAsync(Func<UniTask> hook, string viewId, string stage)
        {
            try
            {
                await hook();
            }
            catch (Exception ex)
            {
                _logger.LogError($"View '{viewId}' {stage} hook failed", Category, ex);
            }
        }

        private void Release(string viewId, INavigableView view) => Safe(() => _factory.Release(viewId, view), viewId, "release");

        private void Safe(Action action, string viewId, string stage)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError($"View '{viewId}' {stage} failed", Category, ex);
            }
        }

        private void Changed(NavigationAction action, string from, string to)
        {
            _logger.LogDebug($"{action}: {from ?? "<none>"} -> {to ?? "<none>"}", Category);
            _events?.Publish(new NavigationChangedEvent(action, from, to, _stack.Count));
        }

        private ScreenEntry Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        private ScreenEntry PopTop()
        {
            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            return top;
        }

        private static void RequireId(string viewId)
        {
            if (string.IsNullOrEmpty(viewId))
                throw new ArgumentException("View id is required", nameof(viewId));
        }

        private sealed class ScreenEntry
        {
            public ScreenEntry(string id, INavigableView view, NavigationParameters parameters)
            {
                Id = id;
                View = view;
                Parameters = parameters;
            }

            public string Id { get; }
            public INavigableView View { get; }
            public NavigationParameters Parameters { get; }
        }

        private abstract class ModalEntry
        {
            private bool _closing;

            protected ModalEntry(string id, INavigableView view)
            {
                Id = id;
                View = view;
            }

            public string Id { get; }
            public INavigableView View { get; }

            public bool BeginClose()
            {
                if (_closing)
                    return false;
                _closing = true;
                return true;
            }

            public abstract void Cancel();
        }

        private sealed class ModalEntry<TResult> : ModalEntry
        {
            public ModalEntry(string id, INavigableView view) : base(id, view)
            {
            }

            public UniTaskCompletionSource<TResult> Completion { get; } = new UniTaskCompletionSource<TResult>();

            public override void Cancel() => Completion.TrySetCanceled();
        }
    }
}
