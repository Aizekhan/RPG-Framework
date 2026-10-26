using NUnit.Framework;
using RPGFramework.Timers.Unity;
using UnityEngine;

namespace RPGFramework.Timers.Tests
{
    public sealed class TimeScaleControllerTests
    {
        private GameObject _host;
        private TimeScaleController _controller;
        private float _originalScale;

        [SetUp]
        public void SetUp()
        {
            _originalScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 1f;
            _host = new GameObject("Time");
            _controller = _host.AddComponent<TimeScaleController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            UnityEngine.Time.timeScale = _originalScale;
        }

        [Test]
        public void PauseAndResume_RestoreTheChosenScale()
        {
            int paused = 0, resumed = 0;
            _controller.OnPaused += () => paused++;
            _controller.OnResumed += () => resumed++;

            _controller.TimeScale = 0.5f;
            _controller.Pause();
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f));
            Assert.That(_controller.IsPaused, Is.True);

            _controller.TimeScale = 2f; // changed while paused: applied on resume
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f));

            _controller.TogglePause();
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(2f));
            Assert.That(paused, Is.EqualTo(1));
            Assert.That(resumed, Is.EqualTo(1));
        }

        [Test]
        public void TimeScale_IsNeverNegative()
        {
            _controller.SetTimeScale(-3f);

            Assert.That(_controller.TimeScale, Is.EqualTo(0f));
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f));
        }
    }
}
