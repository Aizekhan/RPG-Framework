using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Core.Async;
using RPGFramework.Core.Initialization;
using RPGFramework.Logging;

namespace RPGFramework.Core.Tests
{
    public sealed class InitializationTransactionTests
    {
        [Test]
        public void Rollback_RunsActionsInReverseRegistrationOrder()
        {
            var order = new List<string>();
            var tx = new InitializationTransaction();
            tx.RegisterRollback(() => order.Add("first"));
            tx.RegisterRollback(() => order.Add("second"));
            tx.RegisterRollback(() => order.Add("third"));

            tx.Rollback();

            CollectionAssert.AreEqual(new[] { "third", "second", "first" }, order);
        }

        [Test]
        public void Rollback_ContinuesAfterFailingStep_AndCountsFailures()
        {
            var order = new List<string>();
            var sink = new MemoryLogSink();
            var tx = new InitializationTransaction(new Logger(LogSeverity.Trace, sink));
            tx.RegisterRollback(() => order.Add("first"));
            tx.RegisterRollback(() => throw new InvalidOperationException("broken"), "broken step");
            tx.RegisterRollback(() => order.Add("third"));

            tx.Rollback();

            CollectionAssert.AreEqual(new[] { "third", "first" }, order);
            Assert.That(tx.FailedRollbackCount, Is.EqualTo(1));
            Assert.That(sink.Snapshot(), Has.Some.Matches<LogEntry>(e => e.Severity == LogSeverity.Error && e.Message.Contains("broken step")));
        }

        [Test]
        public void Commit_PreventsRollbackAndDisposeRollback()
        {
            int rolledBack = 0;
            var tx = new InitializationTransaction();
            tx.RegisterRollback(() => rolledBack++);

            tx.Commit();
            tx.Rollback();
            tx.Dispose();

            Assert.That(rolledBack, Is.EqualTo(0));
            Assert.That(tx.IsCommitted, Is.True);
        }

        [Test]
        public void Dispose_WithoutCommit_RollsBack()
        {
            int rolledBack = 0;
            try
            {
                using var tx = new InitializationTransaction();
                tx.RegisterRollback(() => rolledBack++);
                throw new InvalidOperationException("step failed");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.That(rolledBack, Is.EqualTo(1));
        }

        [Test]
        public void RegisterAfterCommit_Throws_AndUseAfterDispose_Throws()
        {
            var tx = new InitializationTransaction();
            tx.Commit();
            Assert.Throws<InvalidOperationException>(() => tx.RegisterRollback(() => { }));

            tx.Dispose();
            Assert.Throws<ObjectDisposedException>(tx.Rollback);
        }

        [Test]
        public void DisposeOnRollback_DisposesResource()
        {
            var resource = new Disposable();
            var tx = new InitializationTransaction();

            Assert.That(tx.DisposeOnRollback(resource), Is.SameAs(resource));
            tx.Rollback();

            Assert.That(resource.Disposed, Is.True);
        }

        private sealed class Disposable : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }
    }

    public sealed class AsyncTaskTrackerTests
    {
        [Test]
        public void Track_CountsOperationUntilItCompletes()
        {
            var tracker = new AsyncTaskTracker();
            var gate = new UniTaskCompletionSource();

            tracker.Track(_ => gate.Task, "load");

            Assert.That(tracker.ActiveCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "load" }, tracker.GetActiveOperationNames());

            gate.TrySetResult();

