using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Localization.Unity;
using RPGFramework.Logging;
using UnityEngine;
using Logger = RPGFramework.Logging.Logger;

namespace RPGFramework.Localization.Tests
{
    /// <summary>
    /// Localization: texts by key with the default language behind, named values, plural forms (Ukrainian one / few /
    /// many), the language kept and the device's used on the first start, a change announced, CSV both ways, checks
    /// that keep translations whole, the module from table assets.
    /// </summary>
    public sealed class LocalizationTests
    {
        private static StringTable English() => new StringTable("en", "English")
            .Set("menu.continue", "Continue")
            .Set("menu.quit", "Quit")
            .Set("hit", "{name} hits for {damage} damage")
            .Set("gold", "{value:0.0} gold")
            .Set("goblins.one", "a goblin")
            .Set("goblins.other", "{count} goblins")
            .Set("braces", "{{not a value}}");

        private static StringTable Ukrainian() => new StringTable("uk", "Українська")
            .Set("menu.continue", "Продовжити")
            .Set("hit", "{name} б'є на {damage} шкоди")
            .Set("goblins.one", "{count} гоблін")
            .Set("goblins.few", "{count} гобліни")
            .Set("goblins.many", "{count} гоблінів");

        [Test]
        public void Texts_ComeFromTheLanguage_ThenTheDefault_ThenTheKey()
        {
            var localization = new LocalizationService(new[] { English(), Ukrainian() }, "en", systemLanguage: "uk-UA");

            Assert.That(localization.Language, Is.EqualTo("uk"), "the device's language, region aside");
            Assert.That(localization.Languages, Is.EqualTo(new[] { "en", "uk" }));
            Assert.That(localization.DisplayNameOf("uk"), Is.EqualTo("Українська"));
            Assert.That(localization.Text("menu.continue"), Is.EqualTo("Продовжити"));
            Assert.That(localization.Text("menu.quit"), Is.EqualTo("Quit"), "not translated yet: English");
            Assert.That(localization.Text("menu.nowhere"), Is.EqualTo("menu.nowhere"));
            Assert.That(localization.Text("kit.respawn", "Respawn"), Is.EqualTo("Respawn"), "the kit's own English");
            Assert.That(localization.MissingKeys, Is.EquivalentTo(new[] { "uk: menu.quit", "uk: menu.nowhere", "uk: kit.respawn" }));
            Assert.That(localization.Has("menu.quit"), Is.True);
            Assert.That(localization.Has("goblins"), Is.True, "a key with plural forms");
            Assert.That(localization.Has("kit.respawn"), Is.False);
        }

        [Test]
        public void NamedValues_ArePutIn_WithTheLanguagesNumbers()
        {
            var localization = new LocalizationService(new[] { English(), Ukrainian() }, "en");

            Assert.That(localization.Format("hit", ("name", "Wolf"), ("damage", 12)), Is.EqualTo("Wolf hits for 12 damage"));
            Assert.That(localization.Format("gold", ("value", 2.5)), Is.EqualTo("2.5 gold"));
            Assert.That(localization.Text("braces"), Is.EqualTo("{not a value}"));
            Assert.That(localization.Format("hit", ("name", "Wolf")), Is.EqualTo("Wolf hits for {damage} damage"),
                "a value not given stays visible");

            var text = LocalizedText.Of("hit", ("name", LocalizedText.Of("menu.quit")), ("damage", 3));
            Assert.That(localization.Text(text), Is.EqualTo("Quit hits for 3 damage"), "a value that is a text is translated");

            localization.SetLanguage("uk");
            Assert.That(localization.Text(text), Is.EqualTo("Quit б'є на 3 шкоди"));
            Assert.That(localization.Format("gold", ("value", 2.5)), Is.EqualTo("2,5 gold"), "Ukrainian decimal comma");
        }

