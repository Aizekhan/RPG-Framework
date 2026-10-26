namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// ECS component containing the complete current environmental state.
    /// </summary>
    public struct EnvironmentData
    {
        /// <summary>
        /// The current weather type.
        /// </summary>
        public WeatherType Weather;

        /// <summary>
        /// The intensity of the weather (0.0 - 1.0).
        /// </summary>
        public float WeatherIntensity;

        /// <summary>
        /// The current time of day.
        /// </summary>
        public TimeOfDay TimeOfDay;

        /// <summary>
        /// The current hour (0-23).
        /// </summary>
        public int Hour;

        /// <summary>
        /// The current minute (0-59).
        /// </summary>
        public int Minute;

        /// <summary>
        /// The current season.
        /// </summary>
        public Season Season;

        /// <summary>
        /// The current day number.
        /// </summary>
        public int Day;

        /// <summary>
        /// Temperature in degrees Celsius.
        /// </summary>
        public float Temperature;

        /// <summary>
        /// Wind speed (0.0 - 1.0 normalized).
        /// </summary>
        public float WindSpeed;

        /// <summary>
        /// Creates a new environment data with default values.
        /// </summary>
        public static EnvironmentData Default => new EnvironmentData
        {
            Weather = WeatherType.Clear,
            WeatherIntensity = 0f,
            TimeOfDay = TimeOfDay.Noon,
            Hour = 12,
            Minute = 0,
            Season = Season.Spring,
            Day = 1,
            Temperature = 20f,
            WindSpeed = 0f
        };
    }
}
