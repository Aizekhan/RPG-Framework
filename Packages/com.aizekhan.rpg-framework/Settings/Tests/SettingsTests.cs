using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Audio;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Localization;
using RPGFramework.Logging;
using RPGFramework.Settings.Unity;
using Logger = RPGFramework.Logging.Logger;

namespace RPGFramework.Settings.Tests
{
    /// <summary>
    /// Settings: typed options kept between starts (a default is not kept), steps, a bad kept value ignored, changes
    /// announced; display options applied only when they differ from the screen; volumes and the language shown but
    /// kept by their services; the page's rows; the module.
    /// </summary>
    public sealed class SettingsTests
    {
        private sealed class FakeDisplay : IDisplayOutput
        {
            public readonly List<DisplayState> Applied = new List<DisplayState>();
            public IReadOnlyList<(int Width, int Height)> Resolutions { get; set; } = new[] { (1280, 720), (1920, 1080), (1920, 1080), (2560, 1440) };
            public IReadOnlyList<string> QualityLevels { get; set; } = new[] { "Low", "Medium", "High" };
            public DisplayState Current { get; set; } = new DisplayState(1920, 1080, DisplayWindowMode.Borderless, 2, true, 0);
            public bool CanChangeResolution { get; set; } = true;
            public void Apply(DisplayState state) => Applied.Add(state);
        }

        [Test]
        public void Settings_AreKept_StepAndAnnounceChanges()
        {
            var store = new MemorySettingsStore();
            using (var bus = new EventBus())
            {
                var published = new List<SettingChangedEvent>();
                bus.Subscribe<SettingChangedEvent>(e => published.Add(e));
                var settings = new SettingsService(store, bus);
                var shake = settings.Add(new SliderSetting("game.shake", "Camera shake", 0f, 2f, 0.25f, 1f, "Game"));
                var subtitles = settings.Add(new ToggleSetting("game.subtitles", "Subtitles", false, "Game"));
                var difficulty = settings.Add(new ChoiceSetting("game.difficulty", "Difficulty", new[] { "Easy", "Normal", "Hard" }, 1, "Game"));
                var changed = new List<string>();
                settings.Changed += s => changed.Add(s.Id);

                shake.Step(+1);
                Assert.That(shake.Value, Is.EqualTo(1.25f));
                shake.Value = 1.6f;
                Assert.That(shake.Value, Is.EqualTo(1.5f), "onto a step");
                shake.Value = 9f;
                Assert.That(shake.Value, Is.EqualTo(2f), "inside the ends");
                subtitles.Step(+1);
                difficulty.Step(+1);
                difficulty.Step(+1);
                Assert.That(difficulty.Value, Is.EqualTo("Easy"), "a choice wraps around");

                Assert.That(changed, Is.EqualTo(new[] { "game.shake", "game.shake", "game.shake", "game.subtitles", "game.difficulty", "game.difficulty" }));
                Assert.That(published.Last().Id, Is.EqualTo("game.difficulty"));
                Assert.That(published.Last().Value, Is.EqualTo("Easy"));
                Assert.That(store.Values["game.shake"], Is.EqualTo("2"));
                Assert.That(store.Flushes, Is.EqualTo(6));

                difficulty.Select("normal");
                Assert.That(store.Values.ContainsKey("game.difficulty"), Is.False, "back on the default: not kept");
                Assert.That(settings.Groups, Is.EqualTo(new[] { "Game" }));
            }

            store.Values["game.subtitles"] = "maybe";
            var nextStart = new SettingsService(store);
            Assert.That(nextStart.Add(new SliderSetting("game.shake", "Camera shake", 0f, 2f, 0.25f, 1f)).Value, Is.EqualTo(2f));
            Assert.That(nextStart.Add(new ToggleSetting("game.subtitles", "Subtitles", false)).Value, Is.False, "a bad kept value is ignored");
            Assert.That(nextStart.Toggle("game.subtitles", true), Is.False);
            Assert.That(nextStart.Slider("game.none", 3f), Is.EqualTo(3f));

            nextStart.ResetToDefaults();
            Assert.That(nextStart.Slider("game.shake"), Is.EqualTo(1f));
            Assert.That(store.Values.ContainsKey("game.shake"), Is.False);
        }

