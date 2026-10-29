using System.Linq;
using NUnit.Framework;
using RPGFramework.Localization;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The kit's own texts: one English table of every key its HUD and co-op panel read, and a game's translation
    /// used in their place.
    /// </summary>
    public sealed class KitTextsTests
    {
        [Test]
        public void TheKitsTexts_AreOneTable_AndAGamesTranslationIsUsed()
        {
            var english = KitTexts.Table();
            Assert.That(english.Count, Is.EqualTo(KitTexts.All.Count()), "no key twice");
            Assert.That(english.Keys.All(k => k.StartsWith("kit.hud.") || k.StartsWith("kit.coop.") || k.StartsWith("kit.log.") || k.StartsWith("kit.menu.")), Is.True);

            var ukrainian = new StringTable("uk", "Українська").Set("kit.hud.wins", "{team} перемагає");
            var localization = new LocalizationService(new[] { english, ukrainian }, "en");
            var wins = LocalizedText.Of("kit.hud.wins", ("team", "Red"));
            Assert.That(localization.Text(wins, ActionRpgHud.Keys["kit.hud.wins"]), Is.EqualTo("Red wins"));
            localization.SetLanguage("uk");
            Assert.That(localization.Text(wins, ActionRpgHud.Keys["kit.hud.wins"]), Is.EqualTo("Red перемагає"));
            Assert.That(localization.Text(LocalizedText.Of("kit.coop.lobby", ("count", 2), ("max", 4)), CoopPanel.Keys["kit.coop.lobby"]),
                Is.EqualTo("Lobby  2 / 4"), "not translated: English");

            var problems = LocalizationChecks.Problems(new[] { english, ukrainian }, "en");
            Assert.That(problems, Has.Member("uk: kit.hud.fell is missing"));
            Assert.That(problems.Any(p => p.Contains("kit.hud.wins")), Is.False, "the same {team}");
        }

        [Test]
        public void LogLines_AreShownInTheLanguage_AndPlainLinesStayAsWritten()
        {
            var ukrainian = new StringTable("uk").Set("kit.log.slain", "{name} повалено");
            var localization = new LocalizationService(new[] { KitTexts.Table(), ukrainian }, "en");
            localization.SetLanguage("uk");

            var slain = new LogLine(LocalizedText.Of("kit.log.slain", ("name", "Вовк")), "Вовк slain");
            Assert.That(slain.In(localization), Is.EqualTo("Вовк повалено"));
            Assert.That(slain.In(null), Is.EqualTo("Вовк slain"));
            Assert.That(new LogLine(LocalizedText.Of("kit.log.levelUp", ("level", 3)), "Level up! Level 3").In(localization),
                Is.EqualTo("Level up! Level 3"), "not translated: English");
            Assert.That(new LogLine(default, "Bounty paid {x}").In(localization), Is.EqualTo("Bounty paid {x}"), "a plain line");
            Assert.That(KitTexts.All.Any(t => t.Key == "kit.log.wave"), Is.True);
        }
    }
}
