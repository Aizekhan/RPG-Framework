using System;
using System.Linq;
using NUnit.Framework;

namespace RPGFramework.Loot.Tests
{
    public sealed class LootTableBuilderTests
    {
        [Test]
        public void Build_ProducesConfiguredTable()
        {
            var table = LootTableBuilder.Create("goblin_common", "Common Goblin Loot")
                .WithDrops(2, 4)
                .AllowDuplicates()
                .AddEntry("gold_coins", weight: 100, minQuantity: 5, maxQuantity: 15, dropChance: 0.8f)
                .AddEntry("goblin_ear", weight: 10, minLevel: 3, maxLevel: 10)
                .AddGuaranteedDrop("exp_orb_small")
                .Build();

            Assert.That(table.TableId, Is.EqualTo("goblin_common"));
            Assert.That(table.Name, Is.EqualTo("Common Goblin Loot"));
            Assert.That(table.MinDrops, Is.EqualTo(2));
            Assert.That(table.MaxDrops, Is.EqualTo(4));
            Assert.That(table.AllowDuplicates, Is.True);
            Assert.That(table.Entries.Select(e => e.ItemId), Is.EqualTo(new[] { "gold_coins", "goblin_ear" }));
            Assert.That(table.Entries[0].MaxQuantity, Is.EqualTo(15));
            Assert.That(table.Entries[1].MinLevel, Is.EqualTo(3));
            Assert.That(table.GuaranteedDrops, Is.EqualTo(new[] { "exp_orb_small" }));
        }

        [Test]
        public void BuiltTables_AreIndependentOfLaterBuilderChanges()
        {
            var builder = LootTableBuilder.Create("chest").AddEntry("gold");
            var first = builder.Build();

            builder.AddEntry("gem").AddGuaranteedDrop("key");
            var second = builder.Build();

            Assert.That(first.Name, Is.EqualTo("chest"), "name defaults to id");
            Assert.That(first.Entries.Count, Is.EqualTo(1));
            Assert.That(first.GuaranteedDrops, Is.Empty);
            Assert.That(second.Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void BuiltTable_WorksWithGenerator()
        {
            var table = LootTableBuilder.Create("boss")
                .WithDrops(1, 1)
                .AddEntry("crown", minQuantity: 2, maxQuantity: 2)
                .AddGuaranteedDrop("trophy")
                .Build();
            var generator = new LootGenerator(new LootTableRegistry());

            var result = generator.GenerateLoot(table, seed: 42);

            CollectionAssert.AreEquivalent(new[] { "crown", "trophy" }, result.Drops.Select(d => d.ItemId));
            Assert.That(result.Drops.Single(d => d.ItemId == "crown").Quantity, Is.EqualTo(2));
        }

        [Test]
        public void InvalidValues_AreRejected()
        {
            var builder = LootTableBuilder.Create("t");

            Assert.Throws<ArgumentException>(() => LootTableBuilder.Create(""));
            Assert.Throws<ArgumentException>(() => builder.AddEntry(null));
            Assert.Throws<ArgumentException>(() => builder.AddGuaranteedDrop(""));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddEntry("a", weight: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddEntry("a", minQuantity: 3, maxQuantity: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddEntry("a", dropChance: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddEntry("a", minLevel: 5, maxLevel: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithDrops(3, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithDrops(-1, 1));
            Assert.DoesNotThrow(() => builder.AddEntry("a", minLevel: 5, maxLevel: -1));
        }
    }
}