            Assert.That(tracker.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Track_LogsAndReportsFaultsInsteadOfLosingThem()
        {
            var sink = new MemoryLogSink();
            var tracker = new AsyncTaskTracker(new Logger(LogSeverity.Trace, sink));
            string faultedName = null;
            tracker.OperationFaulted += (name, _) => faultedName = name;
            var gate = new UniTaskCompletionSource();

            tracker.Track(async _ =>
            {
                await gate.Task;
                throw new InvalidOperationException("boom");
            }, "save");
            gate.TrySetResult();

            Assert.That(faultedName, Is.EqualTo("save"));
            Assert.That(tracker.ActiveCount, Is.EqualTo(0));
            Assert.That(sink.Snapshot(), Has.Some.Matches<LogEntry>(e => e.Severity == LogSeverity.Error && e.Exception is InvalidOperationException));
        }

        [Test]
        public void CancelByName_CancelsOnlyMatchingOperations()
        {
            var tracker = new AsyncTaskTracker();
            bool aCancelled = false, bCancelled = false;

            tracker.Track(token => WaitForCancel(token, () => aCancelled = true), "a");
            tracker.Track(token => WaitForCancel(token, () => bCancelled = true), "b");

            Assert.That(tracker.Cancel("a"), Is.EqualTo(1));
            Assert.That(aCancelled, Is.True);
            Assert.That(bCancelled, Is.False);
            Assert.That(tracker.ActiveCount, Is.EqualTo(1));

            tracker.CancelAll();
            Assert.That(bCancelled, Is.True);
            Assert.That(tracker.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void WaitForAllAsync_CompletesWhenLastOperationFinishes()
        {
            var tracker = new AsyncTaskTracker();
            var first = new UniTaskCompletionSource();
            var second = new UniTaskCompletionSource();
            tracker.Track(_ => first.Task);
            tracker.Track(_ => second.Task);

            var wait = tracker.WaitForAllAsync(TimeSpan.FromSeconds(10));
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Pending));

            first.TrySetResult();
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Pending));

            second.TrySetResult();
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(wait.GetAwaiter().GetResult(), Is.True);
        }

        [Test]
        public void WaitForAllAsync_WithNothingRunning_CompletesImmediately()
        {
            var wait = new AsyncTaskTracker().WaitForAllAsync(TimeSpan.FromSeconds(1));

            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Dispose_CancelsRunningOperations_AndRejectsNewOnes()
        {
            var tracker = new AsyncTaskTracker();
            bool cancelled = false;
            tracker.Track(token => WaitForCancel(token, () => cancelled = true));

            tracker.Dispose();

            Assert.That(cancelled, Is.True);
            Assert.Throws<ObjectDisposedException>(() => tracker.Track(_ => UniTask.CompletedTask));
        }

        private static async UniTask WaitForCancel(System.Threading.CancellationToken token, Action onCancel)
        {
            var never = new UniTaskCompletionSource();
            using (token.Register(() =>
            {
                onCancel();
                never.TrySetCanceled(token);
            }))
            {
                await never.Task;
            }
        }
    }

    public sealed class EnsureTests
    {
        [Test]
        public void Guards_ReturnValidValues()
        {
            var list = new List<int> { 1 };

            Assert.That(Ensure.NotNull("x", "p"), Is.EqualTo("x"));
            Assert.That(Ensure.NotNullOrWhiteSpace("id", "p"), Is.EqualTo("id"));
            Assert.That(Ensure.NotEmpty(list, "p"), Is.SameAs(list));
            Assert.That(Ensure.InRange(5, 0, 10, "p"), Is.EqualTo(5));
            Assert.That(Ensure.Positive(1f, "p"), Is.EqualTo(1f));
            Assert.That(Ensure.NotNegative(0, "p"), Is.EqualTo(0));
            Assert.That(Ensure.Defined(LogSeverity.Error, "p"), Is.EqualTo(LogSeverity.Error));
        }

        [Test]
        public void Guards_ThrowStandardExceptionsWithParamName()
        {
            Assert.That(Assert.Throws<ArgumentNullException>(() => Ensure.NotNull<string>(null, "name")).ParamName, Is.EqualTo("name"));
            Assert.Throws<ArgumentException>(() => Ensure.NotNullOrWhiteSpace(" ", "id"));
            Assert.Throws<ArgumentException>(() => Ensure.NotEmpty(new List<int>(), "list"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(11, 0, 10, "level"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(0, "count"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotNegative(-0.5f, "hp"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Defined((LogSeverity)99, "severity"));
            Assert.Throws<ArgumentException>(() => Ensure.IsTrue(false, "must hold"));
        }
    }
}
