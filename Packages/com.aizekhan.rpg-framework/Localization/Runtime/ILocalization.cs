using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Localization
{
    /// <summary>
    /// The player's language and the game's texts in it. Keys missing in the language come from the default
    /// language's table, then show as the key itself (and are listed in <see cref="MissingKeys"/>).
    /// </summary>
    public interface ILocalization
    {
        /// <summary>
        /// The language texts are in now (a code: "en", "uk").
        /// </summary>
        string Language { get; }

        string DefaultLanguage { get; }

        /// <summary>
        /// The languages there are tables for, the default first.
        /// </summary>
        IReadOnlyList<string> Languages { get; }

        /// <summary>
        /// A language's own name ("Українська"), for a language menu.
        /// </summary>
        string DisplayNameOf(string language);

        /// <summary>
        /// Switches the language (kept for the next start); "uk-UA" picks "uk" when only that exists.
        /// </summary>
        /// <returns>False when there is no table for it.</returns>
        bool SetLanguage(string language);

        /// <summary>
        /// The language changed (the new one): views show their texts again.
        /// </summary>
        event Action<string> LanguageChanged;

        string Text(string key);

        /// <summary>
        /// The text of a key, or <paramref name="fallback"/> when no table has it (the kit's own texts: they read
        /// right in English until a game translates them).
        /// </summary>
        string Text(string key, string fallback);

        /// <summary>
        /// The text with its named values put in; a "count" value picks the plural form.
        /// </summary>
        string Text(LocalizedText text);

        /// <summary>
        /// <see cref="Text(LocalizedText)"/>, with <paramref name="fallback"/> as the template when no table has the key
        /// (its values are put in the same way).
        /// </summary>
        string Text(LocalizedText text, string fallback);

        /// <summary>
        /// <see cref="Text(LocalizedText)"/> of a key and its values.
        /// </summary>
        string Format(string key, params (string Name, object Value)[] args);

        /// <summary>
        /// The text in the form for <paramref name="count"/> (1 / 2-4 / 5+ in Ukrainian), with {count} put in.
        /// </summary>
        string Plural(string key, long count, params (string Name, object Value)[] args);

        /// <summary>
        /// A table of the language or the default one has the key (or a plural form of it).
        /// </summary>
        bool Has(string key);

        /// <summary>
        /// Keys asked for that the language's table does not have ("uk: menu.quit"), for a translator or a test.
        /// </summary>
        IReadOnlyCollection<string> MissingKeys { get; }
    }

    /// <summary>
    /// Where the player's language is kept between starts (settings, not the save).
    /// </summary>
    public interface ILanguageStore
    {
        /// <returns>The kept language, or null.</returns>
        string Load();

        void Save(string language);
    }

    /// <summary>
    /// The language kept only while the game runs (tests, servers).
    /// </summary>
    public sealed class MemoryLanguageStore : ILanguageStore
    {
        public string Language { get; set; }
        public string Load() => Language;
        public void Save(string language) => Language = language;
    }

    public readonly struct LanguageChangedEvent : IEvent
    {
        public LanguageChangedEvent(string previous, string language)
        {
            Previous = previous;
            Language = language;
        }

        public string Previous { get; }
        public string Language { get; }
    }
}
