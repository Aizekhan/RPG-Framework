using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RPGFramework.Events;

namespace RPGFramework.Localization
{
    /// <summary>
    /// <see cref="ILocalization"/> over string tables. It starts in the kept language, else the system's when there is a
    /// table for it, else the default.
    /// </summary>
    public sealed class LocalizationService : ILocalization
    {
        private const int MaxMissing = 1024;

        private readonly Dictionary<string, StringTable> _tables = new Dictionary<string, StringTable>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _languages = new List<string>();
        private readonly ILanguageStore _store;
        private readonly IEventPublisher _events;
        private readonly HashSet<(string Language, string Key)> _missing = new HashSet<(string Language, string Key)>();
        private StringTable _current;
        private StringTable _default;
        private CultureInfo _culture = CultureInfo.InvariantCulture;

        /// <param name="tables">One per language.</param>
        /// <param name="defaultLanguage">Where missing texts come from; null: the first table's language.</param>
        /// <param name="store">Keeps the player's choice; null keeps it only while the game runs.</param>
        /// <param name="systemLanguage">The device's language ("uk-UA"), used on the first start.</param>
        public LocalizationService(IEnumerable<StringTable> tables, string defaultLanguage = null, ILanguageStore store = null,
            string systemLanguage = null, IEventPublisher events = null)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            foreach (var table in tables.Where(t => t != null))
            {
                if (_tables.TryGetValue(table.Language, out var existing))
                {
                    // Two tables of one language (the game's and a module's): one table of both
                    foreach (string key in table.Keys)
                    {
                        table.TryGet(key, out string text);
                        existing.Set(key, text);
                    }
                    continue;
                }
                _tables[table.Language] = table;
                _languages.Add(table.Language);
            }
            if (_tables.Count == 0)
                _tables["en"] = new StringTable("en", "English");
            if (_languages.Count == 0)
                _languages.Add("en");

            _default = Find(defaultLanguage) ?? _tables[_languages[0]];
            DefaultLanguage = _default.Language;
            _languages.Remove(DefaultLanguage);
            _languages.Insert(0, DefaultLanguage);
            _store = store ?? new MemoryLanguageStore();
            _events = events;
            _current = Find(_store.Load()) ?? Find(systemLanguage) ?? _default;
            _culture = CultureOf(_current.Language);
        }

        public string Language => _current.Language;
        public string DefaultLanguage { get; }
        public IReadOnlyList<string> Languages => _languages;
        public IReadOnlyCollection<string> MissingKeys => _missing.Select(m => $"{m.Language}: {m.Key}").ToList();

        public event Action<string> LanguageChanged;

        public string DisplayNameOf(string language) => Find(language)?.DisplayName ?? language ?? "";

        public bool SetLanguage(string language)
        {
            var table = Find(language);
            if (table == null)
                return false;
            _store.Save(table.Language);
            if (table == _current)
                return true;
            string previous = _current.Language;
            _current = table;
            _culture = CultureOf(table.Language);
            LanguageChanged?.Invoke(table.Language);
            _events?.Publish(new LanguageChangedEvent(previous, table.Language));
            return true;
        }

        public string Text(string key) => Lookup(key, null) ?? key ?? "";

        public string Text(string key, string fallback) => Lookup(key, null) ?? fallback ?? key ?? "";

        public string Text(LocalizedText text) => Text(text, null);

        public string Text(LocalizedText text, string fallback)
        {
            if (text.IsEmpty)
                return fallback == null ? "" : Fill(fallback, text.Args);
            object count = text.Arg("count");
            PluralForm? form = IsNumber(count) ? PluralRules.FormOf(Language, Convert.ToInt64(count, CultureInfo.InvariantCulture)) : (PluralForm?)null;
            string template = Lookup(text.Key, form) ?? fallback ?? text.Key;
            return Fill(template, text.Args);
        }

