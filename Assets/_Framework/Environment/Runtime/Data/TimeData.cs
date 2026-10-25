namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Struct for tracking detailed time information.
    /// </summary>
    public struct TimeData
    {
        /// <summary>
        /// Total elapsed game time in seconds.
        /// </summary>
        public float TotalSeconds;

        /// <summary>
        /// Current hour (0-23).
        /// </summary>
        public int Hour;

        /// <summary>
        /// Current minute (0-59).
        /// </summary>
        public int Minute;

        /// <summary>
        /// Current day number since game start (1-based).
        /// </summary>
        public int Day;

        /// <summary>
        /// Time of day period.
        /// </summary>
        public TimeOfDay TimeOfDay;

        /// <summary>
        /// Time scale multiplier (1.0 = real-time).
        /// </summary>
        public float TimeScale;

        /// <summary>
        /// Creates a default time data starting at day 1, noon.
        /// </summary>
        public static TimeData Default => new TimeData
        {
            TotalSeconds = 0f,
            Hour = 12,
            Minute = 0,
            Day = 1,
            TimeOfDay = TimeOfDay.Noon,
            TimeScale = 1f
        };

        /// <summary>
        /// Converts the time to total minutes since start of day.
        /// </summary>
        public int ToMinutesOfDay() => Hour * 60 + Minute;

        /// <summary>
        /// Converts the time to a formatted string (HH:MM).
        /// </summary>
        public string ToTimeString() => $"{Hour:D2}:{Minute:D2}";
    }
}
