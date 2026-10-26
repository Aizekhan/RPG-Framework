using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Logging;

namespace RPGFramework.Events.Tests
{
    internal readonly struct TestEvent : IEvent
    {
        public TestEvent(int value) => Value = value;
        public int Value { get; }
        public override string ToString() => $"TestEvent({Value})";
    }

    internal sealed class OtherEvent : IEvent
    {
    }

    internal readonly struct ItemEvent : IEvent
    {
        public ItemEvent(int id) => Id = id;
        public int Id { get; }
    }

    internal sealed class ItemBatch : IEvent
    {
        public ItemBatch(IReadOnlyList<ItemEvent> items) => Items = items;
        public IReadOnlyList<ItemEvent> Items { get; }
    }

    public sealed class EventBusTests
    {
        private MemoryLogSink _sink;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _sink = new MemoryLogSink();
            _bus = new EventBus(new Logger(LogSeverity.Trace, _sink));
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        [Test]
        public void Publish_DeliversToSubscriber()
        {
            int received = 0;
            _bus.Subscribe<TestEvent>(e => received = e.Value);

            _bus.Publish(new TestEvent(7));

            Assert.That(received, Is.EqualTo(7));
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(1)));
            Assert.That(_bus.GetSubscriberCount<TestEvent>(), Is.Zero);
        }

        [Test]
        public void Handlers_RunByPriorityDescending_StableWithinPriority()
        {
            var order = new List<string>();
            _bus.Subscribe<TestEvent>(_ => order.Add("normal1"));
            _bus.Subscribe<TestEvent>(_ => order.Add("low"), EventPriority.Low);
            _bus.Subscribe<TestEvent>(_ => order.Add("critical"), EventPriority.Critical);
            _bus.Subscribe<TestEvent>(_ => order.Add("normal2"));
            _bus.Subscribe<TestEvent>(_ => order.Add("high"), EventPriority.High);

            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "critical", "high", "normal1", "normal2", "low" }, order);
        }

        [Test]
        public void DuplicateSubscription_IsIgnored_AndItsTokenDoesNotRemoveOriginal()
        {
            int calls = 0;
            Action<TestEvent> handler = _ => calls++;

            _bus.Subscribe(handler);
            var duplicate = _bus.Subscribe(handler);
            duplicate.Dispose();
            _bus.Publish(new TestEvent(0));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(_bus.GetSubscriberCount<TestEvent>(), Is.EqualTo(1));
            Assert.That(_sink.Snapshot().Any(e => e.Severity == LogSeverity.Warning && e.Message.Contains("Duplicate")), Is.True);
        }

        [Test]
        public void DisposingToken_Unsubscribes_AndIsIdempotent()
        {
            int calls = 0;
            var token = _bus.Subscribe<TestEvent>(_ => calls++);

            token.Dispose();
            token.Dispose();
            _bus.Publish(new TestEvent(0));

            Assert.That(calls, Is.Zero);
            Assert.That(_bus.GetSubscriberCount<TestEvent>(), Is.Zero);
        }

        [Test]
        public void Unsubscribe_ReturnsWhetherHandlerWasRegistered()
        {
            Action<TestEvent> handler = _ => { };
            _bus.Subscribe(handler);

            Assert.That(_bus.Unsubscribe(handler), Is.True);
            Assert.That(_bus.Unsubscribe(handler), Is.False);
        }

        [Test]
        public void ThrowingHandler_IsIsolated_OthersStillRun()
        {
            bool secondRan = false;
            _bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"), EventPriority.High);
            _bus.Subscribe<TestEvent>(_ => secondRan = true);

            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(0)));

            Assert.That(secondRan, Is.True);
            Assert.That(_bus.HandlerErrorCount, Is.EqualTo(1));
            Assert.That(_sink.Snapshot().Any(e => e.Severity == LogSeverity.Error && e.Exception is InvalidOperationException), Is.True);
        }

        [Test]
        public void UnsubscribeDuringDispatch_CurrentDispatchCompletes_NextDoesNotCallRemoved()
        {
            var calls = new List<string>();
            IDisposable second = null;
            _bus.Subscribe<TestEvent>(_ =>
            {
                calls.Add("first");
                second.Dispose();
            }, EventPriority.High);
            second = _bus.Subscribe<TestEvent>(_ => calls.Add("second"));

            _bus.Publish(new TestEvent(0));
            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "first", "second", "first" }, calls);
        }

        [Test]
        public void SubscribeDuringDispatch_NewHandlerReceivesOnlyNextEvent()
        {
            var received = new List<int>();
            bool subscribed = false;
            _bus.Subscribe<TestEvent>(e =>
            {
                if (subscribed)
                    return;
                subscribed = true;
                _bus.Subscribe<TestEvent>(x => received.Add(x.Value));
            });

            _bus.Publish(new TestEvent(1));
            _bus.Publish(new TestEvent(2));

            CollectionAssert.AreEqual(new[] { 2 }, received);
        }

        [Test]
        public void RecursivePublishing_IsCutOffAtMaxDepth()
        {
            _bus.MaxDispatchDepth = 5;
            int calls = 0;
            _bus.Subscribe<TestEvent>(e =>
            {
                calls++;
                _bus.Publish(new TestEvent(e.Value + 1));
            });

            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(0)));

            Assert.That(calls, Is.EqualTo(5));
            Assert.That(_bus.HandlerErrorCount, Is.EqualTo(1));
        }

        [Test]
        public void Sticky_IsReplayedToLateSubscriber_UnlessOptedOut()
        {
            _bus.PublishSticky(new TestEvent(42));
            int late = 0;
            int optedOut = 0;

            _bus.Subscribe<TestEvent>(e => late = e.Value);
            _bus.Subscribe<TestEvent>(e => optedOut = e.Value, receiveSticky: false);

            Assert.That(late, Is.EqualTo(42));
            Assert.That(optedOut, Is.Zero);
            Assert.That(_bus.TryGetSticky<TestEvent>(out var sticky), Is.True);
            Assert.That(sticky.Value, Is.EqualTo(42));
        }

        [Test]
        public void ClearSticky_StopsReplay()
        {
            _bus.PublishSticky(new TestEvent(1));

            Assert.That(_bus.ClearSticky<TestEvent>(), Is.True);
            Assert.That(_bus.ClearSticky<TestEvent>(), Is.False);

            int received = 0;
            _bus.Subscribe<TestEvent>(e => received = e.Value);
            Assert.That(received, Is.Zero);
            Assert.That(_bus.TryGetSticky<TestEvent>(out _), Is.False);
        }

        [Test]
        public void Enqueue_DefersUntilFlush()
        {
            var received = new List<int>();
            _bus.Subscribe<TestEvent>(e => received.Add(e.Value));

            _bus.Enqueue(new TestEvent(1));
            _bus.Enqueue(new TestEvent(2));
            Assert.That(received, Is.Empty);
            Assert.That(_bus.PendingCount, Is.EqualTo(2));

            int processed = _bus.Flush();

            Assert.That(processed, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1, 2 }, received);
            Assert.That(_bus.PendingCount, Is.Zero);
        }

        [Test]
        public void Flush_DoesNotProcessEventsEnqueuedByHandlers()
        {
            var received = new List<int>();
            _bus.Subscribe<TestEvent>(e =>
            {
                received.Add(e.Value);
                if (e.Value < 3)
                    _bus.Enqueue(new TestEvent(e.Value + 1));
            });
            _bus.Enqueue(new TestEvent(1));

            Assert.That(_bus.Flush(), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 1 }, received);
            Assert.That(_bus.PendingCount, Is.EqualTo(1));

            _bus.Flush();
            CollectionAssert.AreEqual(new[] { 1, 2 }, received);
        }

        [Test]
        public void Flush_RespectsMaxEvents()
        {
            for (int i = 0; i < 5; i++)
                _bus.Enqueue(new TestEvent(i));

            Assert.That(_bus.Flush(2), Is.EqualTo(2));
            Assert.That(_bus.PendingCount, Is.EqualTo(3));
        }

        [Test]
        public void Observer_SeesEveryPublishedEvent_UntilDisposed()
        {
            var seen = new List<Type>();
            var observer = _bus.AddObserver((type, _) => seen.Add(type));

            _bus.Publish(new TestEvent(1));
            _bus.Publish(new OtherEvent());
            observer.Dispose();
            _bus.Publish(new TestEvent(2));

            CollectionAssert.AreEqual(new[] { typeof(TestEvent), typeof(OtherEvent) }, seen);
        }

        [Test]
        public void PublishAsync_RunsSyncHandlersThenAsyncGroupsByPriority()
        {
            var order = new List<string>();
            var highGate = new UniTaskCompletionSource();
            _bus.Subscribe<TestEvent>(_ => order.Add("sync"));
            _bus.SubscribeAsync<TestEvent>(async (_, __) =>
            {
                order.Add("high-start");
                await highGate.Task;
                order.Add("high-end");
            }, EventPriority.High);
            _bus.SubscribeAsync<TestEvent>((_, __) =>
            {
                order.Add("normal");
                return UniTask.CompletedTask;
            });

            var publish = _bus.PublishAsync(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "sync", "high-start" }, order);
            Assert.That(publish.Status, Is.EqualTo(UniTaskStatus.Pending));

            highGate.TrySetResult();

            CollectionAssert.AreEqual(new[] { "sync", "high-start", "high-end", "normal" }, order);
            Assert.That(publish.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void PublishAsync_HandlersOfSamePriority_RunConcurrently()
        {
            var gateA = new UniTaskCompletionSource();
            var started = new List<string>();
            _bus.SubscribeAsync<TestEvent>(async (_, __) =>
            {
                started.Add("a");
                await gateA.Task;
            });
            _bus.SubscribeAsync<TestEvent>((_, __) =>
            {
                started.Add("b");
                return UniTask.CompletedTask;
            });

            var publish = _bus.PublishAsync(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "a", "b" }, started);
            gateA.TrySetResult();
            Assert.That(publish.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void PublishAsync_FailingAsyncHandler_IsIsolated()
        {
            bool otherRan = false;
            _bus.SubscribeAsync<TestEvent>((_, __) => throw new InvalidOperationException("async boom"));
            _bus.SubscribeAsync<TestEvent>((_, __) =>
            {
                otherRan = true;
                return UniTask.CompletedTask;
            });

            var publish = _bus.PublishAsync(new TestEvent(0));

            Assert.That(publish.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(otherRan, Is.True);
            Assert.That(_bus.HandlerErrorCount, Is.EqualTo(1));
        }

        [Test]
        public void PublishAsync_Cancelled_StopsBeforeNextGroup()
        {
            var cts = new CancellationTokenSource();
            var gate = new UniTaskCompletionSource();
            bool lowRan = false;
            _bus.SubscribeAsync<TestEvent>(async (_, __) => await gate.Task, EventPriority.High);
            _bus.SubscribeAsync<TestEvent>((_, __) =>
            {
                lowRan = true;
                return UniTask.CompletedTask;
            }, EventPriority.Low);

            var publish = _bus.PublishAsync(new TestEvent(0), cts.Token);
            cts.Cancel();
            gate.TrySetResult();

            Assert.That(publish.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(lowRan, Is.False);
        }

        [Test]
        public void UnsubscribeAsync_RemovesAsyncHandler()
        {
            Func<TestEvent, CancellationToken, UniTask> handler = (_, __) => UniTask.CompletedTask;
            _bus.SubscribeAsync(handler);
            Assert.That(_bus.GetSubscriberCount<TestEvent>(), Is.EqualTo(1));

            Assert.That(_bus.UnsubscribeAsync(handler), Is.True);
            Assert.That(_bus.GetSubscriberCount<TestEvent>(), Is.Zero);
        }

        [Test]
        public void Clear_RemovesSubscribersStickyAndQueue()
        {
            int calls = 0;
            _bus.Subscribe<TestEvent>(_ => calls++);
            _bus.PublishSticky(new TestEvent(1));
            _bus.Enqueue(new TestEvent(2));
            calls = 0;

            _bus.Clear();
            _bus.Publish(new TestEvent(3));

            Assert.That(calls, Is.Zero);
            Assert.That(_bus.PendingCount, Is.Zero);
            Assert.That(_bus.TryGetSticky<TestEvent>(out _), Is.False);
        }

        [Test]
        public void AfterDispose_PublishIsIgnored_SubscribeThrows()
        {
            _bus.Dispose();

            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(0)));
            Assert.Throws<ObjectDisposedException>(() => _bus.Subscribe<TestEvent>(_ => { }));
        }

        [Test]
        public void ManySubscribeUnsubscribeCycles_LeaveNoHandlers()
        {
            for (int i = 0; i < 1000; i++)
            {
                int captured = i;
                var token = _bus.Subscribe<TestEvent>(_ => captured++);
                token.Dispose();
            }

            Assert.That(_bus.GetSubscriberCount<TestEvent>(), Is.Zero);
        }

        private sealed class Listener : EventSubscriberBase
        {
            public Listener(IEventBus bus) : base(bus)
            {
                Subscribe<TestEvent>(OnTest);
            }

            public int Received { get; private set; }

            private void OnTest(TestEvent e) => Received += e.Value;
        }

        [Test]
        public void EventSubscriberBase_DisposeUnsubscribesEverything()
        {
            var listener = new Listener(_bus);
            _bus.Publish(new TestEvent(3));
            listener.Dispose();
            _bus.Publish(new TestEvent(4));

            Assert.That(listener.Received, Is.EqualTo(3));
            Assert.That(listener.IsDisposed, Is.True);
            Assert.That(_bus.GetSubscriberCount<TestEvent>(), Is.Zero);
        }

        [Test]
        public void SubscriptionBag_DisposesAllAndEmpties()
        {
            var bag = new SubscriptionBag();
            _bus.Subscribe<TestEvent>(_ => { }).AddTo(bag);
            _bus.Subscribe<OtherEvent>(_ => { }).AddTo(bag);
            Assert.That(bag.Count, Is.EqualTo(2));

            bag.Dispose();

            Assert.That(bag.Count, Is.Zero);
            Assert.That(_bus.GetSubscriberCount<TestEvent>() + _bus.GetSubscriberCount<OtherEvent>(), Is.Zero);
        }
    }

    public sealed class EventThrottlerTests
    {
        private EventBus _bus;
        private List<int> _received;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _received = new List<int>();
            _bus.Subscribe<TestEvent>(e => _received.Add(e.Value));
        }

        [Test]
        public void UnconfiguredType_PassesThrough()
        {
            var throttler = new EventThrottler(_bus);

            Assert.That(throttler.Publish(new TestEvent(1)), Is.True);
            Assert.That(throttler.Publish(new TestEvent(2)), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _received);
        }

        [Test]
        public void LeadingAndTrailing_PublishesFirstImmediately_AndLatestAtWindowEnd()
        {
            var throttler = new EventThrottler(_bus);
            throttler.Configure<TestEvent>(1f);

            throttler.Publish(new TestEvent(1));
            throttler.Publish(new TestEvent(2));
            throttler.Publish(new TestEvent(3));
            CollectionAssert.AreEqual(new[] { 1 }, _received);

            throttler.Update(0.5f);
            CollectionAssert.AreEqual(new[] { 1 }, _received);

            throttler.Update(0.5f);
            CollectionAssert.AreEqual(new[] { 1, 3 }, _received);
        }

        [Test]
        public void Leading_DropsEventsInsideWindow()
        {
            var throttler = new EventThrottler(_bus);
            throttler.Configure<TestEvent>(1f, ThrottleMode.Leading);

            throttler.Publish(new TestEvent(1));
            throttler.Publish(new TestEvent(2));
            throttler.Update(2f);
            throttler.Publish(new TestEvent(3));

            CollectionAssert.AreEqual(new[] { 1, 3 }, _received);
        }

        [Test]
        public void Trailing_PublishesOnlyLatestAfterWindow()
        {
            var throttler = new EventThrottler(_bus);
            throttler.Configure<TestEvent>(1f, ThrottleMode.Trailing);

            Assert.That(throttler.Publish(new TestEvent(1)), Is.False);
            throttler.Publish(new TestEvent(2));
            Assert.That(_received, Is.Empty);

            throttler.Update(1f);

            CollectionAssert.AreEqual(new[] { 2 }, _received);
        }

        [Test]
        public void FlushPending_PublishesTrailingImmediately()
        {
            var throttler = new EventThrottler(_bus);
            throttler.Configure<TestEvent>(10f, ThrottleMode.Trailing);
            throttler.Publish(new TestEvent(5));

            throttler.FlushPending();

            CollectionAssert.AreEqual(new[] { 5 }, _received);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var throttler = new EventThrottler(_bus);
            Assert.Throws<ArgumentOutOfRangeException>(() => throttler.Configure<TestEvent>(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => throttler.Update(-1f));
            Assert.Throws<ArgumentNullException>(() => new EventThrottler(null));
        }
    }

    public sealed class EventBatcherTests
    {
        private EventBus _bus;
        private List<ItemBatch> _batches;
        private EventBatcher _batcher;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _batches = new List<ItemBatch>();
            _bus.Subscribe<ItemBatch>(b => _batches.Add(b));
            _batcher = new EventBatcher(_bus);
            _batcher.Configure<ItemEvent, ItemBatch>(items => new ItemBatch(items), maxBatchSize: 3, maxDelaySeconds: 1f);
        }

        [Test]
        public void ReachingMaxSize_PublishesBatch()
        {
            for (int i = 1; i <= 4; i++)
                _batcher.Add(new ItemEvent(i));

            Assert.That(_batches, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, _batches[0].Items.Select(x => x.Id));
            Assert.That(_batcher.PendingCount<ItemEvent>(), Is.EqualTo(1));
        }

        [Test]
        public void MaxDelay_PublishesPartialBatch()
        {
            _batcher.Add(new ItemEvent(1));
            _batcher.Update(0.6f);
            Assert.That(_batches, Is.Empty);

            _batcher.Update(0.6f);

            Assert.That(_batches, Has.Count.EqualTo(1));
            Assert.That(_batches[0].Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void DelayCountsFromFirstItemOfBatch()
        {
            _batcher.Update(5f);
            _batcher.Add(new ItemEvent(1));
            _batcher.Update(0.5f);

            Assert.That(_batches, Is.Empty);
        }

        [Test]
        public void FlushAll_PublishesPending_AndBatchListsAreIndependent()
        {
            _batcher.Add(new ItemEvent(1));
            _batcher.FlushAll();
            _batcher.Add(new ItemEvent(2));
            _batcher.FlushAll();

            Assert.That(_batches, Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1 }, _batches[0].Items.Select(x => x.Id));
            CollectionAssert.AreEqual(new[] { 2 }, _batches[1].Items.Select(x => x.Id));
        }

        [Test]
        public void UnconfiguredType_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _batcher.Add(new TestEvent(1)));
        }
    }

    public sealed class EventRecorderTests
    {
        [Test]
        public void RecordsInOrder_AndEvictsOldestWhenFull()
        {
            var bus = new EventBus();
            using (var recorder = new EventRecorder(bus, capacity: 2))
            {
                bus.Publish(new TestEvent(1));
                bus.Publish(new TestEvent(2));
                bus.Publish(new TestEvent(3));

                var snapshot = recorder.Snapshot();
                Assert.That(snapshot.Count, Is.EqualTo(2));
                Assert.That(((TestEvent)snapshot[0].Payload).Value, Is.EqualTo(2));
                Assert.That(((TestEvent)snapshot[1].Payload).Value, Is.EqualTo(3));
                Assert.That(snapshot[1].Sequence, Is.EqualTo(3));
                Assert.That(recorder.TotalRecorded, Is.EqualTo(3));
            }
        }

        [Test]
        public void Filter_Pause_AndDispose_StopRecording()
        {
            var bus = new EventBus();
            var recorder = new EventRecorder(bus, filter: t => t == typeof(TestEvent));

            bus.Publish(new OtherEvent());
            bus.Publish(new TestEvent(1));
            recorder.IsPaused = true;
            bus.Publish(new TestEvent(2));
            recorder.IsPaused = false;
            recorder.Dispose();
            bus.Publish(new TestEvent(3));

            var snapshot = recorder.Snapshot();
            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(snapshot[0].EventType, Is.EqualTo(typeof(TestEvent)));
        }

        [Test]
        public void Clear_EmptiesBuffer()
        {
            var bus = new EventBus();
            var recorder = new EventRecorder(bus);
            bus.Publish(new TestEvent(1));

            recorder.Clear();

            Assert.That(recorder.Snapshot(), Is.Empty);
        }
    }
}
