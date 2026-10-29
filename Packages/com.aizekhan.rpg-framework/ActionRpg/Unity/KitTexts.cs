using System.Collections.Generic;
using System.Linq;
using RPGFramework.Localization;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The kit's own texts: the keys its HUD and co-op panel read, in English. A game translates them in its string
    /// tables (<see cref="Table"/> is a start, or <see cref="LocalizationCsv"/> for translators).
    /// </summary>
    public static class KitTexts
    {
        /// <summary>
        /// English without a game's tables: the keys' own texts, values put in the same way.
        /// </summary>
        internal static readonly ILocalization English = new LocalizationService(new[] { new StringTable("en", "English") });

        public static IEnumerable<KeyValuePair<string, string>> All => ActionRpgHud.Keys.Concat(CoopPanel.Keys).Concat(KitLog.Keys);

        /// <summary>
        /// The kit's texts as an English table, to add to the game's or to write out for translators.
        /// </summary>
        public static StringTable Table(string language = "en", string displayName = "English")
        {
            var table = new StringTable(language, displayName);
            foreach (var text in All)
                table.Set(text.Key, text.Value);
            return table;
        }
    }
}
