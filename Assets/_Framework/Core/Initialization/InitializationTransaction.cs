using System;
using System.Collections.Generic;
using RPGFramework.Logging;

namespace RPGFramework.Core.Initialization
{
    /// <summary>
    /// Undo log for multi-step initialization: every step registers how to revert itself, and on failure
    /// (or disposal without <see cref="Commit"/>) the steps are reverted in reverse order.
    /// </summary>
    public interface IInitializationTransaction : IDisposable
    {
        bool IsCommitted { get; }

        void RegisterRollback(Action rollbackAction, string description = null);

        void Commit();

        void Rollback();
    }

    /// <summary>
    /// Default <see cref="IInitializationTransaction"/>.
    /// Usage: <c>using var tx = new InitializationTransaction(logger); ...steps...; tx.Commit();</c>
    /// An exception before Commit leaves the using block and rolls everything back.
    /// </summary>
    public sealed class InitializationTransaction : IInitializationTransaction
    {
        private const string Category = "Initialization";

        private readonly Stack<(Action action, string description)> _rollbackActions = new Stack<(Action, string)>();
        private readonly ILogger _logger;
        private bool _disposed;

        public InitializationTransaction(ILogger logger = null)
        {
            _logger = logger;
        }

        public bool IsCommitted { get; private set; }

        /// <summary>
        /// Number of rollback actions that threw during the last <see cref="Rollback"/>.
        /// </summary>
        public int FailedRollbackCount { get; private set; }

        public void RegisterRollback(Action rollbackAction, string description = null)
        {
            ThrowIfDisposed();
            if (IsCommitted)
                throw new InvalidOperationException("Cannot register rollback after commit");
            if (rollbackAction == null)
                throw new ArgumentNullException(nameof(rollbackAction));

            _rollbackActions.Push((rollbackAction, description));
        }

        public void Commit()
        {
            ThrowIfDisposed();
            IsCommitted = true;
            _rollbackActions.Clear();
        }

        public void Rollback()
        {
            ThrowIfDisposed();
            if (IsCommitted)
            {
                _logger.LogWarning("Rollback requested on a committed transaction; ignored", Category);
                return;
            }

            FailedRollbackCount = 0;
            if (_rollbackActions.Count > 0)
                _logger.LogWarning($"Rolling back {_rollbackActions.Count} initialization step(s)", Category);

            while (_rollbackActions.Count > 0)
            {
                var (action, description) = _rollbackActions.Pop();
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    FailedRollbackCount++;
                    _logger.LogError($"Rollback step failed: {description ?? "<unnamed>"}", Category, ex);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (!IsCommitted && _rollbackActions.Count > 0)
                Rollback();

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InitializationTransaction));
        }
    }

    public static class InitializationTransactionExtensions
    {
        /// <summary>
        /// Disposes <paramref name="disposable"/> if the transaction is rolled back.
        /// </summary>
        public static T DisposeOnRollback<T>(this IInitializationTransaction transaction, T disposable, string description = null)
            where T : IDisposable
        {
            if (disposable != null)
                transaction.RegisterRollback(disposable.Dispose, description ?? typeof(T).Name);
            return disposable;
        }
    }
}