        [Test]
        public void Display_StartsAsTheScreenIs_AndAppliesChanges()
        {
            var display = new FakeDisplay();
            var store = new MemorySettingsStore();
            var settings = new SettingsService(store);
            var options = DisplaySettings.Add(settings, display);

            Assert.That(display.Applied, Is.Empty, "a first start changes nothing");
            Assert.That(options.Resolution.Choices, Is.EqualTo(new[] { "2560 x 1440", "1920 x 1080", "1280 x 720" }));
            Assert.That(options.Resolution.Value, Is.EqualTo("1920 x 1080"));
            Assert.That(options.Quality.Value, Is.EqualTo("High"));
            Assert.That(options.FrameRate.Value, Is.EqualTo("Unlimited"));
            Assert.That(settings.InGroup(DisplaySettings.Group).Count(), Is.EqualTo(5));

            options.WindowMode.Select("Windowed");
            options.Resolution.Select("1280 x 720");
            Assert.That(display.Applied.Last(), Is.EqualTo(new DisplayState(1280, 720, DisplayWindowMode.Windowed, 2, true, 0)));

            var next = new FakeDisplay();
            DisplaySettings.Add(new SettingsService(store), next);
            Assert.That(next.Applied.Single(), Is.EqualTo(new DisplayState(1280, 720, DisplayWindowMode.Windowed, 2, true, 0)),
                "the kept settings, applied on the next start");

            var phone = new FakeDisplay { CanChangeResolution = false };
            var phoneOptions = DisplaySettings.Add(new SettingsService(), phone);
            Assert.That(phoneOptions.Resolution, Is.Null);
            Assert.That(phoneOptions.Quality, Is.Not.Null);
        }

        [Test]
        public void VolumesAndLanguage_AreShown_ButKeptByTheirServices()
        {
            var store = new MemorySettingsStore();
            var settings = new SettingsService(store);
            using (var audio = new AudioService(new RecordingAudioOutput(), store: new MemoryVolumeStore()))
            {
                var volumes = settings.AddVolumes(audio, AudioChannels.Master, AudioChannels.Music);
                volumes[1].Step(-1);
                Assert.That(audio.GetVolume(AudioChannels.Music), Is.EqualTo(0.95f).Within(1e-4f));
                Assert.That(store.Values, Is.Empty, "the audio service keeps volumes");
            }

            var localization = new LocalizationService(new[] { new StringTable("en", "English"), new StringTable("uk", "Українська") }, "en");
            var language = settings.AddLanguage(localization);
            Assert.That(language.Choices, Is.EqualTo(new[] { "English", "Українська" }));
            language.Step(+1);
            Assert.That(localization.Language, Is.EqualTo("uk"));
            Assert.That(store.Values, Is.Empty);
            Assert.That(settings.AddLanguage(new LocalizationService(new[] { new StringTable("en") })), Is.Null, "one language: no choice");
        }

        [Test]
        public void ThePage_HasARowPerSetting_InTheLanguage()
        {
            var settings = new SettingsService();
            settings.Add(new ToggleSetting("game.subtitles", "Subtitles", true, "Game"));
            settings.Add(new SliderSetting("audio.master", "Master volume", 0f, 1f, 0.1f, 0.8f, "Audio"));

            var rows = SettingsRows.For(settings);
            Assert.That(rows.Select(r => r.Text()), Is.EqualTo(new[] { "<b>Game</b>", "Subtitles   <b>On</b>", "<b>Audio</b>", "Master volume   <b>80%</b>", "" }));
            rows[1].Buttons[1].Click();
            Assert.That(settings.Toggle("game.subtitles"), Is.False);
            rows.Last().Buttons[0].Click();
            Assert.That(settings.Toggle("game.subtitles"), Is.True, "Defaults");

            var ukrainian = new StringTable("uk").Set("settings.game.subtitles", "Субтитри").Set("settings.on", "Увімк.")
                .Set("settings.group.Game", "Гра");
            var localization = new LocalizationService(new[] { ukrainian });
            var translated = SettingsRows.For(settings, localization, "Game");
            Assert.That(translated[0].Text(), Is.EqualTo("Субтитри   <b>Увімк.</b>"), "one group: no heading");
        }

        [Test]
        public void TheModule_RegistersSettings_WithTheDisplay()
        {
            var display = new FakeDisplay();
            var store = new MemorySettingsStore();
            using (var logger = new Logger(LogSeverity.Warning, new MemoryLogSink()))
            using (var host = new GameHost(logger))
            {
                var module = new SettingsModule(store, display);
                host.Start(b => b.UseModule(module));
                var settings = host.Container.Resolve<ISettingsService>();

                Assert.That(settings.Find(DisplaySettings.VSyncId), Is.Not.Null);
                Assert.That(module.Display, Is.Not.Null);
                settings.Find<ToggleSetting>(DisplaySettings.VSyncId).Value = false;
                Assert.That(display.Applied.Single().VSync, Is.False);
            }
        }
    }
}
