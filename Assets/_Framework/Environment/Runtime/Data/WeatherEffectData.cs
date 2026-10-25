namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Struct containing weather-based gameplay effect modifiers.
    /// </summary>
    public struct WeatherEffectData
    {
        /// <summary>
        /// Visibility range modifier (0.0 - 1.0, where 1.0 is full visibility).
        /// </summary>
        public float VisibilityModifier;

        /// <summary>
        /// Movement speed modifier (0.0 - 1.0, where 1.0 is full speed).
        /// </summary>
        public float MovementModifier;

        /// <summary>
        /// Combat accuracy modifier (0.0 - 1.0, where 1.0 is full accuracy).
        /// </summary>
        public float CombatModifier;

        /// <summary>
        /// Stamina drain rate modifier (1.0 = normal, higher = faster drain).
        /// </summary>
        public float StaminaModifier;

        /// <summary>
        /// Whether this weather deals environmental damage.
        /// </summary>
        public bool IsHarsh;

        /// <summary>
        /// Environmental damage per second (if IsHarsh is true).
        /// </summary>
        public float DamagePerSecond;

        /// <summary>
        /// Creates default weather effects with no modifiers.
        /// </summary>
        public static WeatherEffectData Default => new WeatherEffectData
        {
            VisibilityModifier = 1f,
            MovementModifier = 1f,
            CombatModifier = 1f,
            StaminaModifier = 1f,
            IsHarsh = false,
            DamagePerSecond = 0f
        };

        /// <summary>
        /// Creates weather effects for clear weather.
        /// </summary>
        public static WeatherEffectData Clear => Default;

        /// <summary>
        /// Creates weather effects for cloudy weather.
        /// </summary>
        public static WeatherEffectData Cloudy => new WeatherEffectData
        {
            VisibilityModifier = 0.95f,
            MovementModifier = 1f,
            CombatModifier = 0.98f,
            StaminaModifier = 1f,
            IsHarsh = false,
            DamagePerSecond = 0f
        };

        /// <summary>
        /// Creates weather effects for rain.
        /// </summary>
        public static WeatherEffectData Rain => new WeatherEffectData
        {
            VisibilityModifier = 0.8f,
            MovementModifier = 0.9f,
            CombatModifier = 0.85f,
            StaminaModifier = 1.1f,
            IsHarsh = false,
            DamagePerSecond = 0f
        };

        /// <summary>
        /// Creates weather effects for heavy rain.
        /// </summary>
        public static WeatherEffectData HeavyRain => new WeatherEffectData
        {
            VisibilityModifier = 0.5f,
            MovementModifier = 0.75f,
            CombatModifier = 0.7f,
            StaminaModifier = 1.3f,
            IsHarsh = false,
            DamagePerSecond = 0f
        };

        /// <summary>
        /// Creates weather effects for snow.
        /// </summary>
        public static WeatherEffectData Snow => new WeatherEffectData
        {
            VisibilityModifier = 0.7f,
            MovementModifier = 0.8f,
            CombatModifier = 0.8f,
            StaminaModifier = 1.2f,
            IsHarsh = false,
            DamagePerSecond = 0f
        };

        /// <summary>
        /// Creates weather effects for blizzard.
        /// </summary>
        public static WeatherEffectData Blizzard => new WeatherEffectData
        {
            VisibilityModifier = 0.3f,
            MovementModifier = 0.5f,
            CombatModifier = 0.5f,
            StaminaModifier = 1.8f,
            IsHarsh = true,
            DamagePerSecond = 2f
        };

        /// <summary>
        /// Creates weather effects for storm.
        /// </summary>
        public static WeatherEffectData Storm => new WeatherEffectData
        {
            VisibilityModifier = 0.4f,
            MovementModifier = 0.7f,
            CombatModifier = 0.6f,
            StaminaModifier = 1.5f,
            IsHarsh = true,
            DamagePerSecond = 1f
        };

        /// <summary>
        /// Creates weather effects for fog.
        /// </summary>
        public static WeatherEffectData Fog => new WeatherEffectData
        {
            VisibilityModifier = 0.3f,
            MovementModifier = 0.85f,
            CombatModifier = 0.6f,
            StaminaModifier = 1f,
            IsHarsh = false,
            DamagePerSecond = 0f
        };

        /// <summary>
        /// Creates weather effects for sandstorm.
        /// </summary>
        public static WeatherEffectData Sandstorm => new WeatherEffectData
        {
            VisibilityModifier = 0.2f,
            MovementModifier = 0.6f,
            CombatModifier = 0.5f,
            StaminaModifier = 1.7f,
            IsHarsh = true,
            DamagePerSecond = 3f
        };
    }
}
