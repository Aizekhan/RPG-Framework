using System.Collections.Generic;
using RPGFramework.Localization;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A line of the kit's event log: its text to translate (no key for a line a game wrote as plain text) and the
    /// line in English.
    /// </summary>
    public readonly struct LogLine
    {
        public LogLine(LocalizedText text, string english)
        {
            Text = text;
            English = english ?? "";
        }

        public LocalizedText Text { get; }
        public string English { get; }

        /// <summary>
        /// The line in the localization's language; English without one, or for a plain line.
        /// </summary>
        public string In(ILocalization localization) =>
            localization == null || Text.IsEmpty ? English : localization.Text(Text, KitLog.Keys.TryGetValue(Text.Key, out var english) ? english : English);

        public override string ToString() => English;
    }

    /// <summary>
    /// The kit's event log texts by key (<c>kit.log.*</c>), in English, for a game's string tables. Names of
    /// players, quests and items are put in as the game's content gives them.
    /// </summary>
    public static class KitLog
    {
        public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
        {
            ["kit.log.enters"] = "{name} enters the world",
            ["kit.log.newQuest"] = "New quest: {quest}",
            ["kit.log.joins"] = "{name} joins the party",
            ["kit.log.leaves"] = "{name} leaves the party",
            ["kit.log.wave"] = "Wave {wave}: {count} enemies",
            ["kit.log.welcomeBack"] = "Welcome back: level {level}, {count} items",
            ["kit.log.brings"] = "{name} brings level {level}, {count} items",
            ["kit.log.revives"] = "{reviver} brings {name} back",
            ["kit.log.falls"] = "{name} falls",
            ["kit.log.slain"] = "{name} slain",
            ["kit.log.progress"] = "{quest}: {done}/{required}",
            ["kit.log.questDone"] = "Quest complete: {quest}",
            ["kit.log.memberLevel"] = "{name} reaches level {level}",
            ["kit.log.levelUp"] = "Level up! Level {level}",
            ["kit.log.item"] = "+{count} {item}",
            ["kit.log.backpackFull"] = "Backpack is full"
        };

        // English without a game's tables: the keys' own texts, values put in the same way
        internal static readonly ILocalization English = new LocalizationService(new[] { new StringTable("en", "English") });

        internal static LogLine Line(string key, params (string Name, object Value)[] args)
        {
            var text = new LocalizedText(key, args);
            return new LogLine(text, English.Text(text, Keys[key]));
        }
    }
}
