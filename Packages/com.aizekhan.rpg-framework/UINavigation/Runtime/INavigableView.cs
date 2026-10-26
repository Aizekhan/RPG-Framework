using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.UINavigation
{
    /// <summary>
    /// A screen or modal managed by <see cref="INavigationService"/>. Lifecycle for one stay in the stack:
    /// <c>OnOpenedAsync</c> (before it becomes visible) → <c>OnNavigatedToAsync</c> (it is the top view) ⇄
    /// <c>OnNavigatedFromAsync</c> (covered or leaving) → <c>OnClosedAsync</c> (removed; then released to the factory).
    /// A cached instance goes through the whole cycle again when it is reused.
    /// </summary>
    public interface INavigableView
    {
        bool IsVisible { get; }

        void Show();
        void Hide();

        UniTask OnOpenedAsync(NavigationParameters parameters, CancellationToken cancellationToken);
        UniTask OnNavigatedToAsync(NavigationParameters parameters, CancellationToken cancellationToken);
        UniTask OnNavigatedFromAsync(CancellationToken cancellationToken);
        UniTask OnClosedAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// A view shown with <see cref="INavigationService.ShowModalAsync{TResult}"/>. The view reports its result through
    /// the callback bound once it is open (e.g. from a button click); the service then closes it.
    /// </summary>
    public interface IModalView<TResult> : INavigableView
    {
        void BindResult(Action<TResult> complete);
    }

    /// <summary>
    /// Creates views by id (instantiating a prefab, taking one from a cache, ...) and takes them back when closed.
    /// </summary>
    public interface IViewFactory
    {
        UniTask<INavigableView> CreateAsync(string viewId, CancellationToken cancellationToken);
        void Release(string viewId, INavigableView view);
    }
}
