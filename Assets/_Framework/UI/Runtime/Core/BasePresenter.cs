using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.UINavigation;

namespace RPGFramework.UI
{
    /// <summary>
    /// Base presenter implementation for MVP pattern.
    /// Provides default lifecycle management.
    /// </summary>
    public abstract class BasePresenter<TView> : IPresenter<TView> where TView : INavigableView
    {
        protected BasePresenter(TView view)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
        }

        public TView View { get; }

        public bool IsDisposed { get; private set; }

        public virtual UniTask InitializeAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        public void Dispose()
        {
            if (IsDisposed)
                return;

            OnDispose();
            IsDisposed = true;
        }

        /// <summary>
        /// Override to implement custom cleanup logic.
        /// </summary>
        protected virtual void OnDispose() { }
    }
}
