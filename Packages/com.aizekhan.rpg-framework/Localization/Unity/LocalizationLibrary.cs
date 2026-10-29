using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPGFramework.Localization.Unity
{
    /// <summary>
    /// One text of a <see cref="StringTableAsset"/>.
    /// </summary>
    [Serializable]
    public sealed class LocalizedEntry
    {
        public string Key = "";
        [TextArea(1, 4)] public string Text = "";
    }

    /// <summary>
    /// The texts of one language. Plural forms of a key go under <c>key.one</c>, <c>key.few</c>, <c>key.many</c>,
    /// <c>key.other</c>; named values in braces ("{damage} damage"). Fill it by hand or from a CSV
    /// (<see cref="LocalizationCsv"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Localization/String Table", fileName = "StringTable")]
    public sealed class StringTableAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Language code: en, uk, pt-BR")] private string _language = "en";
        [SerializeField, Tooltip("The language's own name, for a language menu: English, Українська")] private string _displayName = "English";
        [SerializeField, Tooltip("A font with this language's letters; empty: the views' own")] private Font _font;
        [SerializeField] private List<LocalizedEntry> _entries = new List<LocalizedEntry>();

        public string Language => _language;
        public string DisplayName => _displayName;
        public Font Font => _font;
        public IReadOnlyList<LocalizedEntry> Entries => _entries;

        public StringTable ToTable()
        {
            var table = new StringTable(string.IsNullOrWhiteSpace(_language) ? "en" : _language, _displayName);
            foreach (var entry in _entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Key))
                    table.Set(entry.Key.Trim(), entry.Text);
            }
            return table;
        }

        /// <summary>
        /// Replaces the texts with a table's (the language and name stay unless empty here).
        /// </summary>
        public void Fill(StringTable table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(_language))
                _language = table.Language;
            if (string.IsNullOrWhiteSpace(_displayName))
                _displayName = table.DisplayName;
            _entries = table.Keys.OrderBy(k => k, StringComparer.Ordinal)
                .Select(key => new LocalizedEntry { Key = key, Text = table.TryGet(key, out string text) ? text : "" })
                .ToList();
        }

        public static StringTableAsset Create(StringTable table, Font font = null)
        {
            var asset = CreateInstance<StringTableAsset>();
            asset.name = "StringTable_" + table.Language;
            asset._language = table.Language;
            asset._displayName = table.DisplayName;
            asset._font = font;
            asset.Fill(table);
            return asset;
        }
    }

    /// <summary>
    /// The game's languages: a table each and the default one (missing texts come from it). Give it to
    /// <see cref="LocalizationModule"/> (or to the action RPG kit's host).
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Localization/Library", fileName = "LocalizationLibrary")]
    public sealed class LocalizationLibrary : ScriptableObject
    {
        [SerializeField] private List<StringTableAsset> _tables = new List<StringTableAsset>();
        [SerializeField, Tooltip("Where missing texts come from; empty: the first table's")] private string _defaultLanguage = "en";
        [SerializeField, Tooltip("Start in the device's language when there is a table for it (until the player picks one)")]
        private bool _useSystemLanguage = true;

        public IReadOnlyList<StringTableAsset> Tables => _tables;
        public string DefaultLanguage => _defaultLanguage;
        public bool UseSystemLanguage => _useSystemLanguage;

        /// <returns>The language's table asset (with its font), or null.</returns>
        public StringTableAsset Table(string language) =>
            _tables.FirstOrDefault(t => t != null && string.Equals(t.Language, language, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<StringTable> ToTables() => _tables.Where(t => t != null).Select(t => t.ToTable());

        public static LocalizationLibrary Create(IEnumerable<StringTableAsset> tables, string defaultLanguage = null, bool useSystemLanguage = true)
        {
            var library = CreateInstance<LocalizationLibrary>();
            library.name = "LocalizationLibrary";
            if (tables != null)
                library._tables.AddRange(tables.Where(t => t != null));
            library._defaultLanguage = defaultLanguage ?? "";
            library._useSystemLanguage = useSystemLanguage;
            return library;
        }
    }
}
