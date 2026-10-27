using RPGFramework.Events;

namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Event raised when the weather changes.
    /// Raised through <see cref="WeatherSystem.OnWeatherChanged"/>; forward it to an event bus if your game needs one.
    /// </summary>
    public class WeatherChangedEvent : IEvent
    {
        /// <summary>
        /// The previous weather type.
        /// </summary>
        public WeatherType PreviousWeather { get; }

        /// <summary>
        /// The new weather type.
        /// </summary>
        public WeatherType NewWeather { get; }

        /// <summary>
        /// The transition duration in seconds.
        /// </summary>
        public float TransitionDuration { get; }

        /// <summary>
        /// Creates a new weather changed event.
        /// </summary>
        public WeatherChangedEvent(WeatherType previousWeather, WeatherType newWeather, float transitionDuration)
        {
            PreviousWeather = previousWeather;
            NewWeather = newWeather;
            TransitionDuration = transitionDuration;
        }
    }
}
