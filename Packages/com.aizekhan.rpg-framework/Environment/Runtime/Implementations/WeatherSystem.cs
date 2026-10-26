using System;

namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Manages weather states and transitions.
    /// </summary>
    public class WeatherSystem : IWeatherSystem
    {
        private WeatherType _currentWeather;
        private float _currentIntensity;
        private bool _isTransitioning;
        private float _transitionProgress;
        private float _transitionDuration;
        private WeatherType _targetWeather;
        private float _startIntensity;

        /// <summary>
        /// Event raised when weather changes.
        /// </summary>
        public event Action<WeatherChangedEvent> OnWeatherChanged;

        public WeatherType CurrentWeather => _currentWeather;
        public float WeatherIntensity => _currentIntensity;
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// Creates a new weather system with the specified initial weather.
        /// </summary>
        public WeatherSystem(WeatherType initialWeather = WeatherType.Clear)
        {
            _currentWeather = initialWeather;
            _currentIntensity = 0f;
            _isTransitioning = false;
            _transitionProgress = 0f;
            _transitionDuration = 0f;
            _targetWeather = initialWeather;
            _startIntensity = 0f;
        }

        public void ChangeWeather(WeatherType newWeather, float transitionDuration = 0f)
        {
            if (newWeather == _currentWeather && !_isTransitioning)
            {
                return;
            }

            var previousWeather = _currentWeather;
            _targetWeather = newWeather;
            _transitionDuration = transitionDuration;
            _startIntensity = _currentIntensity;

            if (transitionDuration <= 0f)
            {
                _currentWeather = newWeather;
                _currentIntensity = newWeather == WeatherType.Clear ? 0f : 1f;
                _isTransitioning = false;
                _transitionProgress = 0f;
            }
            else
            {
                _isTransitioning = true;
                _transitionProgress = 0f;
            }

            OnWeatherChanged?.Invoke(new WeatherChangedEvent(previousWeather, newWeather, transitionDuration));
        }

        public void Update(float deltaTime)
        {
            if (!_isTransitioning)
            {
                return;
            }

            _transitionProgress += deltaTime;

            if (_transitionProgress >= _transitionDuration)
            {
                _currentWeather = _targetWeather;
                _currentIntensity = _targetWeather == WeatherType.Clear ? 0f : 1f;
                _isTransitioning = false;
                _transitionProgress = 0f;
            }
            else
            {
                float t = _transitionProgress / _transitionDuration;
                float targetIntensity = _targetWeather == WeatherType.Clear ? 0f : 1f;
                _currentIntensity = _startIntensity + (targetIntensity - _startIntensity) * t;

                if (t >= 0.5f && _currentWeather != _targetWeather)
                {
                    _currentWeather = _targetWeather;
                }
            }
        }

        public WeatherEffectData GetCurrentEffects()
        {
            var baseEffects = GetBaseEffects(_currentWeather);

            if (_currentIntensity < 1f && _currentWeather != WeatherType.Clear)
            {
                var clearEffects = WeatherEffectData.Clear;
                return LerpEffects(clearEffects, baseEffects, _currentIntensity);
            }

            return baseEffects;
        }

        public void SetIntensity(float intensity)
        {
            _currentIntensity = Math.Clamp(intensity, 0f, 1f);
        }

        private WeatherEffectData GetBaseEffects(WeatherType weather)
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

        private WeatherEffectData LerpEffects(WeatherEffectData from, WeatherEffectData to, float t)
        {
            return new WeatherEffectData
            {
                VisibilityModifier = from.VisibilityModifier + (to.VisibilityModifier - from.VisibilityModifier) * t,
                MovementModifier = from.MovementModifier + (to.MovementModifier - from.MovementModifier) * t,
                CombatModifier = from.CombatModifier + (to.CombatModifier - from.CombatModifier) * t,
                StaminaModifier = from.StaminaModifier + (to.StaminaModifier - from.StaminaModifier) * t,
                IsHarsh = t > 0.5f ? to.IsHarsh : from.IsHarsh,
                DamagePerSecond = from.DamagePerSecond + (to.DamagePerSecond - from.DamagePerSecond) * t
            };
        }
    }
}
