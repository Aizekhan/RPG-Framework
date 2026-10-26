using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Logging;

namespace RPGFramework.Core.Async
{
    /// <summary>
    /// Owns fire-and-forget async operations: every operation gets a cancellation token linked to the tracker,
    /// exceptions are logged instead of lost, and shutdown can cancel and await everything still running.
    /// </summary>
    public interface IAsyncTaskTracker : IDisposable
    {
        int ActiveCount { get; }

        /// <summary>
        /// Starts <paramref name="operation"/> and tracks it until it finishes.
        /// </summary>
        void Track(Func<CancellationToken, UniTask> operation, string operationName = null);

        /// <summary>
        /// Cancels every operation with this name. Returns how many were cancelled.
        /// </summary>
        int Cancel(string operationName);

        void CancelAll();

        /// <summary>
        /// Completes when no operation is running. Returns false if <paramref name="timeout"/> elapsed first.
        /// </summary>
        UniTask<bool> WaitForAllAsync(TimeSpan timeout);

        IReadOnlyList<string> GetActiveOperationNames();
    }

    /// <summary>
    /// Default <see cref="IAsyncTaskTracker"/>. Thread-safe; does not depend on the Unity player loop.
    /// </summary>
    public sealed class AsyncTaskTracker : IAsyncTaskTracker
    {
        private const string Category = "Async";

        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private readonly List<TrackedOperation> _active = new List<TrackedOperation>();
        private readonly CancellationTokenSource _globalCts = new CancellationTokenSource();
        private UniTaskCompletionSource _idle;
        private bool _disposed;

        public AsyncTaskTracker(ILogger logger = null)
        {
            _logger = logger;
        }

        public int ActiveCount
        {
            get
            {
                lock (_gate)
                    return _active.Count;
            }
        }

        /// <summary>
        /// Raised (on the thread that observed it) when a tracked operation throws.
        /// </summary>
        public event Action<string, Exception> OperationFaulted;

        public void Track(Func<CancellationToken, UniTask> operation, string operationName = null)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            TrackedOperation tracked;
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(AsyncTaskTracker));

                tracked = new TrackedOperation(operationName ?? "operation", CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token));
                _active.Add(tracked);
            }

            RunAsync(operation, tracked).Forget();
        }

        public int Cancel(string operationName)
        {
            // Cancel outside the lock: cancellation runs continuations synchronously and they remove themselves from _active
            var matching = new List<TrackedOperation>();
            lock (_gate)
            {
                foreach (var tracked in _active)
                {
                    if (tracked.Name == operationName)
                        matching.Add(tracked);
                }
            }

            foreach (var tracked in matching)
                tracked.Cts.Cancel();
            return matching.Count;
        }

        public void CancelAll()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
            }
            _globalCts.Cancel();
        }

        public async UniTask<bool> WaitForAllAsync(TimeSpan timeout)
        {
            UniTask idle;
            lock (_gate)
            {
                if (_active.Count == 0)
                    return true;
                _idle ??= new UniTaskCompletionSource();
                idle = _idle.Task;
            }

            using (var timeoutCts = new CancellationTokenSource(timeout))
            {
                try
                {
                    await idle.AttachExternalCancellation(timeoutCts.Token);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning($"Timed out waiting for {ActiveCount} async operation(s)", Category);
                    return false;
                }
            }
        }

        public IReadOnlyList<string> GetActiveOperationNames()
        {
            lock (_gate)
            {
                var names = new string[_active.Count];
                for (int i = 0; i < _active.Count; i++)
                    names[i] = _active[i].Name;
                return names;
            }
        }

        /// <summary>
        /// Cancels all operations. Operations finish on their own; their tokens are already cancelled.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }
            _globalCts.Cancel();
        }

        private async UniTaskVoid RunAsync(Func<CancellationToken, UniTask> operation, TrackedOperation tracked)
        {
            try
            {
                await operation(tracked.Cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is an expected outcome
            }
            catch (Exception ex)
            {
                _logger.LogError($"Async operation '{tracked.Name}' failed", Category, ex);
                OperationFaulted?.Invoke(tracked.Name, ex);
            }
            finally
            {
                UniTaskCompletionSource idle = null;
                lock (_gate)
                {
                    _active.Remove(tracked);
                    if (_active.Count == 0 && _idle != null)
                    {
                        idle = _idle;
                        _idle = null;
                    }
                }
                tracked.Cts.Dispose();
                idle?.TrySetResult();
            }
        }

        private sealed class TrackedOperation
        {
            public TrackedOperation(string name, CancellationTokenSource cts)
            {
                Name = name;
                Cts = cts;
            }

            public string Name { get; }
            public CancellationTokenSource Cts { get; }
        }
    }
}
