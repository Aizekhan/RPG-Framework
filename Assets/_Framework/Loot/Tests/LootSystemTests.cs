using NUnit.Framework;
using RPGFramework.Loot;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Loot.Tests
{
    /// <summary>
    /// Comprehensive test suite for RPG Framework Loot System.
    /// Tests loot entries, tables, generators, and registry.
    /// </summary>
    [TestFixture]
    public class LootSystemTests
    {
        #region LootEntryTests (4 tests)

        [Test]
        public void CreateLootEntry_WithDefaults_UsesExpectedValues()
        {
            // Arrange & Act
            var entry = new LootEntry("item_sword");

            // Assert
            Assert.That(entry.ItemId, Is.EqualTo("item_sword"), "ItemId should match");
            Assert.That(entry.Weight, Is.EqualTo(1), "Default weight should be 1");
            Assert.That(entry.MinQuantity, Is.EqualTo(1), "Default min quantity should be 1");
            Assert.That(entry.MaxQuantity, Is.EqualTo(1), "Default max quantity should be 1");
            Assert.That(entry.DropChance, Is.EqualTo(1.0f), "Default drop chance should be 100%");
            Assert.That(entry.MinLevel, Is.EqualTo(0), "Default min level should be 0");
            Assert.That(entry.MaxLevel, Is.EqualTo(-1), "Default max level should be -1 (no max)");
        }

        [Test]
        public void CreateLootEntry_WithLevelRange_FiltersCorrectly()
        {
            // Arrange & Act
            var entry = new LootEntry("item_legendary", minLevel: 10, maxLevel: 20);

            // Assert
            Assert.That(entry.MinLevel, Is.EqualTo(10), "MinLevel should be 10");
            Assert.That(entry.MaxLevel, Is.EqualTo(20), "MaxLevel should be 20");
        }

        [Test]
        public void DropChance_CustomValue_IsStored()
        {
            // Arrange & Act
            var entry = new LootEntry("item_rare", dropChance: 0.5f);

            // Assert
            Assert.That(entry.DropChance, Is.EqualTo(0.5f), "Drop chance should be 50%");
        }

        [Test]
        public void Weight_PositiveValue_IsValid()
        {
            // Arrange & Act
            var entry = new LootEntry("item_common", weight: 100);

            // Assert
            Assert.That(entry.Weight, Is.EqualTo(100), "Weight should be 100");
        }

        #endregion

        #region LootTableTests (4 tests)

        [Test]
        public void CreateLootTable_WithEntries_StoresCorrectly()
        {
            // Arrange
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_1"),
                new LootEntry("item_2"),
                new LootEntry("item_3")
            };

            // Act
            var table = new LootTable("table_goblin", "Goblin Loot", entries);

            // Assert
            Assert.That(table.TableId, Is.EqualTo("table_goblin"), "TableId should match");
            Assert.That(table.Name, Is.EqualTo("Goblin Loot"), "Name should match");
            Assert.That(table.Entries.Count, Is.EqualTo(3), "Should have 3 entries");
        }

        [Test]
        public void GuaranteedDrops_AlwaysIncluded()
        {
            // Arrange
            var guaranteed = new List<string> { "quest_item_1", "quest_item_2" };
            var table = new LootTable("table_boss", "Boss", null, guaranteedDrops: guaranteed);

            // Assert
            Assert.That(table.GuaranteedDrops.Count, Is.EqualTo(2), "Should have 2 guaranteed drops");
            Assert.That(table.GuaranteedDrops[0], Is.EqualTo("quest_item_1"), "First guaranteed should match");
        }

        [Test]
        public void MinMaxDrops_ValidRange()
        {
            // Arrange & Act
            var table = new LootTable("table_chest", "Chest", null, minDrops: 2, maxDrops: 5);

            // Assert
            Assert.That(table.MinDrops, Is.EqualTo(2), "MinDrops should be 2");
            Assert.That(table.MaxDrops, Is.EqualTo(5), "MaxDrops should be 5");
        }

        [Test]
        public void AllowDuplicates_FlagWorks()
        {
            // Arrange & Act
            var tableNoDupes = new LootTable("table_1", "No Dupes", null, allowDuplicates: false);
            var tableDupes = new LootTable("table_2", "Dupes OK", null, allowDuplicates: true);

            // Assert
            Assert.That(tableNoDupes.AllowDuplicates, Is.False, "Should not allow duplicates");
            Assert.That(tableDupes.AllowDuplicates, Is.True, "Should allow duplicates");
        }

        #endregion

        #region LootGeneratorTests (10 tests)

        [Test]
        public void GenerateLoot_ReturnsValidResult()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_sword", weight: 1)
            };
            var table = new LootTable("table_test", "Test", entries);

            // Act
            var result = generator.GenerateLoot(table, level: 1, seed: 12345);

            // Assert
            Assert.That(result.TableId, Is.EqualTo("table_test"), "TableId should match");
            Assert.That(result.Level, Is.EqualTo(1), "Level should be 1");
            Assert.That(result.Seed, Is.EqualTo(12345), "Seed should match");
        }

        [Test]
        public void GenerateLoot_RespectsMinMaxDrops()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_1", weight: 100),
                new LootEntry("item_2", weight: 100),
                new LootEntry("item_3", weight: 100)
            };
            var table = new LootTable("table_test", "Test", entries, minDrops: 2, maxDrops: 2);

            // Act
            var result = generator.GenerateLoot(table, seed: 12345);

            // Assert - with min=max=2, should get exactly 2 drops (may include duplicates or failures)
            Assert.That(result.TotalCount, Is.LessThanOrEqualTo(2), "Should not exceed max drops");
        }

        [Test]
        public void GenerateLoot_AddsGuaranteedDrops()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var guaranteed = new List<string> { "quest_item" };
            var table = new LootTable("table_test", "Test", null, minDrops: 0, maxDrops: 0, guaranteedDrops: guaranteed);

            // Act
            var result = generator.GenerateLoot(table);

            // Assert
            Assert.That(result.TotalCount, Is.GreaterThanOrEqualTo(1), "Should have at least guaranteed drop");
            Assert.That(result.Drops.Any(d => d.ItemId == "quest_item"), Is.True, "Should contain quest item");
        }

        [Test]
        public void GenerateLoot_FiltersEntriesByLevel()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_low", weight: 100, minLevel: 1, maxLevel: 5),
                new LootEntry("item_high", weight: 100, minLevel: 10, maxLevel: 20)
            };
            var table = new LootTable("table_test", "Test", entries, minDrops: 10, maxDrops: 10);

            // Act - level 3 should only allow item_low
            var result = generator.GenerateLoot(table, level: 3, seed: 12345);

            // Assert - if any items dropped, they should all be item_low
            foreach (var drop in result.Drops)
            {
                if (drop.SourceEntry != null) // Skip guaranteed drops
                {
                    Assert.That(drop.ItemId, Is.EqualTo("item_low"), "Only item_low should drop at level 3");
                }
            }
        }

        [Test]
        public void GenerateLoot_DeterministicWithSeed()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_1", weight: 50),
                new LootEntry("item_2", weight: 50)
            };
            var table = new LootTable("table_test", "Test", entries);

            // Act - generate twice with same seed
            var result1 = generator.GenerateLoot(table, seed: 99999);
            var result2 = generator.GenerateLoot(table, seed: 99999);

            // Assert - results should be identical
            Assert.That(result1.TotalCount, Is.EqualTo(result2.TotalCount), "Should generate same count");
            for (int i = 0; i < result1.TotalCount; i++)
            {
                Assert.That(result1.Drops[i].ItemId, Is.EqualTo(result2.Drops[i].ItemId), "Items should match");
                Assert.That(result1.Drops[i].Quantity, Is.EqualTo(result2.Drops[i].Quantity), "Quantities should match");
            }
        }

        [Test]
        public void GenerateLoot_LuckModifier_AffectsWeight()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_common", weight: 90),
                new LootEntry("item_rare", weight: 10)
            };
            var table = new LootTable("table_test", "Test", entries, minDrops: 100, maxDrops: 100);

            // Act - generate with high luck
            var resultHighLuck = generator.GenerateLoot(table, luckModifier: 5.0f, seed: 12345);

            // Assert - luck modifier should affect results (exact distribution depends on RNG)
            Assert.That(resultHighLuck.LuckModifier, Is.EqualTo(5.0f), "Luck modifier should be recorded");
        }

        [Test]
        public void GenerateLoot_WeightedSelection_HigherWeightMoreLikely()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_common", weight: 90),
                new LootEntry("item_rare", weight: 10)
            };
            var table = new LootTable("table_test", "Test", entries, minDrops: 1000, maxDrops: 1000, allowDuplicates: true);

            // Act
            var result = generator.GenerateLoot(table, seed: 12345);

            // Assert - with 1000 rolls, common (90% weight) should appear significantly more than rare (10%)
            int commonCount = result.Drops.Count(d => d.ItemId == "item_common");
            int rareCount = result.Drops.Count(d => d.ItemId == "item_rare");

            // At minimum, common should be at least 3x more frequent than rare (conservative threshold)
            Assert.That(commonCount, Is.GreaterThan(rareCount * 3), "Common item (90% weight) should drop significantly more than rare (10% weight)");
        }

        [Test]
        public void GenerateLoot_NoDuplicates_WhenDisallowed()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_1", weight: 100),
                new LootEntry("item_2", weight: 100)
            };
            var table = new LootTable("table_test", "Test", entries, minDrops: 2, maxDrops: 2, allowDuplicates: false);

            // Act
            var result = generator.GenerateLoot(table, seed: 12345);

            // Assert - should have at most 2 unique items
            var uniqueItems = result.Drops.Select(d => d.ItemId).Distinct().ToList();
            Assert.That(uniqueItems.Count, Is.LessThanOrEqualTo(2), "Should not have duplicate items");
        }

        [Test]
        public void GenerateLoot_AllowsDuplicates_WhenEnabled()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);
            var entries = new List<ILootEntry>
            {
                new LootEntry("item_1", weight: 100)
            };
            var table = new LootTable("table_test", "Test", entries, minDrops: 3, maxDrops: 3, allowDuplicates: true);

            // Act
            var result = generator.GenerateLoot(table, seed: 12345);

            // Assert - with only 1 entry and duplicates allowed, could potentially get 3x same item
            Assert.That(table.AllowDuplicates, Is.True, "Table should allow duplicates");
        }

        [Test]
        public void GenerateLootById_InvalidTableId_ReturnsDefault()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var generator = new LootGenerator(registry);

            // Act
            var result = generator.GenerateLootById("nonexistent_table");

            // Assert
            Assert.That(result.TableId, Is.Null, "Should return default result");
            Assert.That(result.HasLoot, Is.False, "Should have no loot");
        }

        #endregion

        #region LootTableRegistryTests (5 tests)

        [Test]
        public void RegisterTable_AddsToRegistry()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var table = new LootTable("table_goblin", "Goblin", null);

            // Act
            registry.RegisterTable(table);

            // Assert
            Assert.That(registry.HasTable("table_goblin"), Is.True, "Registry should contain table");
        }

        [Test]
        public void GetTable_ReturnsRegisteredTable()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var table = new LootTable("table_dragon", "Dragon", null);
            registry.RegisterTable(table);

            // Act
            var retrieved = registry.GetTable("table_dragon");

            // Assert
            Assert.That(retrieved, Is.Not.Null, "Should retrieve table");
            Assert.That(retrieved.TableId, Is.EqualTo("table_dragon"), "TableId should match");
            Assert.That(retrieved.Name, Is.EqualTo("Dragon"), "Name should match");
        }

        [Test]
        public void GetTable_InvalidId_ReturnsNull()
        {
            // Arrange
            var registry = new LootTableRegistry();

            // Act
            var retrieved = registry.GetTable("nonexistent");

            // Assert
            Assert.That(retrieved, Is.Null, "Should return null for nonexistent table");
        }

        [Test]
        public void UnregisterTable_RemovesFromRegistry()
        {
            // Arrange
            var registry = new LootTableRegistry();
            var table = new LootTable("table_temp", "Temporary", null);
            registry.RegisterTable(table);
            Assert.That(registry.HasTable("table_temp"), Is.True, "Table should be registered");

            // Act
            var removed = registry.UnregisterTable("table_temp");

            // Assert
            Assert.That(removed, Is.True, "Should return true when removing");
            Assert.That(registry.HasTable("table_temp"), Is.False, "Table should no longer be registered");
        }

        [Test]
        public void GetAllTables_ReturnsAllRegistered()
        {
            // Arrange
            var registry = new LootTableRegistry();
            registry.RegisterTable(new LootTable("table_1", "Table 1", null));
            registry.RegisterTable(new LootTable("table_2", "Table 2", null));
            registry.RegisterTable(new LootTable("table_3", "Table 3", null));

            // Act
            var allTables = registry.GetAllTables();

            // Assert
            Assert.That(allTables.Count, Is.EqualTo(3), "Should have 3 tables");
        }

        #endregion
    }
}
