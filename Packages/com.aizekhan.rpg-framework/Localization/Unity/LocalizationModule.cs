using System;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using UnityEngine;

namespace RPGFramework.Localization.Unity
{
    /// <summary>
    /// Keeps the player's language in Unity's PlayerPrefs (settings, not the save).
    /// </summary>
    public sealed class PlayerPrefsLanguageStore : ILanguageStore
    {
        public const string Key = "RPGFramework.Localization.Language";

        public string Load() => PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;

        public void Save(string language)
        {
            PlayerPrefs.SetString(Key, language ?? "");
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Unity points for <see cref="ILocalization"/>.
    /// </summary>
    public static class LocalizationUnity
    {
        /// <summary>
        /// The device's language as a code ("uk"), or null when Unity does not know it.
        /// </summary>
        public static string DeviceLanguage => CodeOf(Application.systemLanguage);

        public static string CodeOf(SystemLanguage language)
        {
            switch (language)
            {
                case UnityEngine.SystemLanguage.Afrikaans: return "af";
                case UnityEngine.SystemLanguage.Arabic: return "ar";
                case UnityEngine.SystemLanguage.Basque: return "eu";
                case UnityEngine.SystemLanguage.Belarusian: return "be";
                case UnityEngine.SystemLanguage.Bulgarian: return "bg";
                case UnityEngine.SystemLanguage.Catalan: return "ca";
                case UnityEngine.SystemLanguage.Chinese: return "zh";
                case UnityEngine.SystemLanguage.ChineseSimplified: return "zh-Hans";
                case UnityEngine.SystemLanguage.ChineseTraditional: return "zh-Hant";
                case UnityEngine.SystemLanguage.Czech: return "cs";
                case UnityEngine.SystemLanguage.Danish: return "da";
                case UnityEngine.SystemLanguage.Dutch: return "nl";
                case UnityEngine.SystemLanguage.English: return "en";
                case UnityEngine.SystemLanguage.Estonian: return "et";
                case UnityEngine.SystemLanguage.Finnish: return "fi";
                case UnityEngine.SystemLanguage.French: return "fr";
                case UnityEngine.SystemLanguage.German: return "de";
                case UnityEngine.SystemLanguage.Greek: return "el";
                case UnityEngine.SystemLanguage.Hebrew: return "he";
                case UnityEngine.SystemLanguage.Hungarian: return "hu";
                case UnityEngine.SystemLanguage.Icelandic: return "is";
                case UnityEngine.SystemLanguage.Indonesian: return "id";
                case UnityEngine.SystemLanguage.Italian: return "it";
                case UnityEngine.SystemLanguage.Japanese: return "ja";
                case UnityEngine.SystemLanguage.Korean: return "ko";
                case UnityEngine.SystemLanguage.Latvian: return "lv";
                case UnityEngine.SystemLanguage.Lithuanian: return "lt";
                case UnityEngine.SystemLanguage.Norwegian: return "no";
                case UnityEngine.SystemLanguage.Polish: return "pl";
                case UnityEngine.SystemLanguage.Portuguese: return "pt";
                case UnityEngine.SystemLanguage.Romanian: return "ro";
                case UnityEngine.SystemLanguage.Russian: return "ru";
                case UnityEngine.SystemLanguage.SerboCroatian: return "sh";
                case UnityEngine.SystemLanguage.Slovak: return "sk";
                case UnityEngine.SystemLanguage.Slovenian: return "sl";
                case UnityEngine.SystemLanguage.Spanish: return "es";
                case UnityEngine.SystemLanguage.Swedish: return "sv";
                case UnityEngine.SystemLanguage.Thai: return "th";
                case UnityEngine.SystemLanguage.Turkish: return "tr";
                case UnityEngine.SystemLanguage.Ukrainian: return "uk";
                case UnityEngine.SystemLanguage.Vietnamese: return "vi";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Registers <see cref="ILocalization"/> with the tables of a <see cref="LocalizationLibrary"/>, the language kept
    /// in PlayerPrefs and, on the first start, the device's language when there is a table for it. A language change
    /// is published on the host's bus as <see cref="LanguageChangedEvent"/>. A service registered before this module
    /// configures is kept.
    /// </summary>
    public sealed class LocalizationModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.Localization";

        private readonly LocalizationLibrary _library;
        private readonly ILanguageStore _store;
        private readonly string _systemLanguage;

        /// <param name="library">The game's languages; null: English only, every text its fallback.</param>
        /// <param name="store">Null: PlayerPrefs.</param>
        /// <param name="systemLanguage">Null: the device's (when the library uses it).</param>
        public LocalizationModule(LocalizationLibrary library, ILanguageStore store = null, string systemLanguage = null)
        {
            _library = library;
            _store = store;
            _systemLanguage = systemLanguage;
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<ILocalization>())
                Container.RegisterFactory<ILocalization>(c => Create(c.Resolve<IEventBus>()));
        }

        private ILocalization Create(IEventBus bus)
        {
            string system = _systemLanguage;
            if (system == null && (_library == null || _library.UseSystemLanguage))
                system = LocalizationUnity.DeviceLanguage;
            return new LocalizationService(
                _library != null ? _library.ToTables() : Array.Empty<StringTable>(),
                _library != null && !string.IsNullOrWhiteSpace(_library.DefaultLanguage) ? _library.DefaultLanguage : null,
                _store ?? new PlayerPrefsLanguageStore(),
                system,
                bus);
        }
    }
}
