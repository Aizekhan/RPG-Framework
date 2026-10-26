using System;

namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Coordinates all environmental systems.
    /// </summary>
    public class EnvironmentManager : IEnvironmentManager
    {
        private readonly IWeatherSystem _weatherSystem;
        private readonly ITimeSystem _timeSystem;
        private readonly ISeasonSystem _seasonSystem;
        private readonly IEnvironmentEffect _environmentEffect;
        private readonly Random _random;

        public IWeatherSystem WeatherSystem => _weatherSystem;
        public ITimeSystem TimeSystem => _timeSystem;
        public ISeasonSystem SeasonSystem => _seasonSystem;
        public IEnvironmentEffect EnvironmentEffect => _environmentEffect;

        /// <summary>
        /// Creates a new environment manager with the specified systems.
        /// </summary>
        public EnvironmentManager(
            IWeatherSystem weatherSystem,
            ITimeSystem timeSystem,
            ISeasonSystem seasonSystem,
            IEnvironmentEffect environmentEffect)
        {
            _weatherSystem = weatherSystem ?? throw new ArgumentNullException(nameof(weatherSystem));
            _timeSystem = timeSystem ?? throw new ArgumentNullException(nameof(timeSystem));
            _seasonSystem = seasonSystem ?? throw new ArgumentNullException(nameof(seasonSystem));
            _environmentEffect = environmentEffect ?? throw new ArgumentNullException(nameof(environmentEffect));
            _random = new Random();
        }

        /// <summary>
        /// Creates a new environment manager with default systems.
        /// </summary>
        public EnvironmentManager() : this(
            new WeatherSystem(),
            new TimeSystem(),
            new SeasonSystem(),
            new EnvironmentEffect())
        {
        }

        public EnvironmentData GetCurrentState()
        {
            return new EnvironmentData
            {
                Weather = _weatherSystem.CurrentWeather,
                WeatherIntensity = _weatherSystem.WeatherIntensity,
                TimeOfDay = _timeSystem.CurrentTimeOfDay,
                Hour = _timeSystem.CurrentHour,
                Minute = _timeSystem.CurrentMinute,
                Season = _seasonSystem.CurrentSeason,
                Day = _timeSystem.CurrentDay,
                Temperature = CalculateTemperature(),
                WindSpeed = CalculateWindSpeed()
            };
        }

        public void Update(float deltaTime)
        {
            _timeSystem.Update(deltaTime);
            _seasonSystem.Update(_timeSystem.CurrentDay);
            _weatherSystem.Update(deltaTime);

            var currentState = GetCurrentState();
            _environmentEffect.UpdateEffects(currentState);
        }

        public void Initialize(EnvironmentData initialState)
        {
            _timeSystem.SetTime(initialState.Hour, initialState.Minute);
            _seasonSystem.SetSeason(initialState.Season);
            _weatherSystem.ChangeWeather(initialState.Weather, 0f);
            _weatherSystem.SetIntensity(initialState.WeatherIntensity);

            var currentState = GetCurrentState();
            _environmentEffect.UpdateEffects(currentState);
        }

        public void TriggerRandomWeather()
        {
            var availableWeathers = GetWeatherForSeason(_seasonSystem.CurrentSeason);
            var randomWeather = availableWeathers[_random.Next(availableWeathers.Length)];
            var transitionDuration = _random.Next(30, 120);

            _weatherSystem.ChangeWeather(randomWeather, transitionDuration);
        }

        private float CalculateTemperature()
        {
            float baseTemp = 20f;

            float seasonModifier = _seasonSystem.GetTemperatureModifier() * 20f;

            float timeModifier = _timeSystem.CurrentTimeOfDay switch
            {
                TimeOfDay.Dawn => -5f,
                TimeOfDay.Morning => 0f,
                TimeOfDay.Noon => 5f,
                TimeOfDay.Afternoon => 3f,
                TimeOfDay.Evening => -2f,
                TimeOfDay.Dusk => -5f,
                TimeOfDay.Night => -8f,
                TimeOfDay.Midnight => -10f,
                _ => 0f
            };

            float weatherModifier = _weatherSystem.CurrentWeather switch
            {
                WeatherType.Snow or WeatherType.Blizzard => -10f,
                WeatherType.Rain or WeatherType.HeavyRain => -5f,
                WeatherType.Storm => -7f,
                WeatherType.Clear => 2f,
                _ => 0f
            };

            return baseTemp + seasonModifier + timeModifier + weatherModifier;
        }

        private float CalculateWindSpeed()
        {
            return _weatherSystem.CurrentWeather switch
            {
                WeatherType.Clear => 0.1f,
                WeatherType.Cloudy => 0.3f,
                WeatherType.Rain => 0.5f,
                WeatherType.HeavyRain => 0.7f,
                WeatherType.Snow => 0.4f,
                WeatherType.Blizzard => 0.9f,
                WeatherType.Storm => 1f,
                WeatherType.Fog => 0.2f,
                WeatherType.Sandstorm => 1f,
                _ => 0f
            };
        }

        private WeatherType[] GetWeatherForSeason(Season season)
        {
            return season switch
            {
                Season.Spring => new[] { WeatherType.Clear, WeatherType.Cloudy, WeatherType.Rain, WeatherType.Storm },
                Season.Summer => new[] { WeatherType.Clear, WeatherType.Cloudy, WeatherType.Storm, WeatherType.Fog },
                Season.Autumn => new[] { WeatherType.Clear, WeatherType.Cloudy, WeatherType.Rain, WeatherType.HeavyRain, WeatherType.Fog },
                Season.Winter => new[] { WeatherType.Cloudy, WeatherType.Snow, WeatherType.Blizzard, WeatherType.Fog },
                _ => new[] { WeatherType.Clear }
            };
        }
    }
}
