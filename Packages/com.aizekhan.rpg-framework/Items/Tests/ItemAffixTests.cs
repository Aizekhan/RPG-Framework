using System;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Stats;

namespace RPGFramework.Items.Tests
{
    /// <summary>
    /// Random item affixes: counts by rarity, one per group, item level and kind, weights, values on steps, the same
    /// seed rolling the same item, names, saving on the instance, and the stat modifiers they give.
    /// </summary>
    public sealed class ItemAffixTests
    {
        private enum HeroStat
        {
            Strength,
            Crit
        }

        private static AffixPool Pool() => new AffixPool()
            .Add(new AffixDefinition("str_1", AffixSlot.Suffix, "of the Bear", "Strength", 5f, 12f) { Group = "str" })
            .Add(new AffixDefinition("str_2", AffixSlot.Suffix, "of the Titan", "Strength", 20f, 30f) { Group = "str", MinItemLevel = 20 })
            .Add(new AffixDefinition("sturdy", AffixSlot.Prefix, "Sturdy", "Strength", 1f, 3f))
            .Add(new AffixDefinition("keen", AffixSlot.Prefix, "Keen", "Crit", 0.01f, 0.05f, StatModifierType.Multiplicative) { Step = 0.01f }
                .OnlyOn("weapon"));

        [Test]
        public void Roll_CountsByRarity_OnePerGroup_AndValuesOnSteps()
        {
            var pool = Pool();
            for (int seed = 0; seed < 50; seed++)
            {
                var affixes = pool.Roll(new Random(seed), 30, ItemRarity.Legendary, new[] { "weapon" });
                Assert.That(affixes.Count, Is.EqualTo(3), "only 3 can fit: one Strength suffix, two prefixes");
                Assert.That(affixes.Count(a => a.AffixId.StartsWith("str_")), Is.EqualTo(1), "one per group");
                foreach (var affix in affixes)
                {
                    var definition = pool.Find(affix.AffixId);
                    Assert.That(affix.Value, Is.InRange(definition.Min, definition.Max + 1e-4f));
                    if (affix.AffixId != "keen")
                        Assert.That(affix.Value % 1f, Is.EqualTo(0f), "whole steps");
                }
            }

            Assert.That(pool.Roll(new Random(1), 30, ItemRarity.Common), Is.Empty);
            Assert.That(pool.Roll(new Random(1), 30, ItemRarity.Uncommon).Count, Is.EqualTo(1));
            Assert.That(pool.Roll(new Random(3), 30, ItemRarity.Mythic, new[] { "weapon" }),
                Is.EqualTo(pool.Roll(new Random(3), 30, ItemRarity.Mythic, new[] { "weapon" })), "the same seed");
        }

        [Test]
        public void Roll_KeepsToItemLevelKindAndWeight()
        {
            var pool = Pool();
            for (int seed = 0; seed < 50; seed++)
            {
                var affixes = pool.Roll(new Random(seed), 5, ItemRarity.Mythic, new[] { "ring" });
                Assert.That(affixes.Select(a => a.AffixId), Is.EquivalentTo(new[] { "str_1", "sturdy" }),
                    "level 5: no Titan; a ring: no Keen");
            }

            pool.Find("sturdy").Weight = 0;
            pool.SetCount(ItemRarity.Rare, 1, 1, 1);
            for (int seed = 0; seed < 20; seed++)
                Assert.That(pool.Roll(new Random(seed), 5, ItemRarity.Rare, new[] { "ring" }).Single().AffixId, Is.EqualTo("str_1"));
        }

