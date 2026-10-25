using System;
using System.Threading.Tasks;

namespace RPGFramework.UI
{
    /// <summary>
    /// Base presenter implementation for MVP pattern.
    /// Provides default lifecycle management.
    /// </summary>
    public abstract class BasePresenter<TView> : IPresenter<TView> where TView : IView
    {
        protected bool _disposed;

        public TView View { get; private set; }

        protected BasePresenter(TView view)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
        }

        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;

            OnDispose();
            _disposed = true;
        }

        /// <summary>
        /// Override to implement custom cleanup logic.
        /// </summary>
        protected virtual void OnDispose() { }
    }
}
