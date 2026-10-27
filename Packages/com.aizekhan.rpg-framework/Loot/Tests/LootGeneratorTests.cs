using System.Linq;
using NUnit.Framework;

namespace RPGFramework.Loot.Tests
{
    /// <summary>
    /// The generator hands out what the table promises: without duplicates, each roll picks among the items not yet
    /// dropped (so a table asking for as many drops as it has items always gives all of them), and weights decide how
    /// often each item comes up.
    /// </summary>
    public sealed class LootGeneratorTests
    {
        private static LootGenerator Generator() => new LootGenerator(new LootTableRegistry());

        [Test]
        public void WithoutDuplicates_EveryRollStillDrops_UntilTheItemsRunOut()
        {
            var table = LootTableBuilder.Create("chest")
                .AddEntry("sword", weight: 1)
                .AddEntry("shield", weight: 1)
                .AddEntry("helm", weight: 1)
                .WithDrops(3, 3)
                .AllowDuplicates(false)
                .Build();

            for (int seed = 0; seed < 200; seed++)
            {
                var loot = Generator().GenerateLoot(table, seed: seed);
                Assert.That(loot.Drops.Select(d => d.ItemId), Is.EquivalentTo(new[] { "sword", "shield", "helm" }),
                    $"seed {seed}");
            }
        }

        [Test]
        public void Weights_DecideHowOftenAnItemComesUp()
        {
            var table = LootTableBuilder.Create("goblin")
                .AddEntry("ear", weight: 3)
                .AddEntry("tooth", weight: 1)
                .WithDrops(1, 1)
                .Build();

            int ears = 0;
            const int rolls = 4000;
            for (int seed = 0; seed < rolls; seed++)
            {
                if (Generator().GenerateLoot(table, seed: seed).Drops.Single().ItemId == "ear")
                    ears++;
            }

            Assert.That(ears / (double)rolls, Is.EqualTo(0.75).Within(0.03));
        }
    }
}
