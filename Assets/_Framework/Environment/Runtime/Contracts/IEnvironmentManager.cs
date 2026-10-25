namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Coordinates all environmental systems (weather, time, seasons, effects).
    /// </summary>
    public interface IEnvironmentManager
    {
        /// <summary>
        /// Gets the weather system.
        /// </summary>
        IWeatherSystem WeatherSystem { get; }

        /// <summary>
        /// Gets the time system.
        /// </summary>
        ITimeSystem TimeSystem { get; }

        /// <summary>
        /// Gets the season system.
        /// </summary>
        ISeasonSystem SeasonSystem { get; }

        /// <summary>
        /// Gets the environment effect calculator.
        /// </summary>
        IEnvironmentEffect EnvironmentEffect { get; }

        /// <summary>
        /// Gets the current complete environment state.
        /// </summary>
        EnvironmentData GetCurrentState();

        /// <summary>
        /// Updates all environmental systems.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        void Update(float deltaTime);

        /// <summary>
        /// Initializes the environment manager with starting conditions.
        /// </summary>
        void Initialize(EnvironmentData initialState);

        /// <summary>
        /// Triggers a random weather change based on current season.
        /// </summary>
        void TriggerRandomWeather();
    }
}
