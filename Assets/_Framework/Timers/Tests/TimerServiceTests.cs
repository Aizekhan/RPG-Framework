using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.Logging;

namespace RPGFramework.Timers.Tests
{
    public sealed class TimerServiceTests
    {
        private TimerService _timers;
        private MemoryLogSink _sink;

        [SetUp]
        public void SetUp()
        {
            _sink = new MemoryLogSink();
            _timers = new TimerService(new Logger(LogSeverity.Trace, _sink));
        }

        [Test]
        public void Schedule_FiresOnceAfterDelay_ThenHandleIsStale()
        {
            int fired = 0;
            var handle = _timers.Schedule(1f, () => fired++);

            _timers.Update(0.6f);
            Assert.That(fired, Is.Zero);
            Assert.That(_timers.GetRemaining(handle), Is.EqualTo(0.4f).Within(1e-5f));

            _timers.Update(0.6f);
            _timers.Update(5f);

            Assert.That(fired, Is.EqualTo(1));
            Assert.That(_timers.IsActive(handle), Is.False);
            Assert.That(_timers.Cancel(handle), Is.False);
            Assert.That(_timers.ActiveCount, Is.Zero);
        }

        [Test]
        public void Repeat_FiresEveryCrossedInterval_WithoutDrift_AndStopsAfterCount()
        {
            int ticks = 0;
            var handle = _timers.Repeat(1f, () => ticks++, repeatCount: 5);

            _timers.Update(2.5f); // crosses 1 and 2
            Assert.That(ticks, Is.EqualTo(2));
            Assert.That(_timers.GetRemaining(handle), Is.EqualTo(0.5f).Within(1e-5f));

            _timers.Update(10f); // only 3 repeats left
            Assert.That(ticks, Is.EqualTo(5));
            Assert.That(_timers.IsActive(handle), Is.False);
        }

        [Test]
        public void InfiniteRepeat_RunsUntilCancelled()
        {
            int ticks = 0;
            var handle = _timers.Repeat(0.5f, () => ticks++);

            for (int i = 0; i < 10; i++)
                _timers.Update(0.5f);
            _timers.Cancel(handle);
            _timers.Update(5f);

            Assert.That(ticks, Is.EqualTo(10));
        }

        [Test]
        public void PauseAndResume_FreezeRemainingTime()
        {
            int fired = 0;
            var handle = _timers.Schedule(1f, () => fired++);

            _timers.Update(0.5f);
            Assert.That(_timers.Pause(handle), Is.True);
            _timers.Update(10f);
            Assert.That(fired, Is.Zero);
            Assert.That(_timers.IsPaused(handle), Is.True);

            _timers.Resume(handle);
            _timers.Update(0.5f);
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void Categories_CanBePausedResumedAndCancelledTogether()
        {
            var log = new List<string>();
            _timers.Schedule(1f, () => log.Add("combat1"), "Combat");
            _timers.Schedule(1f, () => log.Add("combat2"), "Combat");
            _timers.Schedule(1f, () => log.Add("ui"), "UI");

            Assert.That(_timers.PauseCategory("Combat"), Is.EqualTo(2));
            _timers.Update(1f);
            CollectionAssert.AreEqual(new[] { "ui" }, log);

            Assert.That(_timers.CancelCategory("Combat"), Is.EqualTo(2));
            _timers.Update(5f);
            CollectionAssert.AreEqual(new[] { "ui" }, log);
            Assert.That(_timers.ActiveCount, Is.Zero);
        }

        [Test]
        public void TimeScale_ScalesAllTimers_ZeroFreezes()
        {
            int fired = 0;
            _timers.Schedule(1f, () => fired++);

            _timers.TimeScale = 0f;
            _timers.Update(100f);
            Assert.That(fired, Is.Zero);

            _timers.TimeScale = 2f;
            _timers.Update(0.5f);
            Assert.That(fired, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _timers.TimeScale = -1f);
        }

        [Test]
        public void ScheduleFromCallback_StartsNextUpdate_CancelFromCallbackWorks()
        {
            var log = new List<string>();
            TimerHandle other = default;
            _timers.Schedule(1f, () =>
            {
                log.Add("first");
                _timers.Schedule(0f, () => log.Add("chained"));
                _timers.Cancel(other);
            });
            other = _timers.Schedule(1f, () => log.Add("other"));

            _timers.Update(1f);
            CollectionAssert.AreEqual(new[] { "first" }, log);

            _timers.Update(0.01f);
            CollectionAssert.AreEqual(new[] { "first", "chained" }, log);
        }

        [Test]
        public void ThrowingCallback_IsIsolatedAndLogged()
        {
            bool otherFired = false;
            _timers.Schedule(1f, () => throw new InvalidOperationException("boom"), "Combat");
            _timers.Schedule(1f, () => otherFired = true);

            Assert.DoesNotThrow(() => _timers.Update(1f));

            Assert.That(otherFired, Is.True);
            Assert.That(_sink.Snapshot().Any(e => e.Severity == LogSeverity.Error && e.Exception is InvalidOperationException), Is.True);
        }

        [Test]
        public void CallbackPausingItsRepeatTimer_StopsCatchUp()
        {
            int ticks = 0;
            TimerHandle handle = default;
            handle = _timers.Repeat(1f, () =>
            {
                ticks++;
                _timers.Pause(handle);
            });

            _timers.Update(5f);

            Assert.That(ticks, Is.EqualTo(1));
        }

        [Test]
        public void DelayAsync_CompletesInGameTime_AndSupportsCancellation()
        {
            var delay = _timers.DelayAsync(2f);
            _timers.Update(1f);
            Assert.That(delay.Status, Is.EqualTo(UniTaskStatus.Pending));
            _timers.Update(1f);
            Assert.That(delay.Status, Is.EqualTo(UniTaskStatus.Succeeded));

            var cts = new CancellationTokenSource();
            var cancelled = _timers.DelayAsync(2f, cts.Token);
            cts.Cancel();
            Assert.That(cancelled.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_timers.ActiveCount, Is.Zero);
        }

        [Test]
        public void GetTimers_DescribesActiveTimers()
        {
            var handle = _timers.Repeat(2f, () => { }, 3, "Regen");
            _timers.Update(0.5f);

            var info = _timers.GetTimers().Single();

            Assert.That(info.Handle, Is.EqualTo(handle));
            Assert.That(info.Category, Is.EqualTo("Regen"));
            Assert.That(info.Remaining, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(info.RemainingRepeats, Is.EqualTo(3));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _timers.Schedule(1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _timers.Schedule(-1f, () => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _timers.Repeat(0f, () => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _timers.Repeat(1f, () => { }, 0));
            Assert.That(_timers.IsActive(TimerHandle.None), Is.False);
        }
    }
}
