using System;
using System.Collections.Generic;

namespace RPGFramework.Localization
{
    /// <summary>
    /// The texts of one language by key ("menu.continue" → "Продовжити"). Plural forms of a key live under
    /// <c>key.one</c>, <c>key.few</c>, <c>key.many</c>, <c>key.other</c> (<see cref="PluralRules"/>); named values go in
    /// braces: "{damage} damage", "{value:0.0}"; "{{" and "}}" are literal braces.
    /// </summary>
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <param name="language">A language code: "en", "uk", "pt-BR".</param>
        /// <param name="displayName">The language's own name, for a language menu ("Українська").</param>
        public StringTable(string language, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(language))
                throw new ArgumentException("A language code is required", nameof(language));
            Language = language.Trim();
            DisplayName = string.IsNullOrEmpty(displayName) ? Language : displayName;
        }

        public string Language { get; }
        public string DisplayName { get; }

        public IEnumerable<string> Keys => _texts.Keys;
        public int Count => _texts.Count;

        public StringTable Set(string key, string text)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("A key is required", nameof(key));
            _texts[key] = text ?? "";
            return this;
        }

        public bool Remove(string key) => key != null && _texts.Remove(key);

        public bool TryGet(string key, out string text)
        {
            text = null;
            return key != null && _texts.TryGetValue(key, out text);
        }

        public bool Has(string key) => key != null && _texts.ContainsKey(key);

        public override string ToString() => $"{DisplayName} ({Language}, {Count} texts)";
    }
}
