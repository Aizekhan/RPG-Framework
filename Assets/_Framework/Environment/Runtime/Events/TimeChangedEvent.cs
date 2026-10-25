namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Event raised when the time of day changes.
    /// </summary>
    public class TimeChangedEvent
    {
        /// <summary>
        /// The previous time of day.
        /// </summary>
        public TimeOfDay PreviousTime { get; }

        /// <summary>
        /// The new time of day.
        /// </summary>
        public TimeOfDay NewTime { get; }

        /// <summary>
        /// The current hour (0-23).
        /// </summary>
        public int Hour { get; }

        /// <summary>
        /// The current minute (0-59).
        /// </summary>
        public int Minute { get; }

        /// <summary>
        /// The current day number.
        /// </summary>
        public int Day { get; }

        /// <summary>
        /// Creates a new time changed event.
        /// </summary>
        public TimeChangedEvent(TimeOfDay previousTime, TimeOfDay newTime, int hour, int minute, int day)
        {
            PreviousTime = previousTime;
            NewTime = newTime;
            Hour = hour;
            Minute = minute;
            Day = day;
        }
    }
}