        [Test]
        public void Plurals_PickTheFormOfTheLanguage()
        {
            var localization = new LocalizationService(new[] { English(), Ukrainian() }, "en");

            Assert.That(localization.Plural("goblins", 1), Is.EqualTo("a goblin"));
            Assert.That(localization.Plural("goblins", 3), Is.EqualTo("3 goblins"));

            localization.SetLanguage("uk");
            Assert.That(localization.Plural("goblins", 1), Is.EqualTo("1 гоблін"));
            Assert.That(localization.Plural("goblins", 3), Is.EqualTo("3 гобліни"));
            Assert.That(localization.Plural("goblins", 5), Is.EqualTo("5 гоблінів"));
            Assert.That(localization.Plural("goblins", 11), Is.EqualTo("11 гоблінів"));
            Assert.That(localization.Plural("goblins", 21), Is.EqualTo("21 гоблін"));
            Assert.That(localization.Plural("goblins", 24), Is.EqualTo("24 гобліни"));
            Assert.That(localization.Text(LocalizedText.Of("goblins", ("count", 12))), Is.EqualTo("12 гоблінів"));

            Assert.That(PluralRules.FormOf("uk", 0), Is.EqualTo(PluralForm.Many));
            Assert.That(PluralRules.FormOf("uk-UA", 2), Is.EqualTo(PluralForm.Few));
            Assert.That(PluralRules.FormOf("pl", 21), Is.EqualTo(PluralForm.Many), "Polish: one only for 1");
            Assert.That(PluralRules.FormOf("ja", 1), Is.EqualTo(PluralForm.Other));
            Assert.That(PluralRules.FormOf("en", 1), Is.EqualTo(PluralForm.One));
        }

        [Test]
        public void TheLanguage_IsKept_AndAChangeIsAnnounced()
        {
            var store = new MemoryLanguageStore();
            using (var bus = new EventBus())
            {
                var published = new List<LanguageChangedEvent>();
                bus.Subscribe<LanguageChangedEvent>(e => published.Add(e));
                var localization = new LocalizationService(new[] { English(), Ukrainian() }, "en", store, "fr", bus);
                var changed = new List<string>();
                localization.LanguageChanged += changed.Add;

                Assert.That(localization.Language, Is.EqualTo("en"), "no French table: the default");
                Assert.That(localization.SetLanguage("de"), Is.False);
                Assert.That(localization.SetLanguage("UK"), Is.True);
                Assert.That(changed, Is.EqualTo(new[] { "uk" }));
                Assert.That(published.Single().Previous, Is.EqualTo("en"));
                Assert.That(published.Single().Language, Is.EqualTo("uk"));
                Assert.That(store.Language, Is.EqualTo("uk"));
            }

            var nextStart = new LocalizationService(new[] { English(), Ukrainian() }, "en", store, "en-US");
            Assert.That(nextStart.Language, Is.EqualTo("uk"), "the player's choice beats the device's language");
        }

        [Test]
        public void Csv_GoesToTranslatorsAndBack()
        {
            var english = new StringTable("en").Set("a", "Plain").Set("b", "Comma, \"quoted\"\nand a new line");
            var ukrainian = new StringTable("uk").Set("a", "Просто");

            string csv = LocalizationCsv.Write(new[] { english, ukrainian });
            Assert.That(csv, Does.StartWith("key,en,uk\na,Plain,Просто\n"));

            var tables = LocalizationCsv.Read(csv);
            Assert.That(tables.Select(t => t.Language), Is.EqualTo(new[] { "en", "uk" }));
            Assert.That(tables[0].TryGet("b", out string b) ? b : null, Is.EqualTo("Comma, \"quoted\"\nand a new line"));
            Assert.That(tables[1].Has("b"), Is.False, "an empty cell is not translated yet");
            Assert.That(tables[1].TryGet("a", out string a) ? a : null, Is.EqualTo("Просто"));
            Assert.Throws<System.FormatException>(() => LocalizationCsv.Read("id,en\na,b"));
        }

