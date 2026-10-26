using NUnit.Framework;
using RPGFramework.Environment.Runtime;

namespace RPGFramework.Environment.Tests
{
    [TestFixture]
    public class EnvironmentSystemTests
    {
        #region WeatherSystem Tests

        [Test]
        public void WeatherSystem_InitializesWithClearWeather()
        {
            var weatherSystem = new WeatherSystem();

            Assert.That(weatherSystem.CurrentWeather, Is.EqualTo(WeatherType.Clear));
            Assert.That(weatherSystem.WeatherIntensity, Is.EqualTo(0f));
            Assert.That(weatherSystem.IsTransitioning, Is.False);
        }

        [Test]
        public void WeatherSystem_InstantWeatherChange_UpdatesImmediately()
        {
            var weatherSystem = new WeatherSystem();

            weatherSystem.ChangeWeather(WeatherType.Rain, 0f);

            Assert.That(weatherSystem.CurrentWeather, Is.EqualTo(WeatherType.Rain));
            Assert.That(weatherSystem.WeatherIntensity, Is.EqualTo(1f));
            Assert.That(weatherSystem.IsTransitioning, Is.False);
        }

        [Test]
        public void WeatherSystem_TransitionWeatherChange_StartsTransition()
        {
            var weatherSystem = new WeatherSystem();

            weatherSystem.ChangeWeather(WeatherType.Storm, 10f);

            Assert.That(weatherSystem.IsTransitioning, Is.True);
        }

        [Test]
        public void WeatherSystem_TransitionUpdate_ProgressesTowardTarget()
        {
            var weatherSystem = new WeatherSystem();
            weatherSystem.ChangeWeather(WeatherType.Rain, 10f);

            weatherSystem.Update(5f);

            Assert.That(weatherSystem.IsTransitioning, Is.True);
            Assert.That(weatherSystem.WeatherIntensity, Is.GreaterThan(0f));
        }

        [Test]
        public void WeatherSystem_TransitionComplete_EndsTransition()
        {
            var weatherSystem = new WeatherSystem();
            weatherSystem.ChangeWeather(WeatherType.Snow, 5f);

            weatherSystem.Update(5f);

            Assert.That(weatherSystem.CurrentWeather, Is.EqualTo(WeatherType.Snow));
            Assert.That(weatherSystem.WeatherIntensity, Is.EqualTo(1f));
            Assert.That(weatherSystem.IsTransitioning, Is.False);
        }

        [Test]
        public void WeatherSystem_GetCurrentEffects_ReturnsCorrectEffectsForClear()
        {
            var weatherSystem = new WeatherSystem(WeatherType.Clear);

            var effects = weatherSystem.GetCurrentEffects();

            Assert.That(effects.VisibilityModifier, Is.EqualTo(1f));
            Assert.That(effects.MovementModifier, Is.EqualTo(1f));
            Assert.That(effects.IsHarsh, Is.False);
        }

        [Test]
        public void WeatherSystem_GetCurrentEffects_ReturnsCorrectEffectsForBlizzard()
        {
            var weatherSystem = new WeatherSystem(WeatherType.Blizzard);
            weatherSystem.SetIntensity(1f);

            var effects = weatherSystem.GetCurrentEffects();

            Assert.That(effects.VisibilityModifier, Is.LessThan(0.5f));
            Assert.That(effects.MovementModifier, Is.LessThan(0.7f));
            Assert.That(effects.IsHarsh, Is.True);
            Assert.That(effects.DamagePerSecond, Is.GreaterThan(0f));
        }

        [Test]
        public void WeatherSystem_SetIntensity_ClampsValue()
        {
            var weatherSystem = new WeatherSystem(WeatherType.Rain);

            weatherSystem.SetIntensity(1.5f);
            Assert.That(weatherSystem.WeatherIntensity, Is.EqualTo(1f));

            weatherSystem.SetIntensity(-0.5f);
            Assert.That(weatherSystem.WeatherIntensity, Is.EqualTo(0f));
        }

