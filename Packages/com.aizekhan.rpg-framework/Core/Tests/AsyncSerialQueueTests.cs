using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Core.Async;

namespace RPGFramework.Core.Tests
{
    public sealed class AsyncSerialQueueTests
    {
        private AsyncSerialQueue _queue;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _queue = new AsyncSerialQueue();
            _log = new List<string>();
        }

        private Func<UniTask> Op(string name, UniTaskCompletionSource gate = null) => async () =>
        {
            _log.Add($"start {name}");
            if (gate != null)
                await gate.Task;
            _log.Add($"end {name}");
        };

        [Test]
        public void IdleQueue_RunsSynchronously()
        {
            var run = _queue.RunAsync(Op("a"));

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_queue.IsBusy, Is.False);
        }

        [Test]
        public void Operations_RunOneAtATime_InRequestOrder()
        {
            var gate = new UniTaskCompletionSource();
            var first = _queue.RunAsync(Op("a", gate));
            var second = _queue.RunAsync(Op("b"));
            var third = _queue.RunAsync(() => UniTask.FromResult(3));

            CollectionAssert.AreEqual(new[] { "start a" }, _log);
            Assert.That(_queue.PendingCount, Is.EqualTo(2));

            gate.TrySetResult();

            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(third.GetAwaiter().GetResult(), Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "start a", "end a", "start b", "end b" }, _log);
            Assert.That(_queue.IsBusy, Is.False);
        }

        [Test]
        public void CancelledWaiter_IsSkipped()
        {
            var gate = new UniTaskCompletionSource();
            var cts = new CancellationTokenSource();
            _queue.RunAsync(Op("a", gate)).Forget();
            var cancelled = _queue.RunAsync(Op("b"), cts.Token);
            var after = _queue.RunAsync(Op("c"));

            cts.Cancel();
            gate.TrySetResult();

            Assert.That(cancelled.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(after.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            CollectionAssert.AreEqual(new[] { "start a", "end a", "start c", "end c" }, _log);
        }

        [Test]
        public void FailingOperation_ReleasesQueue()
        {
            var failed = _queue.RunAsync(() => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => failed.GetAwaiter().GetResult());
            Assert.That(_queue.IsBusy, Is.False);
            Assert.That(_queue.RunAsync(Op("next")).Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void AlreadyCancelledToken_DoesNotRun()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var run = _queue.RunAsync(Op("a"), cts.Token);

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_log, Is.Empty);
            Assert.That(_queue.IsBusy, Is.False);
        }
    }
}