        public string Format(string key, params (string Name, object Value)[] args) => Text(new LocalizedText(key, args));

        public string Plural(string key, long count, params (string Name, object Value)[] args)
        {
            var all = new List<(string, object)> { ("count", count) };
            if (args != null)
                all.AddRange(args.Where(a => a.Name != "count"));
            return Text(new LocalizedText(key, all.ToArray()));
        }

        public bool Has(string key) => Find(_current, key, null) != null || Find(_default, key, null) != null || HasPlural(key);

        /// <summary>
        /// The table of a language (to add texts at run time, as a module does).
        /// </summary>
        public StringTable Table(string language) => Find(language);

        private bool HasPlural(string key) =>
            Enum.GetValues(typeof(PluralForm)).Cast<PluralForm>()
                .Any(form => _current.Has(key + "." + PluralRules.Suffix(form)) || _default.Has(key + "." + PluralRules.Suffix(form)));

        private string Lookup(string key, PluralForm? form)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            string text = Find(_current, key, form);
            if (text != null)
                return text;
            // A pair, not a joined string: the kit asks every frame for keys a game may never translate
            if (_missing.Count < MaxMissing)
                _missing.Add((Language, key));
            return _current == _default ? null : Find(_default, key, form);
        }

        // The plural form, then "other", then the key itself
        private static string Find(StringTable table, string key, PluralForm? form)
        {
            if (form.HasValue)
            {
                if (table.TryGet(key + "." + PluralRules.Suffix(form.Value), out string plural))
                    return plural;
                if (table.TryGet(key + ".other", out string other))
                    return other;
            }
            return table.TryGet(key, out string text) ? text : null;
        }

        private StringTable Find(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return null;
            language = language.Trim();
            if (_tables.TryGetValue(language, out var table))
                return table;
            int dash = language.IndexOfAny(new[] { '-', '_' });
            if (dash > 0 && _tables.TryGetValue(language.Substring(0, dash), out table))
                return table;
            // "pt" finds "pt-BR"
            return _tables.Values.FirstOrDefault(t => t.Language.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));
        }

        private string Fill(string template, IReadOnlyList<KeyValuePair<string, object>> args)
        {
            if (template.IndexOf('{') < 0 && template.IndexOf('}') < 0)
                return template;
            var result = new StringBuilder(template.Length + 16);
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                if ((c == '{' || c == '}') && i + 1 < template.Length && template[i + 1] == c)
                {
                    result.Append(c);
                    i++;
                    continue;
                }
                int close = c == '{' ? template.IndexOf('}', i + 1) : -1;
                if (close < 0)
                {
                    result.Append(c);
                    continue;
                }
                string inside = template.Substring(i + 1, close - i - 1);
                int colon = inside.IndexOf(':');
                string name = colon < 0 ? inside : inside.Substring(0, colon);
                string format = colon < 0 ? null : inside.Substring(colon + 1);
                if (!TryArg(args, name, out object value))
                {
                    // An unknown name stays as it is, so a translator sees it
                    result.Append(template, i, close - i + 1);
                }
                else
                {
                    result.Append(Show(value, format));
                }
                i = close;
            }
            return result.ToString();
        }

        private string Show(object value, string format)
        {
            switch (value)
            {
                case null:
                    return "";
                case LocalizedText nested:
                    return Text(nested);
                case IFormattable formattable:
                    return formattable.ToString(format, _culture);
                default:
                    return value.ToString();
            }
        }

        private static bool TryArg(IReadOnlyList<KeyValuePair<string, object>> args, string name, out object value)
        {
            foreach (var arg in args)
            {
                if (arg.Key == name)
                {
                    value = arg.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        private static bool IsNumber(object value) =>
            value is int || value is long || value is short || value is byte || value is uint || value is ulong || value is ushort;

        private static CultureInfo CultureOf(string language)
        {
            try
            {
                return CultureInfo.GetCultureInfo(language);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
