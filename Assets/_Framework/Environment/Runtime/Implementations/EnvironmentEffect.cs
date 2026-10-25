namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Applies environmental effects to gameplay mechanics.
    /// </summary>
    public class EnvironmentEffect : IEnvironmentEffect
    {
        private WeatherEffectData _currentEffects;
        private EnvironmentData _currentEnvironment;

        /// <summary>
        /// Creates a new environment effect calculator.
        /// </summary>
        public EnvironmentEffect()
        {
            _currentEffects = WeatherEffectData.Default;
            _currentEnvironment = EnvironmentData.Default;
        }

        public float CalculateVisibilityModifier(float baseVisibility)
        {
            float modifier = _currentEffects.VisibilityModifier;

            if (_currentEnvironment.TimeOfDay == TimeOfDay.Night || _currentEnvironment.TimeOfDay == TimeOfDay.Midnight)
            {
                modifier *= 0.5f;
            }
            else if (_currentEnvironment.TimeOfDay == TimeOfDay.Dawn || _currentEnvironment.TimeOfDay == TimeOfDay.Dusk)
            {
                modifier *= 0.7f;
            }

            return baseVisibility * modifier;
        }

        public float CalculateMovementModifier(float baseSpeed)
        {
            float modifier = _currentEffects.MovementModifier;

            if (_currentEnvironment.Season == Season.Winter)
            {
                modifier *= 0.9f;
            }

            return baseSpeed * modifier;
        }

        public float CalculateCombatModifier(float baseAccuracy)
        {
            float modifier = _currentEffects.CombatModifier;

            if (_currentEnvironment.TimeOfDay == TimeOfDay.Night || _currentEnvironment.TimeOfDay == TimeOfDay.Midnight)
            {
                modifier *= 0.8f;
            }

            if (_currentEnvironment.WindSpeed > 0.7f)
            {
                modifier *= 0.9f;
            }

            return baseAccuracy * modifier;
        }

        public float CalculateStaminaModifier(float baseDrain)
        {
            float modifier = _currentEffects.StaminaModifier;

            if (_currentEnvironment.Temperature < 0f)
            {
                modifier *= 1.2f;
            }
            else if (_currentEnvironment.Temperature > 30f)
            {
                modifier *= 1.3f;
            }

            if (_currentEnvironment.Season == Season.Winter)
            {
                modifier *= 1.1f;
            }

            return baseDrain * modifier;
        }

        public bool IsHarshEnvironment()
        {
            return _currentEffects.IsHarsh;
        }

        public float GetEnvironmentalDamage()
        {
            return _currentEffects.IsHarsh ? _currentEffects.DamagePerSecond : 0f;
        }

        public void UpdateEffects(EnvironmentData environmentData)
        {
            _currentEnvironment = environmentData;
            _currentEffects = GetWeatherEffects(environmentData.Weather);
        }

        private WeatherEffectData GetWeatherEffects(WeatherType weather)
        {
            return weather switch
            {
                WeatherType.Clear => WeatherEffectData.Clear,
                WeatherType.Cloudy => WeatherEffectData.Cloudy,
                WeatherType.Rain => WeatherEffectData.Rain,
                WeatherType.HeavyRain => WeatherEffectData.HeavyRain,
                WeatherType.Snow => WeatherEffectData.Snow,
                WeatherType.Blizzard => WeatherEffectData.Blizzard,
                WeatherType.Storm => WeatherEffectData.Storm,
                WeatherType.Fog => WeatherEffectData.Fog,
                WeatherType.Sandstorm => WeatherEffectData.Sandstorm,
                _ => WeatherEffectData.Default
            };
        }
    }
}
