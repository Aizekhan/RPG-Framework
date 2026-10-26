using System;

namespace RPGFramework.Timers.Unity
{
    /// <summary>
    /// Interface for time management (pause, slow-motion, time scale).
    /// Provides centralized control over game time flow.
    /// </summary>
    public interface ITimeScaleController
    {
        /// <summary>
        /// Current time scale (0 = paused, 1 = normal, >1 = fast, <1 = slow).
        /// </summary>
        float TimeScale { get; set; }

        /// <summary>
        /// Is game currently paused?
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Game time (affected by time scale).
        /// </summary>
        float GameTime { get; }

        /// <summary>
        /// Real time (unaffected by time scale).
        /// </summary>
        float RealTime { get; }

        /// <summary>
        /// Pause the game.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume the game.
        /// </summary>
        void Resume();

        /// <summary>
        /// Toggle pause state.
        /// </summary>
        void TogglePause();

        /// <summary>
        /// Set time scale with optional duration (returns to normal after duration).
        /// </summary>
        void SetTimeScale(float scale, float duration = 0f);
    }
}
