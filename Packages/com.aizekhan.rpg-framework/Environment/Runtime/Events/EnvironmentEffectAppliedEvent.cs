using RPGFramework.Events;

namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Event raised when environmental effects are applied to gameplay.
    /// Raised through <see cref="EnvironmentEffect.OnEffectApplied"/> when the effects in force change: the first
    /// <c>UpdateEffects</c>, then each change of weather (not on every <see cref="EnvironmentManager"/> update).
    /// </summary>
    public class EnvironmentEffectAppliedEvent : IEvent
    {
        /// <summary>
        /// The current environment state.
        /// </summary>
        public EnvironmentData EnvironmentData { get; }

        /// <summary>
        /// The applied weather effects.
        /// </summary>
        public WeatherEffectData WeatherEffects { get; }

        /// <summary>
        /// Whether the environment is currently harsh.
        /// </summary>
        public bool IsHarsh { get; }

        /// <summary>
        /// Environmental damage being applied (if any).
        /// </summary>
        public float EnvironmentalDamage { get; }

        /// <summary>
        /// Creates a new environment effect applied event.
        /// </summary>
        public EnvironmentEffectAppliedEvent(EnvironmentData environmentData, WeatherEffectData weatherEffects, bool isHarsh, float environmentalDamage)
        {
            EnvironmentData = environmentData;
            WeatherEffects = weatherEffects;
            IsHarsh = isHarsh;
            EnvironmentalDamage = environmentalDamage;
        }
    }
}
