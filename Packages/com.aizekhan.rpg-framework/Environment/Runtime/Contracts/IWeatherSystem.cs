namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Manages weather states and transitions in the game world.
    /// </summary>
    public interface IWeatherSystem
    {
        /// <summary>
        /// Gets the current weather type.
        /// </summary>
        WeatherType CurrentWeather { get; }

        /// <summary>
        /// Gets the intensity of the current weather (0.0 - 1.0).
        /// </summary>
        float WeatherIntensity { get; }

        /// <summary>
        /// Gets whether the weather is currently transitioning.
        /// </summary>
        bool IsTransitioning { get; }

        /// <summary>
        /// Changes the weather to a new type with optional transition duration.
        /// </summary>
        /// <param name="newWeather">The target weather type.</param>
        /// <param name="transitionDuration">Duration of the transition in seconds. 0 for instant change.</param>
        void ChangeWeather(WeatherType newWeather, float transitionDuration = 0f);

        /// <summary>
        /// Updates the weather system. Should be called each frame or update cycle.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        void Update(float deltaTime);

        /// <summary>
        /// Gets the weather effect data for the current weather state.
        /// </summary>
        WeatherEffectData GetCurrentEffects();

        /// <summary>
        /// Sets the weather intensity directly (useful for debugging or scripted events).
        /// </summary>
        void SetIntensity(float intensity);
    }
}