        [Test]
        public void Checks_FindMissingKeysAndValues_AndAcceptOtherPluralForms()
        {
            var english = new StringTable("en").Set("hit", "{name} hits for {damage}").Set("goblins.one", "a goblin")
                .Set("goblins.other", "{count} goblins").Set("quit", "Quit");
            var ukrainian = new StringTable("uk").Set("hit", "{name} б'є").Set("goblins.one", "{count} гоблін")
                .Set("goblins.few", "{count} гобліни").Set("goblins.many", "{count} гоблінів").Set("extra", "Зайве");

            var problems = LocalizationChecks.Problems(new[] { english, ukrainian }, "en");

            Assert.That(problems, Is.EquivalentTo(new[]
            {
                "uk: quit is missing",
                "uk: extra is not in en",
                "uk: hit has {name} where en has {damage}, {name}"
            }));
            Assert.That(LocalizationChecks.Problems(new[] { English(), English() }), Is.Empty);
        }

        [Test]
        public void TheModule_RegistersTheLibrarysTables()
        {
            var english = StringTableAsset.Create(English());
            var ukrainian = StringTableAsset.Create(Ukrainian());
            var library = LocalizationLibrary.Create(new[] { english, ukrainian }, "en");
            var store = new MemoryLanguageStore { Language = "uk" };
            try
            {
                using (var logger = new Logger(LogSeverity.Warning, new MemoryLogSink()))
                using (var host = new GameHost(logger))
                {
                    host.Start(b => b.UseModule(new LocalizationModule(library, store, "en")));
                    var localization = host.Container.Resolve<ILocalization>();
                    var published = new List<LanguageChangedEvent>();
                    host.Container.Resolve<IEventBus>().Subscribe<LanguageChangedEvent>(e => published.Add(e));

                    Assert.That(localization.Language, Is.EqualTo("uk"));
                    Assert.That(localization.Text("menu.continue"), Is.EqualTo("Продовжити"));
                    localization.SetLanguage("en");
                    Assert.That(published.Single().Language, Is.EqualTo("en"));
                    Assert.That(library.Table("uk").DisplayName, Is.EqualTo("Українська"));
                    Assert.That(LocalizationUnity.CodeOf(SystemLanguage.Ukrainian), Is.EqualTo("uk"));
                    Assert.That(LocalizationUnity.CodeOf(SystemLanguage.Unknown), Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(english);
                Object.DestroyImmediate(ukrainian);
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void ACodec_CarriesATextAndItsValues_InBytesAndInAString_AndAPlainStringIsItsOwnKey()
        {
            var text = LocalizedText.Of("log.hit", ("who", LocalizedText.Of("hero.anna")), ("count", 3), ("share", 0.5), ("crit", true),
                ("note", "ow"), ("nothing", null), ("big", 5000000000L));
            var stream = new MemoryStream();
            LocalizedTextCodec.Write(new BinaryWriter(stream), text);
            stream.Position = 0;
            var back = LocalizedTextCodec.Read(new BinaryReader(stream));
            Assert.That(back, Is.EqualTo(text));

            string wire = LocalizedTextCodec.ToWire(text);
            Assert.That(LocalizedTextCodec.IsEncoded(wire), Is.True);
            Assert.That(LocalizedTextCodec.FromWire(wire), Is.EqualTo(text));
            Assert.That(LocalizedTextCodec.ToWire("menu.play"), Is.EqualTo("menu.play"), "a key alone stays readable");
            Assert.That(LocalizedTextCodec.FromWire("Not your move").Key, Is.EqualTo("Not your move"));
            Assert.That(LocalizedTextCodec.FromWire(null).IsEmpty, Is.True);
            Assert.That(LocalizedTextCodec.FromWire("not base64!").Key, Is.EqualTo("not base64!"), "damaged: a literal");

            var english = new StringTable("en").Set("log.hit", "{who} hits {count} times").Set("hero.anna", "Anna");
            Assert.That(new LocalizationService(new[] { english }).Text(LocalizedTextCodec.FromWire(wire)), Is.EqualTo("Anna hits 3 times"));
        }
    }
}
