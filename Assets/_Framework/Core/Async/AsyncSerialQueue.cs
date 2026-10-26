using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.Core.Async
{
    /// <summary>
    /// Runs async operations one at a time, in request order: an operation requested while another runs waits
    /// for it instead of racing it. A waiter cancelled before its turn is skipped. Single-threaded (main thread);
    /// when nothing is queued an operation starts synchronously.
    /// </summary>
    public sealed class AsyncSerialQueue
    {
        private readonly Queue<UniTaskCompletionSource> _waiting = new Queue<UniTaskCompletionSource>();
        private bool _busy;

        /// <summary>
        /// True while an operation holds the queue.
        /// </summary>
        public bool IsBusy => _busy;

        /// <summary>
        /// Operations waiting for their turn (may include cancelled waiters not yet skipped).
        /// </summary>
        public int PendingCount => _waiting.Count;

        public async UniTask RunAsync(Func<UniTask> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await EnterAsync(cancellationToken);
            try
            {
                await operation();
            }
            finally
            {
                Exit();
            }
        }

        public async UniTask<T> RunAsync<T>(Func<UniTask<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await EnterAsync(cancellationToken);
            try
            {
                return await operation();
            }
            finally
            {
                Exit();
            }
        }

        private async UniTask EnterAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_busy)
            {
                _busy = true;
                return;
            }

            var turn = new UniTaskCompletionSource();
            _waiting.Enqueue(turn);
            try
            {
                await turn.Task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Our turn may already have been granted: pass it on so the queue does not stall
                if (!turn.TrySetCanceled())
                    Exit();
                throw;
            }
        }

        private void Exit()
        {
            while (_waiting.Count > 0)
            {
                // Skip waiters that cancelled before their turn came
                if (_waiting.Dequeue().TrySetResult())
                    return;
            }
            _busy = false;
        }
    }
}
