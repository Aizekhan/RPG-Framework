using UnityEngine;
using System;
using System.Collections;

namespace RPGFramework.Timers.Unity
{
    /// <summary>
    /// Global game speed: pause (menus), slow motion and temporary time scales on Unity's Time.timeScale, plus
    /// game/real time counters. One per game. ITimerService.TimeScale is separate and scales framework timers only.
    /// </summary>
    public class TimeScaleController : MonoBehaviour, ITimeScaleController
    {
        protected float timeScale = 1f;
        protected float previousTimeScale = 1f;
        protected bool isPaused = false;
        protected float gameTime = 0f;
        protected float realTime = 0f;
        protected Coroutine timeScaleCoroutine;

        // Events
        public event Action OnPaused;
        public event Action OnResumed;
        public event Action<float> OnTimeScaleChanged;

        public float TimeScale
        {
            get => timeScale;
            set
            {
                if (timeScale != value)
                {
                    timeScale = Mathf.Max(0f, value);
                    if (!isPaused)
                    {
                        UnityEngine.Time.timeScale = timeScale;
                    }
                    OnTimeScaleChanged?.Invoke(timeScale);
                }
            }
        }

        public bool IsPaused => isPaused;
        public float GameTime => gameTime;
        public float RealTime => realTime;

        protected virtual void Update()
        {
            // Track times
            realTime += UnityEngine.Time.unscaledDeltaTime;
            gameTime += UnityEngine.Time.deltaTime;
        }

        public virtual void Pause()
        {
            if (isPaused)
                return;

            isPaused = true;
            previousTimeScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 0f;

            OnPaused?.Invoke();
        }

        public virtual void Resume()
        {
            if (!isPaused)
                return;

            isPaused = false;
            UnityEngine.Time.timeScale = timeScale;

            OnResumed?.Invoke();
        }

        public virtual void TogglePause()
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        public virtual void SetTimeScale(float scale, float duration = 0f)
        {
            if (timeScaleCoroutine != null)
            {
                StopCoroutine(timeScaleCoroutine);
                timeScaleCoroutine = null;
            }

            if (duration > 0f)
            {
                timeScaleCoroutine = StartCoroutine(TemporaryTimeScaleCoroutine(scale, duration));
            }
            else
            {
                TimeScale = scale;
            }
        }

        protected virtual IEnumerator TemporaryTimeScaleCoroutine(float scale, float duration)
        {
            float originalScale = timeScale;
            TimeScale = scale;

            yield return new WaitForSecondsRealtime(duration);

            TimeScale = originalScale;
            timeScaleCoroutine = null;
        }

        /// <summary>
        /// Reset game time to zero.
        /// </summary>
        public virtual void ResetGameTime()
        {
            gameTime = 0f;
        }

        /// <summary>
        /// Reset real time to zero.
        /// </summary>
        public virtual void ResetRealTime()
        {
            realTime = 0f;
        }

        /// <summary>
        /// Reset both timers.
        /// </summary>
        public virtual void ResetAllTimers()
        {
            gameTime = 0f;
            realTime = 0f;
        }
    }
}