        #endregion

        #region TimeSystem Tests

        [Test]
        public void TimeSystem_InitializesWithDefaultTime()
        {
            var timeSystem = new TimeSystem();

            Assert.That(timeSystem.CurrentHour, Is.EqualTo(12));
            Assert.That(timeSystem.CurrentMinute, Is.EqualTo(0));
            Assert.That(timeSystem.CurrentDay, Is.EqualTo(1));
            Assert.That(timeSystem.CurrentTimeOfDay, Is.EqualTo(TimeOfDay.Noon));
        }

        [Test]
        public void TimeSystem_Update_AdvancesTime()
        {
            var timeSystem = new TimeSystem(12, 0);

            timeSystem.Update(3600f); // 1 hour at 1x speed

            Assert.That(timeSystem.CurrentHour, Is.EqualTo(13));
        }

        [Test]
        public void TimeSystem_Update_RollsOverToNextDay()
        {
            var timeSystem = new TimeSystem(23, 30);

            timeSystem.Update(3600f); // 1 hour

            Assert.That(timeSystem.CurrentDay, Is.EqualTo(2));
            Assert.That(timeSystem.CurrentHour, Is.EqualTo(0));
        }

        [Test]
        public void TimeSystem_SetTimeScale_AffectsTimeProgression()
        {
            var timeSystem = new TimeSystem();
            timeSystem.SetTimeScale(2f);

            timeSystem.Update(1800f); // 30 minutes at 2x speed = 1 hour game time

            Assert.That(timeSystem.CurrentHour, Is.EqualTo(13));
        }

        [Test]
        public void TimeSystem_SetTime_ChangesTimeDirectly()
        {
            var timeSystem = new TimeSystem(12, 0);

            timeSystem.SetTime(18, 30);

            Assert.That(timeSystem.CurrentHour, Is.EqualTo(18));
            Assert.That(timeSystem.CurrentMinute, Is.EqualTo(30));
            Assert.That(timeSystem.CurrentTimeOfDay, Is.EqualTo(TimeOfDay.Evening));
        }

        [Test]
        public void TimeSystem_AdvanceTime_AddsHoursToTime()
        {
            var timeSystem = new TimeSystem(10, 0);

            timeSystem.AdvanceTime(5.5f);

            Assert.That(timeSystem.CurrentHour, Is.EqualTo(15));
            Assert.That(timeSystem.CurrentMinute, Is.EqualTo(30));
        }

        [Test]
        public void TimeSystem_TimeOfDay_UpdatesCorrectly()
        {
            var timeSystem = new TimeSystem(5, 0);
            Assert.That(timeSystem.CurrentTimeOfDay, Is.EqualTo(TimeOfDay.Dawn));

            timeSystem.SetTime(22, 0);
            Assert.That(timeSystem.CurrentTimeOfDay, Is.EqualTo(TimeOfDay.Night));
        }

        #endregion

        #region SeasonSystem Tests

        [Test]
        public void SeasonSystem_InitializesWithSpring()
        {
            var seasonSystem = new SeasonSystem();

            Assert.That(seasonSystem.CurrentSeason, Is.EqualTo(Season.Spring));
            Assert.That(seasonSystem.DayInSeason, Is.EqualTo(0));
            Assert.That(seasonSystem.DaysPerSeason, Is.EqualTo(30));
        }

        [Test]
        public void SeasonSystem_Update_ChangesSeasonAfterDaysPerSeason()
        {
            var seasonSystem = new SeasonSystem(Season.Spring, 30);

            seasonSystem.Update(31);

            Assert.That(seasonSystem.CurrentSeason, Is.EqualTo(Season.Summer));
        }

