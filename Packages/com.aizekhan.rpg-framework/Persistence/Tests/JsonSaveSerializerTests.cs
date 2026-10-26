using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace RPGFramework.Persistence.Tests
{
    /// <summary>
    /// What players type and what games keep goes through the save serializer and comes back the same: names with
    /// quotes, backslashes, colons and brackets, any language, keys with punctuation, enums and the common value types.
    /// Saves from older versions keep loading: unknown members are skipped, missing ones keep their defaults.
    /// </summary>
    public sealed class JsonSaveSerializerTests
    {
        public enum Mood
        {
            Calm,
            Angry = 5
        }

        public sealed class Everything
        {
            public string Text { get; set; }
            public Mood Mood { get; set; }
            public byte Byte { get; set; }
            public short Short { get; set; }
            public uint UInt { get; set; }
            public ulong ULong { get; set; }
            public char Char { get; set; }
            public decimal Money { get; set; }
            public DateTime When { get; set; }
            public Guid Id { get; set; }
            public int? Maybe { get; set; }
            public int? Nothing { get; set; }
            public float Float { get; set; }
            public double Double { get; set; }
            public long Long { get; set; }
            public bool Flag { get; set; }
            public List<string> Names { get; set; } = new List<string>();
            public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
            public List<Everything> Children { get; set; } = new List<Everything>();
            public string Field = "";
        }

        public sealed class Holder
        {
            public string Field = "";
        }

        public sealed class WithDefaults
        {
            public int Level { get; set; } = 1;
            public string Title { get; set; } = "Novice";
            public List<string> Tags { get; set; } = new List<string> { "new" };
        }

        private static readonly JsonSaveSerializer Serializer = new JsonSaveSerializer();

        private static T RoundTrip<T>(T value) => Serializer.Deserialize<T>(Serializer.Serialize(value));

        [TestCase("plain")]
        [TestCase("")]
        [TestCase("Say \"hi\"")]
        [TestCase("\"quoted at both ends\"")]
        [TestCase("ends with a backslash \\")]
        [TestCase("C:\\new\\table")]
        [TestCase("two\nlines\r\tand a tab")]
        [TestCase("a:b, {c} [d]")]
        [TestCase("Привіт, світе ☃ 😀")]
        [TestCase("control \u0001 \u001f \b \f")]
        [TestCase("\\\"")]
        public void Strings_ComeBackTheSame(string text)
        {
            Assert.That(RoundTrip(new WithDefaults { Title = text }).Title, Is.EqualTo(text));
            Assert.That(RoundTrip(new List<string> { text, text }), Is.EqualTo(new[] { text, text }));
        }

        [Test]
        public void NullString_StaysNull()
        {
            Assert.That(RoundTrip(new WithDefaults { Title = null }).Title, Is.Null);
        }

        [Test]
        public void PublicFields_ComeBackTheSame()
        {
            Assert.That(RoundTrip(new Holder { Field = "Say \"hi\" \\" }).Field, Is.EqualTo("Say \"hi\" \\"));
        }

        [Test]
        public void DictionaryKeys_WithPunctuation_ComeBackTheSame()
        {
            var counts = new Dictionary<string, int> { ["quest:1"] = 1, ["a\"b"] = 2, ["x,y"] = 3, ["{z}"] = 4, ["back\\"] = 5 };

            Assert.That(RoundTrip(counts), Is.EquivalentTo(counts));
        }

        [Test]
        public void ValueTypes_ComeBackTheSame()
        {
            var when = new DateTime(2026, 10, 27, 13, 45, 30, DateTimeKind.Utc);
            var id = Guid.NewGuid();
            var copy = RoundTrip(new Everything
            {
                Mood = Mood.Angry, Byte = 250, Short = -1234, UInt = 4000000000, ULong = ulong.MaxValue, Char = 'ї',
                Money = 12345.678m, When = when, Id = id, Maybe = 7, Nothing = null, Float = 0.1f, Double = 1e-300,
                Long = long.MinValue, Flag = true
            });

            Assert.That(copy.Mood, Is.EqualTo(Mood.Angry));
            Assert.That(copy.Byte, Is.EqualTo(250));
            Assert.That(copy.Short, Is.EqualTo(-1234));
            Assert.That(copy.UInt, Is.EqualTo(4000000000));
            Assert.That(copy.ULong, Is.EqualTo(ulong.MaxValue));
            Assert.That(copy.Char, Is.EqualTo('ї'));
            Assert.That(copy.Money, Is.EqualTo(12345.678m));
            Assert.That(copy.When, Is.EqualTo(when));
            Assert.That(copy.When.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(copy.Id, Is.EqualTo(id));
            Assert.That(copy.Maybe, Is.EqualTo(7));
            Assert.That(copy.Nothing, Is.Null);
            Assert.That(copy.Float, Is.EqualTo(0.1f));
            Assert.That(copy.Double, Is.EqualTo(1e-300));
            Assert.That(copy.Long, Is.EqualTo(long.MinValue));
            Assert.That(copy.Flag, Is.True);
        }

        [Test]
        public void NestedObjects_WithBracketsInTheirText_ComeBackTheSame()
        {
            var tree = new Everything
            {
                Text = "root ]}",
                Children = { new Everything { Text = "child {[", Names = { "a]", "[b", "}" } }, new Everything() }
            };

            var copy = RoundTrip(tree);

            Assert.That(copy.Text, Is.EqualTo("root ]}"));
            Assert.That(copy.Children, Has.Count.EqualTo(2));
            Assert.That(copy.Children[0].Text, Is.EqualTo("child {["));
            Assert.That(copy.Children[0].Names, Is.EqualTo(new[] { "a]", "[b", "}" }));
        }

        [Test]
        public void AnOlderSave_UnknownMembersAreSkipped_MissingOnesKeepTheirDefaults()
        {
            string older = "{\"Level\":7,\"Removed\":{\"Deep\":[1,2,{\"x\":\"}\"}]},\"Gone\":\"text, with: punctuation\"}";

            var loaded = Serializer.Deserialize<WithDefaults>(Encoding.UTF8.GetBytes(older));

            Assert.That(loaded.Level, Is.EqualTo(7));
            Assert.That(loaded.Title, Is.EqualTo("Novice"));
            Assert.That(loaded.Tags, Is.EqualTo(new[] { "new" }));
        }

        [Test]
        public void WhitespaceAndNumberForms_OtherWritersUse_AreRead()
        {
            string pretty = "{\n  \"Level\" : 3 ,\n  \"Title\" : \"A \\u0041\\/B\",\n  \"Tags\" : [ ]\n}";

            var loaded = Serializer.Deserialize<WithDefaults>(Encoding.UTF8.GetBytes(pretty));

            Assert.That(loaded.Level, Is.EqualTo(3));
            Assert.That(loaded.Title, Is.EqualTo("A A/B"));
            Assert.That(loaded.Tags, Is.Empty);
        }

        public sealed class Legacy
        {
            public Mood Mood { get; set; }
            public Dictionary<int, string> ByNumber { get; set; } = new Dictionary<int, string>();
        }

        public sealed class Bag
        {
            public List<string> Items { get; } = new List<string>();
            public HashSet<string> Seen { get; set; } = new HashSet<string>();
            public Bag Next { get; set; }
        }

        [Test]
        public void SavesWrittenByTheOlderSerializer_StillLoad()
        {
            // How the previous version wrote enums and number-keyed dictionaries
            string older = "{\"Mood\":{\"value__\":5},\"ByNumber\":{1:\"one\",2:\"two\"}}";

            var loaded = Serializer.Deserialize<Legacy>(Encoding.UTF8.GetBytes(older));

            Assert.That(loaded.Mood, Is.EqualTo(Mood.Angry));
            Assert.That(loaded.ByNumber, Is.EquivalentTo(new Dictionary<int, string> { [1] = "one", [2] = "two" }));
            Assert.That(RoundTrip(loaded).ByNumber[2], Is.EqualTo("two"));
        }

        [Test]
        public void GetOnlyCollections_AndSets_ComeBackFilled()
        {
            var bag = new Bag { Seen = { "a", "b" } };
            bag.Items.Add("sword");

            var copy = RoundTrip(bag);

            Assert.That(copy.Items, Is.EqualTo(new[] { "sword" }));
            Assert.That(copy.Seen, Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test]
        public void AnObjectThatRefersToItself_FailsWithAReason_InsteadOfCrashing()
        {
            var bag = new Bag();
            bag.Next = bag;

            var error = Assert.Throws<InvalidOperationException>(() => Serializer.Serialize(bag));
            Assert.That(error.Message, Does.Contain("refer back to itself"));
        }

        [TestCase("{\"Level\":")]
        [TestCase("{\"Level\":1")]
        [TestCase("{\"Level\":\"one\"}")]
        [TestCase("[1,2]")]
        [TestCase("{\"Title\":\"unterminated}")]
        public void BrokenText_FailsInsteadOfLoadingSomethingElse(string broken)
        {
            Assert.Throws<InvalidOperationException>(() => Serializer.Deserialize<WithDefaults>(Encoding.UTF8.GetBytes(broken)));
        }
    }
}
