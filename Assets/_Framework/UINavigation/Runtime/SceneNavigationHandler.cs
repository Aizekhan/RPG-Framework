using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Scenes;

namespace RPGFramework.UINavigation
{
    /// <summary>
    /// Connects navigation to <see cref="ISceneDispatcher"/>: when a single-mode scene load starts, every view is
    /// closed; when it succeeds, the scene's root view (<see cref="INavigationService.MapSceneRoot"/>) is opened.
    /// Additive loads and unloads leave navigation alone.
    /// </summary>
    public sealed class SceneNavigationHandler : ISceneTransitionHandler
    {
        private readonly INavigationService _navigation;

        public SceneNavigationHandler(INavigationService navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public UniTask OnTransitionStartAsync(SceneTransition transition, CancellationToken cancellationToken) =>
            transition.Kind == SceneOperationKind.Load ? _navigation.ClearAsync(cancellationToken) : UniTask.CompletedTask;

        public UniTask OnTransitionEndAsync(SceneTransition transition, bool succeeded, CancellationToken cancellationToken)
        {
            if (!succeeded || transition.Kind != SceneOperationKind.Load)
                return UniTask.CompletedTask;
            return _navigation.ShowSceneRootAsync(transition.SceneName, cancellationToken: cancellationToken).AsUniTask();
        }
    }
}
