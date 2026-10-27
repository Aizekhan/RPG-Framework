using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Environment.Runtime;
using RPGFramework.Events;

namespace RPGFramework.Environment.Tests
{
    /// <summary>
    /// Proves that <see cref="EnvironmentEffect"/> raises <see cref="EnvironmentEffectAppliedEvent"/> whenever effects
    /// are applied, with the effects now in force, and that all environment events can travel on a real <see cref="EventBus"/>.
    /// </summary>
    [TestFixture]
    public class EnvironmentEventTests
    {
        [Test]
        public void UpdatingEffects_RaisesEnvironmentEffectApplied()
        {
            var effect = new EnvironmentEffect();
            var applied = new List<EnvironmentEffectAppliedEvent>();
            effect.OnEffectApplied += applied.Add;

            effect.UpdateEffects(new EnvironmentData { Weather = WeatherType.Blizzard, Temperature = -15f });

            Assert.That(applied, Has.Count.EqualTo(1));
            Assert.That(applied[0].EnvironmentData.Weather, Is.EqualTo(WeatherType.Blizzard));
            Assert.That(applied[0].EnvironmentData.Temperature, Is.EqualTo(-15f));
            Assert.That(applied[0].WeatherEffects.IsHarsh, Is.True);
            Assert.That(applied[0].IsHarsh, Is.True);
            Assert.That(applied[0].EnvironmentalDamage, Is.EqualTo(WeatherEffectData.Blizzard.DamagePerSecond));

            effect.UpdateEffects(new EnvironmentData { Weather = WeatherType.Blizzard, Temperature = -16f });
            Assert.That(applied, Has.Count.EqualTo(1), "still a blizzard");
            effect.UpdateEffects(new EnvironmentData { Weather = WeatherType.Rain });
            Assert.That(applied, Has.Count.EqualTo(2));
            Assert.That(applied[1].IsHarsh, Is.False);
        }

        [Test]
        public void ManagerUpdates_RaiseEnvironmentEffectApplied_OnlyWhenTheWeatherChanges()
        {
            var effect = new EnvironmentEffect();
            var manager = new EnvironmentManager(new WeatherSystem(), new TimeSystem(), new SeasonSystem(), effect);
            var applied = new List<EnvironmentEffectAppliedEvent>();
            effect.OnEffectApplied += applied.Add;

            manager.Update(1f);
            manager.Update(1f);
            manager.Update(1f);

            Assert.That(applied, Has.Count.EqualTo(1), "time passes every frame; the effects did not change");
            Assert.That(applied[0].IsHarsh, Is.False);
            Assert.That(applied[0].EnvironmentalDamage, Is.EqualTo(0f));
        }

        [Test]
        public void EnvironmentEvents_CanBeForwardedToEventBus()
        {
            using var bus = new EventBus();
            var received = new List<object>();
            bus.Subscribe<EnvironmentEffectAppliedEvent>(e => received.Add(e));
            bus.Subscribe<WeatherChangedEvent>(e => received.Add(e));
            bus.Subscribe<TimeChangedEvent>(e => received.Add(e));
            bus.Subscribe<SeasonChangedEvent>(e => received.Add(e));

            var effect = new EnvironmentEffect();
            var weather = new WeatherSystem();
            effect.OnEffectApplied += bus.Publish;
            weather.OnWeatherChanged += bus.Publish;

            effect.UpdateEffects(EnvironmentData.Default);
            weather.ChangeWeather(WeatherType.Rain, 0f);
            bus.Publish(new TimeChangedEvent(TimeOfDay.Dawn, TimeOfDay.Morning, 7, 0, 1));
            bus.Publish(new SeasonChangedEvent(Season.Spring, Season.Summer, 90));

            Assert.That(received, Has.Count.EqualTo(4));
            Assert.That(received[0], Is.InstanceOf<EnvironmentEffectAppliedEvent>());
            Assert.That(((WeatherChangedEvent)received[1]).NewWeather, Is.EqualTo(WeatherType.Rain));
        }
    }
}
