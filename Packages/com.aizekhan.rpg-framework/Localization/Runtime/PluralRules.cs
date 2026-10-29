using System;

namespace RPGFramework.Localization
{
    /// <summary>
    /// The plural form a number takes, as Unicode CLDR names them. A table's text for a key in a form lives under
    /// <c>key.one</c>, <c>key.few</c>, <c>key.many</c>, <c>key.other</c>.
    /// </summary>
    public enum PluralForm
    {
        Other,
        One,
        Few,
        Many
    }

    /// <summary>
    /// Which plural form a number takes in a language: English has one (1) and other; Ukrainian, Russian and
    /// Belarusian have one (1, 21, 31...), few (2-4, 22-24...) and many (5-20, 25-30...); Polish has one only for 1.
    /// Languages not listed here use the English rule; a game adds its own with <see cref="Set"/>.
    /// </summary>
    public static class PluralRules
    {
        private static readonly System.Collections.Generic.Dictionary<string, Func<long, PluralForm>> Rules =
            new System.Collections.Generic.Dictionary<string, Func<long, PluralForm>>(StringComparer.OrdinalIgnoreCase)
            {
                ["uk"] = EastSlavic,
                ["ru"] = EastSlavic,
                ["be"] = EastSlavic,
                ["pl"] = Polish,
                ["cs"] = Czech,
                ["sk"] = Czech,
                ["ja"] = _ => PluralForm.Other,
                ["zh"] = _ => PluralForm.Other,
                ["ko"] = _ => PluralForm.Other
            };

        public static PluralForm FormOf(string language, long count)
        {
            long n = Math.Abs(count);
            return Rules.TryGetValue(BaseOf(language), out var rule) ? rule(n) : (n == 1 ? PluralForm.One : PluralForm.Other);
        }

        /// <summary>
        /// Sets the rule of a language ("uk", or a language with its region, which falls back to the base).
        /// </summary>
        public static void Set(string language, Func<long, PluralForm> rule)
        {
            if (string.IsNullOrEmpty(language))
                throw new ArgumentException("A language is required", nameof(language));
            Rules[language] = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        public static string Suffix(PluralForm form)
        {
            switch (form)
            {
                case PluralForm.One: return "one";
                case PluralForm.Few: return "few";
                case PluralForm.Many: return "many";
                default: return "other";
            }
        }

        private static string BaseOf(string language)
        {
            language = language ?? "";
            if (Rules.ContainsKey(language))
                return language;
            int dash = language.IndexOfAny(new[] { '-', '_' });
            return dash > 0 ? language.Substring(0, dash) : language;
        }

        private static PluralForm EastSlavic(long n)
        {
            long ten = n % 10, hundred = n % 100;
            if (ten == 1 && hundred != 11)
                return PluralForm.One;
            if (ten >= 2 && ten <= 4 && (hundred < 12 || hundred > 14))
                return PluralForm.Few;
            return PluralForm.Many;
        }

        private static PluralForm Polish(long n)
        {
            if (n == 1)
                return PluralForm.One;
            long ten = n % 10, hundred = n % 100;
            return ten >= 2 && ten <= 4 && (hundred < 12 || hundred > 14) ? PluralForm.Few : PluralForm.Many;
        }

        private static PluralForm Czech(long n) => n == 1 ? PluralForm.One : n >= 2 && n <= 4 ? PluralForm.Few : PluralForm.Other;
    }
}
