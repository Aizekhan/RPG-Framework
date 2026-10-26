using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.UINavigation.Unity
{
    /// <summary>
    /// Base for modals: call <see cref="Complete"/> (e.g. from a button) to close the modal with a result.
    /// </summary>
    public abstract class ModalView<TResult> : NavigableView, IModalView<TResult>
    {
        private Action<TResult> _complete;

        /// <summary>
        /// True between opening and closing; <see cref="Complete"/> is ignored otherwise.
        /// </summary>
        public bool CanComplete => _complete != null;

        public void BindResult(Action<TResult> complete) => _complete = complete;

        protected void Complete(TResult result)
        {
            var complete = _complete;
            _complete = null;
            complete?.Invoke(result);
        }

        public override UniTask OnClosedAsync(CancellationToken cancellationToken)
        {
            // A cached instance must not complete a previous request
            _complete = null;
            return base.OnClosedAsync(cancellationToken);
        }
    }
}