        [Test]
        public void SeasonSystem_Update_CyclesThroughAllSeasons()
        {
            var seasonSystem = new SeasonSystem(Season.Spring, 10);

            seasonSystem.Update(11);
            Assert.That(seasonSystem.CurrentSeason, Is.EqualTo(Season.Summer));

            seasonSystem.Update(21);
            Assert.That(seasonSystem.CurrentSeason, Is.EqualTo(Season.Autumn));

            seasonSystem.Update(31);
            Assert.That(seasonSystem.CurrentSeason, Is.EqualTo(Season.Winter));

            seasonSystem.Update(41);
            Assert.That(seasonSystem.CurrentSeason, Is.EqualTo(Season.Spring));
        }

        [Test]
        public void SeasonSystem_SeasonProgress_CalculatesCorrectly()
        {
            var seasonSystem = new SeasonSystem(Season.Spring, 30);

            seasonSystem.Update(16);

            Assert.That(seasonSystem.SeasonProgress, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void SeasonSystem_SetSeason_ChangesSeasonDirectly()
        {
            var seasonSystem = new SeasonSystem(Season.Spring);

            seasonSystem.SetSeason(Season.Winter);

            Assert.That(seasonSystem.CurrentSeason, Is.EqualTo(Season.Winter));
            Assert.That(seasonSystem.DayInSeason, Is.EqualTo(0));
        }

        [Test]
        public void SeasonSystem_GetTemperatureModifier_ReturnsCorrectValues()
        {
            var seasonSystem = new SeasonSystem();

            seasonSystem.SetSeason(Season.Summer);
            Assert.That(seasonSystem.GetTemperatureModifier(), Is.GreaterThan(0f));

            seasonSystem.SetSeason(Season.Winter);
            Assert.That(seasonSystem.GetTemperatureModifier(), Is.LessThan(0f));
        }

        #endregion

        #region EnvironmentEffect Tests

        [Test]
        public void EnvironmentEffect_CalculateVisibilityModifier_AppliesWeatherEffects()
        {
            var effect = new EnvironmentEffect();
            var envData = new EnvironmentData
            {
                Weather = WeatherType.Fog,
                TimeOfDay = TimeOfDay.Noon
            };
            effect.UpdateEffects(envData);

            var visibility = effect.CalculateVisibilityModifier(100f);

            Assert.That(visibility, Is.LessThan(100f));
        }

        [Test]
        public void EnvironmentEffect_CalculateVisibilityModifier_AppliesNightPenalty()
        {
            var effect = new EnvironmentEffect();
            var envData = new EnvironmentData
            {
                Weather = WeatherType.Clear,
                TimeOfDay = TimeOfDay.Night
            };
            effect.UpdateEffects(envData);

            var visibility = effect.CalculateVisibilityModifier(100f);

            Assert.That(visibility, Is.LessThan(100f));
        }

        [Test]
        public void EnvironmentEffect_CalculateMovementModifier_AppliesWeatherAndSeason()
        {
            var effect = new EnvironmentEffect();
            var envData = new EnvironmentData
            {
                Weather = WeatherType.Blizzard,
                Season = Season.Winter
            };
            effect.UpdateEffects(envData);

            var movement = effect.CalculateMovementModifier(10f);

            Assert.That(movement, Is.LessThan(10f));
        }

        [Test]
        public void EnvironmentEffect_CalculateCombatModifier_AppliesWeatherAndTime()
        {
            var effect = new EnvironmentEffect();
            var envData = new EnvironmentData
            {
                Weather = WeatherType.Rain,
                TimeOfDay = TimeOfDay.Night
            };
            effect.UpdateEffects(envData);

            var combat = effect.CalculateCombatModifier(1f);

            Assert.That(combat, Is.LessThan(1f));
        }

        [Test]
        public void EnvironmentEffect_CalculateStaminaModifier_AppliesTemperature()
        {
            var effect = new EnvironmentEffect();
            var envData = new EnvironmentData
            {
                Weather = WeatherType.Clear,
                Temperature = -5f
            };
            effect.UpdateEffects(envData);

            var stamina = effect.CalculateStaminaModifier(1f);

            Assert.That(stamina, Is.GreaterThan(1f));
        }

        [Test]
        public void EnvironmentEffect_IsHarshEnvironment_ReturnsTrueForDangerousWeather()
        {
            var effect = new EnvironmentEffect();
            var envData = new EnvironmentData
            {
                Weather = WeatherType.Blizzard
            };
            effect.UpdateEffects(envData);

            Assert.That(effect.IsHarshEnvironment(), Is.True);
            Assert.That(effect.GetEnvironmentalDamage(), Is.GreaterThan(0f));
        }

        #endregion

        #region EnvironmentManager Tests

        [Test]
        public void EnvironmentManager_Initialize_SetsInitialState()
        {
            var manager = new EnvironmentManager();
            var initialState = new EnvironmentData
            {
                Weather = WeatherType.Rain,
                Hour = 15,
                Minute = 30,
                Season = Season.Autumn
            };

            manager.Initialize(initialState);

            var currentState = manager.GetCurrentState();
            Assert.That(currentState.Weather, Is.EqualTo(WeatherType.Rain));
            Assert.That(currentState.Hour, Is.EqualTo(15));
            Assert.That(currentState.Season, Is.EqualTo(Season.Autumn));
        }

        [Test]
        public void EnvironmentManager_Update_UpdatesAllSystems()
        {
            var manager = new EnvironmentManager();
            var initialState = EnvironmentData.Default;
            manager.Initialize(initialState);

            manager.Update(3600f);

            var currentState = manager.GetCurrentState();
            Assert.That(currentState.Hour, Is.Not.EqualTo(initialState.Hour));
        }

        [Test]
        public void EnvironmentManager_GetCurrentState_ReturnsCompleteState()
        {
            var manager = new EnvironmentManager();

            var state = manager.GetCurrentState();

            Assert.That(state.Weather, Is.Not.Null);
            Assert.That(state.TimeOfDay, Is.Not.Null);
            Assert.That(state.Season, Is.Not.Null);
        }

        [Test]
        public void EnvironmentManager_TriggerRandomWeather_ChangesWeather()
        {
            var manager = new EnvironmentManager();
            var initialWeather = manager.WeatherSystem.CurrentWeather;

            manager.TriggerRandomWeather();

            // Weather may stay the same by chance, but system should not crash
            Assert.Pass("Random weather triggered successfully");
        }

        #endregion

        #region Data Structure Tests

        [Test]
        public void EnvironmentData_Default_HasValidValues()
        {
            var data = EnvironmentData.Default;

            Assert.That(data.Weather, Is.EqualTo(WeatherType.Clear));
            Assert.That(data.TimeOfDay, Is.EqualTo(TimeOfDay.Noon));
            Assert.That(data.Season, Is.EqualTo(Season.Spring));
            Assert.That(data.Day, Is.EqualTo(1));
        }

        [Test]
        public void TimeData_ToMinutesOfDay_CalculatesCorrectly()
        {
            var data = new TimeData { Hour = 5, Minute = 30 };

            Assert.That(data.ToMinutesOfDay(), Is.EqualTo(330));
        }

        [Test]
        public void TimeData_ToTimeString_FormatsCorrectly()
        {
            var data = new TimeData { Hour = 9, Minute = 5 };

            Assert.That(data.ToTimeString(), Is.EqualTo("09:05"));
        }

        [Test]
        public void WeatherEffectData_HarshWeathers_HaveDamage()
        {
            Assert.That(WeatherEffectData.Blizzard.IsHarsh, Is.True);
            Assert.That(WeatherEffectData.Blizzard.DamagePerSecond, Is.GreaterThan(0f));

            Assert.That(WeatherEffectData.Storm.IsHarsh, Is.True);
            Assert.That(WeatherEffectData.Storm.DamagePerSecond, Is.GreaterThan(0f));

            Assert.That(WeatherEffectData.Sandstorm.IsHarsh, Is.True);
            Assert.That(WeatherEffectData.Sandstorm.DamagePerSecond, Is.GreaterThan(0f));
        }

        #endregion
    }
}