        [Test]
        public void Names_Saving_AndModifiers()
        {
            var pool = Pool();
            var affixes = new[] { new RolledAffix("sturdy", 2f), new RolledAffix("str_1", 9f), new RolledAffix("keen", 0.03f) };
            Assert.That(pool.NameFor("Sword", affixes), Is.EqualTo("Sturdy Sword of the Bear"));
            Assert.That(pool.NameFor("Sword", affixes, n => n.ToUpperInvariant()), Is.EqualTo("STURDY Sword OF THE BEAR"));
            Assert.That(pool.NameFor("Sword", new RolledAffix[0]), Is.EqualTo("Sword"));

            var instance = new ItemInstance("sword");
            ItemAffixes.Set(instance, affixes);
            Assert.That(instance.CustomProperties[ItemAffixes.Key], Is.EqualTo("sturdy=2;str_1=9;keen=0.03"));
            Assert.That(ItemAffixes.Get(instance), Is.EqualTo(affixes));
            ItemAffixes.Set(instance, new RolledAffix[0]);
            Assert.That(ItemAffixes.Has(instance), Is.False);

            var stats = new StatContainer<HeroStat, float>();
            stats.Initialize();
            stats.SetBaseStat(HeroStat.Strength, 10f);
            foreach (var modifier in pool.ModifiersFor<HeroStat>(instance.InstanceId, affixes))
                stats.AddModifier(modifier);
            Assert.That(stats.GetFinalStat(HeroStat.Strength), Is.EqualTo(21f));
            Assert.That(stats.RemoveModifier(instance.InstanceId + ":str_1"), Is.True);

            var reforged = pool.RerollValues(new Random(7), affixes);
            Assert.That(reforged.Select(a => a.AffixId), Is.EqualTo(affixes.Select(a => a.AffixId)));
            Assert.That(() => new AffixDefinition("bad id", AffixSlot.Prefix, "", "", 0f, 1f), Throws.ArgumentException);
        }

        [Test]
        public void RolledItems_KeepTheAffixesInTheId_AndBuildTheSameItemFromIt()
        {
            var pool = Pool();
            var affixes = new[] { new RolledAffix("sturdy", 2f), new RolledAffix("str_1", 9f) };
            string id = RolledItems.Compose("sword", ItemRarity.Epic, affixes);
            Assert.That(id, Is.EqualTo("sword~3~sturdy=2;str_1=9"));
            Assert.That(RolledItems.BaseOf(id), Is.EqualTo("sword"));
            Assert.That(RolledItems.BaseOf("sword"), Is.EqualTo("sword"));
            Assert.That(RolledItems.TryParse(id, out string baseId, out var rarity, out var parsed), Is.True);
            Assert.That((baseId, rarity), Is.EqualTo(("sword", ItemRarity.Epic)));
            Assert.That(parsed, Is.EqualTo(affixes));
            Assert.That(RolledItems.TryParse("sword", out _, out _, out _), Is.False);

            var item = RolledItems.Create(id, new Item("sword", "Sword", "", ItemType.Equipment, iconId: "icon.sword"), pool);
            Assert.That(item.Name, Is.EqualTo("Sturdy Sword of the Bear"));
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Epic));
            Assert.That(item.IconId, Is.EqualTo("icon.sword"));
            Assert.That(RolledItems.Create(id, new Item("axe", "Axe", "", ItemType.Equipment), pool), Is.Null, "another base");

            Assert.That(RolledItems.Roll("sword", pool, new RarityTable().Set(ItemRarity.Common, 1), new Random(1), 30), Is.EqualTo("sword"),
                "a Common rolls nothing");
            Assert.That(RolledItems.IsRolled(RolledItems.Roll("sword", pool, new RarityTable().Set(ItemRarity.Rare, 1), new Random(1), 30)), Is.True);
            Assert.That(() => RolledItems.Compose("a~b", ItemRarity.Rare, affixes), Throws.ArgumentException);
        }

        [Test]
        public void RarityTable_RollsByWeight_AndLuckFavoursTheRareTiers()
        {
            var table = new RarityTable().Set(ItemRarity.Common, 3).Set(ItemRarity.Rare, 1);
            Assert.That(table.ChanceOf(ItemRarity.Rare), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(table.ChanceOf(ItemRarity.Rare, luck: 1f), Is.EqualTo(0.5).Within(1e-9), "Rare (tier 2) weighs 3 with luck 1");
            Assert.That(table.ChanceOf(ItemRarity.Epic), Is.EqualTo(0.0));

            var random = new Random(11);
            int rare = 0;
            for (int i = 0; i < 4000; i++)
            {
                var rarity = table.Roll(random);
                Assert.That(rarity, Is.EqualTo(ItemRarity.Common).Or.EqualTo(ItemRarity.Rare));
                if (rarity == ItemRarity.Rare)
                    rare++;
            }
            Assert.That(rare / 4000.0, Is.EqualTo(0.25).Within(0.03));

            Assert.That(new RarityTable().Roll(new Random(1)), Is.EqualTo(ItemRarity.Common), "an empty table");
            Assert.That(RarityTable.Default().WeightOf(ItemRarity.Mythic), Is.EqualTo(1));
        }
    }
}
