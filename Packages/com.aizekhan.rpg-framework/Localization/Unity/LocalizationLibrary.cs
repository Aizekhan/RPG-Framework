using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPGFramework.Localization.Unity
{
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
