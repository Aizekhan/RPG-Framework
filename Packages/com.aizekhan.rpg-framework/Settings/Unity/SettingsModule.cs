using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Audio;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Localization;
using UnityEngine;

namespace RPGFramework.Settings.Unity
{
    /// <summary>
    /// Keeps settings in Unity's PlayerPrefs ("RPGFramework.Settings.&lt;id&gt;").
    /// </summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public const string Prefix = "RPGFramework.Settings.";

        public bool TryLoad(string id, out string value)
        {
            string key = Prefix + id;
            value = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            return value != null;
        }

        public void Save(string id, string value) => PlayerPrefs.SetString(Prefix + id, value ?? "");

        public void Delete(string id) => PlayerPrefs.DeleteKey(Prefix + id);

        public void Flush() => PlayerPrefs.Save();
    }

    /// <summary>
    /// Unity's screen for <see cref="DisplaySettings"/>: <c>Screen</c>, <c>QualitySettings</c>,
    /// <c>Application.targetFrameRate</c>. The screen size changes only in a standalone player.
    /// </summary>
    public sealed class UnityDisplayOutput : IDisplayOutput
    {
        public IReadOnlyList<(int Width, int Height)> Resolutions =>
            Screen.resolutions.Select(r => (r.width, r.height)).ToList();

        public IReadOnlyList<string> QualityLevels => QualitySettings.names;

        public DisplayState Current => new DisplayState(Screen.width, Screen.height, ModeOf(Screen.fullScreenMode),
            QualitySettings.GetQualityLevel(), QualitySettings.vSyncCount > 0, Math.Max(0, Application.targetFrameRate));

        public bool CanChangeResolution => Application.isPlaying && !Application.isEditor && !Application.isMobilePlatform
            && !Application.isConsolePlatform;

        public void Apply(DisplayState state)
        {
            if (CanChangeResolution && state.Width > 0 && state.Height > 0)
                Screen.SetResolution(state.Width, state.Height, ModeOf(state.WindowMode));
            if (state.Quality >= 0 && state.Quality < QualitySettings.names.Length && state.Quality != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(state.Quality, true);
            QualitySettings.vSyncCount = state.VSync ? 1 : 0;
            Application.targetFrameRate = state.FrameRate > 0 ? state.FrameRate : -1;
        }

        public static DisplayWindowMode ModeOf(FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.ExclusiveFullScreen: return DisplayWindowMode.FullScreen;
                case FullScreenMode.FullScreenWindow: return DisplayWindowMode.Borderless;
                default: return DisplayWindowMode.Windowed;
            }
        }

        public static FullScreenMode ModeOf(DisplayWindowMode mode)
        {
            switch (mode)
            {
                case DisplayWindowMode.FullScreen: return FullScreenMode.ExclusiveFullScreen;
                case DisplayWindowMode.Borderless: return FullScreenMode.FullScreenWindow;
                default: return FullScreenMode.Windowed;
            }
        }
    }

    /// <summary>
    /// Settings the framework's own services keep: volumes (<see cref="IAudioService"/>) and the language
    /// (<see cref="ILocalization"/>), shown on the settings page but stored by those services.
    /// </summary>
    public static class SettingsBridges
    {
        public const string AudioGroup = "Audio";
        public const string LanguageId = "game.language";

        /// <summary>
        /// A 0..1 slider per channel ("audio.master", "audio.music"...), steps of 0.05.
        /// </summary>
        public static IReadOnlyList<SliderSetting> AddVolumes(this ISettingsService settings, IAudioService audio, params string[] channels)
        {
            if (settings == null || audio == null)
                return Array.Empty<SliderSetting>();
            if (channels == null || channels.Length == 0)
                channels = new[] { AudioChannels.Master, AudioChannels.Music, AudioChannels.Effects, AudioChannels.Interface };
            var added = new List<SliderSetting>();
            foreach (string channel in channels.Where(c => !string.IsNullOrEmpty(c)))
            {
                added.Add(settings.Add(new SliderSetting("audio." + channel, LabelOf(channel), 0f, 1f, 0.05f,
                    () => audio.GetVolume(channel), v => audio.SetVolume(channel, v), 1f, AudioGroup)));
            }
            return added;
        }

        /// <summary>
        /// The language, by each language's own name; none with one language only.
        /// </summary>
        public static ChoiceSetting AddLanguage(this ISettingsService settings, ILocalization localization, string group = "Game")
        {
            if (settings == null || localization == null || localization.Languages.Count < 2)
                return null;
            var languages = localization.Languages.ToList();
            return settings.Add(new ChoiceSetting(LanguageId, "Language", languages.Select(l => localization.DisplayNameOf(l)),
                () => Math.Max(0, languages.FindIndex(l => string.Equals(l, localization.Language, StringComparison.OrdinalIgnoreCase))),
                i => localization.SetLanguage(languages[i]), 0, group));
        }

        private static string LabelOf(string channel)
        {
            switch (channel)
            {
                case AudioChannels.Master: return "Master volume";
                case AudioChannels.Music: return "Music";
                case AudioChannels.Effects: return "Effects";
                case AudioChannels.Interface: return "Interface";
                default: return char.ToUpperInvariant(channel[0]) + channel.Substring(1);
            }
        }
    }

    /// <summary>
    /// Registers <see cref="ISettingsService"/> kept in PlayerPrefs, with the display options (<see cref="DisplaySettings"/>)
    /// and, when those modules are there, the volumes and the language. The game adds its own options (camera shake,
    /// subtitles) to the service. A service registered before this module configures is kept.
    /// </summary>
    public sealed class SettingsModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.Settings";

        private readonly ISettingsStore _store;
        private readonly IDisplayOutput _display;
        private readonly bool _withDisplay;

        /// <param name="store">Null: PlayerPrefs.</param>
        /// <param name="display">Null: Unity's screen.</param>
        /// <param name="withDisplay">Add the display options.</param>
        public SettingsModule(ISettingsStore store = null, IDisplayOutput display = null, bool withDisplay = true)
        {
            _store = store;
            _display = display;
            _withDisplay = withDisplay;
        }

        public override string Name => ModuleName;

        /// <summary>
        /// The display options once the service was made (null before, or without them).
        /// </summary>
        public DisplaySettings Display { get; private set; }

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<ISettingsService>())
                Container.RegisterFactory<ISettingsService>(c => Create(c));
        }

        private ISettingsService Create(RPGFramework.DI.IDIContainer container)
        {
            var settings = new SettingsService(_store ?? new PlayerPrefsSettingsStore(),
                container.IsRegistered<IEventBus>() ? container.Resolve<IEventBus>() : null);
            if (_withDisplay)
                Display = DisplaySettings.Add(settings, _display ?? new UnityDisplayOutput());
            if (container.IsRegistered<IAudioService>())
                settings.AddVolumes(container.Resolve<IAudioService>());
            if (container.IsRegistered<ILocalization>())
                settings.AddLanguage(container.Resolve<ILocalization>());
            return settings;
        }
    }
}
