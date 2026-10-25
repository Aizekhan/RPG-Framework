namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Manages time progression and time of day in the game world.
    /// </summary>
    public interface ITimeSystem
    {
        /// <summary>
        /// Gets the current time of day.
        /// </summary>
        TimeOfDay CurrentTimeOfDay { get; }

        /// <summary>
        /// Gets the current hour (0-23).
        /// </summary>
        int CurrentHour { get; }

        /// <summary>
        /// Gets the current minute (0-59).
        /// </summary>
        int CurrentMinute { get; }

        /// <summary>
        /// Gets the current day number since game start.
        /// </summary>
        int CurrentDay { get; }

        /// <summary>
        /// Gets the time scale multiplier (1.0 = real-time, higher = faster).
        /// </summary>
        float TimeScale { get; }

        /// <summary>
        /// Gets the total elapsed game time in seconds.
        /// </summary>
        float TotalGameTime { get; }

        /// <summary>
        /// Updates the time system.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        void Update(float deltaTime);

        /// <summary>
        /// Sets the time scale for time progression.
        /// </summary>
        void SetTimeScale(float scale);

        /// <summary>
        /// Sets the current time directly.
        /// </summary>
        void SetTime(int hour, int minute);

        /// <summary>
        /// Advances time by a specified number of hours.
        /// </summary>
        void AdvanceTime(float hours);
    }
}
