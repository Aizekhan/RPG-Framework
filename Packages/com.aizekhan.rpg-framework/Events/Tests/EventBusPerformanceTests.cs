using System.Diagnostics;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace RPGFramework.Events.Tests
{
    /// <summary>
    /// A busy frame on the bus: a thousand struct events, each heard by three subscribers, allocate nothing, and the
    /// benchmark prints its numbers to the test output (budgets are generous: they catch regressions, not jitter).
    /// </summary>
    public sealed class EventBusPerformanceTests
    {
        private readonly struct Hit : IEvent
        {
            public Hit(int target, float amount)
            {
                Target = target;
                Amount = amount;
            }

            public int Target { get; }
            public float Amount { get; }
        }

        private EventBus _bus;
        private float _damage;
        private int _hits;
        private int _lastTarget;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _bus.Subscribe<Hit>(OnDamage, EventPriority.High);
            _bus.Subscribe<Hit>(OnCount);
            _bus.Subscribe<Hit>(OnTarget, EventPriority.Low);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        private void OnDamage(Hit hit) => _damage += hit.Amount;
        private void OnCount(Hit hit) => _hits++;
        private void OnTarget(Hit hit) => _lastTarget = hit.Target;

        private void Frame()
        {
            for (int i = 0; i < 1000; i++)
                _bus.Publish(new Hit(i, 1.5f));
        }

        [Test]
        public void AThousandEventsAFrame_AllocateNothing()
        {
            Frame(); // warm up

            Assert.That(Frame, Is.Not.AllocatingGCMemory());
            Assert.That(_hits, Is.EqualTo(2000));
        }

        [Test]
        public void Benchmark_AThousandEventsAFrame_ThreeSubscribersEach()
        {
            const int frames = 200;
            Frame();
            var clock = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
                Frame();
            clock.Stop();

            double perFrame = clock.Elapsed.TotalMilliseconds / frames;
            TestContext.WriteLine($"[Benchmark] EventBus: 1000 events x 3 subscribers: {perFrame:0.000} ms per frame (last target {_lastTarget}, damage {_damage})");
            Assert.That(perFrame, Is.LessThan(2.0), "an eighth of a 60 fps frame");
        }
    }
}
