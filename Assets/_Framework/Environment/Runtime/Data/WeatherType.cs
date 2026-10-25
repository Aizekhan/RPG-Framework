namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Defines the different types of weather conditions.
    /// </summary>
    public enum WeatherType
    {
        /// <summary>
        /// Clear skies with no precipitation.
        /// </summary>
        Clear,

        /// <summary>
        /// Overcast skies with cloud coverage.
        /// </summary>
        Cloudy,

        /// <summary>
        /// Light to moderate rainfall.
        /// </summary>
        Rain,

        /// <summary>
        /// Heavy rainfall with reduced visibility.
        /// </summary>
        HeavyRain,

        /// <summary>
        /// Snowfall with cold temperatures.
        /// </summary>
        Snow,

        /// <summary>
        /// Heavy snowfall with severe visibility reduction.
        /// </summary>
        Blizzard,

        /// <summary>
        /// Thunderstorm with lightning and heavy rain.
        /// </summary>
        Storm,

        /// <summary>
        /// Dense fog with severely reduced visibility.
        /// </summary>
        Fog,

        /// <summary>
        /// Desert sandstorm with reduced visibility and movement.
        /// </summary>
        Sandstorm
    }
}
