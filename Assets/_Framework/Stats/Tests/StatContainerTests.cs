// Assets/_Framework/Stats/Tests/StatContainerTests.cs

using NUnit.Framework;
using RPGFramework.Stats;

namespace RPGFramework.Stats.Tests
{
    /// <summary>
    /// Comprehensive tests for StatContainer component
    /// Tests initialization, stat storage, modifiers, calculations, and serialization
    /// </summary>
    public class StatContainerTests
    {
        // Test enum for stat types
        private enum TestStatType
        {
            Health,
            Damage,
            Defense,
            Speed,
            CritChance
        }

        [Test]
        public void StatContainer_InitialState_IsEmpty()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();

            // Act & Assert
            Assert.That(container.HasStat(TestStatType.Health), Is.False, "Container should not have Health stat initially");
            Assert.That(container.GetBaseStat(TestStatType.Health), Is.EqualTo(0f), "Unset stat should return default value");
            Assert.That(container.GetFinalStat(TestStatType.Health), Is.EqualTo(0f), "Final stat should be zero for unset stat");
        }

        [Test]
        public void StatContainer_SetBaseStat_StoresValue()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();

            // Act
            container.SetBaseStat(TestStatType.Health, 100f);
            container.SetBaseStat(TestStatType.Damage, 25f);

            // Assert
            Assert.That(container.HasStat(TestStatType.Health), Is.True);
            Assert.That(container.GetBaseStat(TestStatType.Health), Is.EqualTo(100f));
            Assert.That(container.GetBaseStat(TestStatType.Damage), Is.EqualTo(25f));
        }

        [Test]
        public void StatContainer_GetFinalStat_NoModifiers_ReturnsBase()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100f);

            // Act
            float finalHealth = container.GetFinalStat(TestStatType.Health);

            // Assert
            Assert.That(finalHealth, Is.EqualTo(100f), "Final stat should equal base when no modifiers");
        }

        [Test]
        public void StatContainer_AdditiveModifier_AppliesCorrectly()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100f);

            var modifier = new StatModifier<TestStatType, float>(
                modifierId: "armor_bonus",
                statType: TestStatType.Health,
                value: 50f,
                modifierType: StatModifierType.Additive
            );

            // Act
            container.AddModifier(modifier);
            float finalHealth = container.GetFinalStat(TestStatType.Health);

            // Assert
            Assert.That(finalHealth, Is.EqualTo(150f), "Additive modifier should add to base: 100 + 50 = 150");
        }

        [Test]
        public void StatContainer_MultiplicativeModifier_AppliesCorrectly()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Damage, 100f);

            var modifier = new StatModifier<TestStatType, float>(
                modifierId: "damage_boost",
                statType: TestStatType.Damage,
                value: 0.5f, // 50% increase
                modifierType: StatModifierType.Multiplicative
            );

            // Act
            container.AddModifier(modifier);
            float finalDamage = container.GetFinalStat(TestStatType.Damage);

            // Assert
            Assert.That(finalDamage, Is.EqualTo(150f), "Multiplicative modifier: 100 * (1 + 0.5) = 150");
        }

        [Test]
        public void StatContainer_OverrideModifier_OverridesValue()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Speed, 100f);

            var additiveModifier = new StatModifier<TestStatType, float>(
                modifierId: "speed_boost",
                statType: TestStatType.Speed,
                value: 50f,
                modifierType: StatModifierType.Additive
            );

            var overrideModifier = new StatModifier<TestStatType, float>(
                modifierId: "speed_cap",
                statType: TestStatType.Speed,
                value: 200f,
                modifierType: StatModifierType.Override
            );

            // Act
            container.AddModifier(additiveModifier);
            container.AddModifier(overrideModifier);
            float finalSpeed = container.GetFinalStat(TestStatType.Speed);

            // Assert
            Assert.That(finalSpeed, Is.EqualTo(200f), "Override modifier should ignore base and other modifiers");
        }

        [Test]
        public void StatContainer_MultipleAdditives_SumCorrectly()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Defense, 50f);

            var modifier1 = new StatModifier<TestStatType, float>("def1", TestStatType.Defense, 20f, StatModifierType.Additive);
            var modifier2 = new StatModifier<TestStatType, float>("def2", TestStatType.Defense, 30f, StatModifierType.Additive);
            var modifier3 = new StatModifier<TestStatType, float>("def3", TestStatType.Defense, 10f, StatModifierType.Additive);

            // Act
            container.AddModifier(modifier1);
            container.AddModifier(modifier2);
            container.AddModifier(modifier3);
            float finalDefense = container.GetFinalStat(TestStatType.Defense);

            // Assert
            Assert.That(finalDefense, Is.EqualTo(110f), "Multiple additives sum: 50 + 20 + 30 + 10 = 110");
        }

        [Test]
        public void StatContainer_MultipleMultiplicatives_MultiplyCorrectly()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Damage, 100f);

            var modifier1 = new StatModifier<TestStatType, float>("mult1", TestStatType.Damage, 0.1f, StatModifierType.Multiplicative); // +10%
            var modifier2 = new StatModifier<TestStatType, float>("mult2", TestStatType.Damage, 0.2f, StatModifierType.Multiplicative); // +20%

            // Act
            container.AddModifier(modifier1);
            container.AddModifier(modifier2);
            float finalDamage = container.GetFinalStat(TestStatType.Damage);

            // Assert
            // 100 * (1 + 0.1) * (1 + 0.2) = 100 * 1.1 * 1.2 = 132
            Assert.That(finalDamage, Is.EqualTo(132f).Within(0.01f), "Multiple multiplicatives compound: 100 * 1.1 * 1.2 = 132");
        }

        [Test]
        public void StatContainer_MixedModifiers_CalculateCorrectOrder()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Damage, 100f);

            var additive = new StatModifier<TestStatType, float>("add", TestStatType.Damage, 50f, StatModifierType.Additive);
            var multiplicative = new StatModifier<TestStatType, float>("mult", TestStatType.Damage, 0.5f, StatModifierType.Multiplicative);

            // Act
            container.AddModifier(additive);
            container.AddModifier(multiplicative);
            float finalDamage = container.GetFinalStat(TestStatType.Damage);

            // Assert
            // Calculation: (base + additives) * multiplicatives
            // (100 + 50) * (1 + 0.5) = 150 * 1.5 = 225
            Assert.That(finalDamage, Is.EqualTo(225f).Within(0.01f), "Mixed modifiers: (100 + 50) * 1.5 = 225");
        }

        [Test]
        public void StatContainer_RemoveModifier_RecalculatesStat()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100f);

            var modifier = new StatModifier<TestStatType, float>("temp_bonus", TestStatType.Health, 50f, StatModifierType.Additive);

            container.AddModifier(modifier);
            Assert.That(container.GetFinalStat(TestStatType.Health), Is.EqualTo(150f), "Before removal");

            // Act
            bool removed = container.RemoveModifier("temp_bonus");
            float finalHealth = container.GetFinalStat(TestStatType.Health);

            // Assert
            Assert.That(removed, Is.True, "Modifier should be removed");
            Assert.That(finalHealth, Is.EqualTo(100f), "Final stat should return to base after removal");
        }

        [Test]
        public void StatContainer_RemoveAllModifiers_ClearsStat()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Damage, 100f);

            container.AddModifier(new StatModifier<TestStatType, float>("mod1", TestStatType.Damage, 20f, StatModifierType.Additive));
            container.AddModifier(new StatModifier<TestStatType, float>("mod2", TestStatType.Damage, 0.5f, StatModifierType.Multiplicative));

            Assert.That(container.GetFinalStat(TestStatType.Damage), Is.Not.EqualTo(100f), "Should have modifiers");

            // Act
            container.RemoveAllModifiers(TestStatType.Damage);
            float finalDamage = container.GetFinalStat(TestStatType.Damage);

            // Assert
            Assert.That(finalDamage, Is.EqualTo(100f), "All modifiers removed, should return to base");
        }

        [Test]
        public void StatContainer_ClearAllModifiers_ResetsAllStats()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100f);
            container.SetBaseStat(TestStatType.Damage, 50f);

            container.AddModifier(new StatModifier<TestStatType, float>("health_mod", TestStatType.Health, 50f, StatModifierType.Additive));
            container.AddModifier(new StatModifier<TestStatType, float>("damage_mod", TestStatType.Damage, 25f, StatModifierType.Additive));

            // Act
            container.ClearAllModifiers();

            // Assert
            Assert.That(container.GetFinalStat(TestStatType.Health), Is.EqualTo(100f), "Health should return to base");
            Assert.That(container.GetFinalStat(TestStatType.Damage), Is.EqualTo(50f), "Damage should return to base");
        }

        [Test]
        public void StatContainer_Serialization_RoundTrip()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100f);
            container.SetBaseStat(TestStatType.Damage, 50f);
            container.SetBaseStat(TestStatType.Defense, 25f);

            // Act: Serialize
            byte[] data = container.Serialize();

            // Act: Deserialize into new container
            var newContainer = new StatContainer<TestStatType, float>();
            newContainer.Deserialize(data);

            // Assert
            Assert.That(newContainer.GetBaseStat(TestStatType.Health), Is.EqualTo(100f), "Health should survive serialization");
            Assert.That(newContainer.GetBaseStat(TestStatType.Damage), Is.EqualTo(50f), "Damage should survive serialization");
            Assert.That(newContainer.GetBaseStat(TestStatType.Defense), Is.EqualTo(25f), "Defense should survive serialization");
        }

        [Test]
        public void StatContainer_MultipleStatTypes_IndependentModifiers()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100f);
            container.SetBaseStat(TestStatType.Damage, 50f);

            var healthModifier = new StatModifier<TestStatType, float>("health_bonus", TestStatType.Health, 50f, StatModifierType.Additive);
            var damageModifier = new StatModifier<TestStatType, float>("damage_bonus", TestStatType.Damage, 25f, StatModifierType.Additive);

            // Act
            container.AddModifier(healthModifier);
            container.AddModifier(damageModifier);

            // Assert
            Assert.That(container.GetFinalStat(TestStatType.Health), Is.EqualTo(150f), "Health modifier independent");
            Assert.That(container.GetFinalStat(TestStatType.Damage), Is.EqualTo(75f), "Damage modifier independent");
        }

        [Test]
        public void StatContainer_ModifierPriority_OrdersCorrectly()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Damage, 100f);

            // Lower priority modifier
            var lowPriorityMult = new StatModifier<TestStatType, float>(
                "low_priority", TestStatType.Damage, 0.5f, StatModifierType.Multiplicative, priority: 1);

            // Higher priority modifier (applied last)
            var highPriorityMult = new StatModifier<TestStatType, float>(
                "high_priority", TestStatType.Damage, 0.2f, StatModifierType.Multiplicative, priority: 10);

            // Act
            container.AddModifier(highPriorityMult); // Add in reverse order to test priority
            container.AddModifier(lowPriorityMult);
            float finalDamage = container.GetFinalStat(TestStatType.Damage);

            // Assert
            // Low priority first: 100 * (1 + 0.5) = 150
            // High priority second: 150 * (1 + 0.2) = 180
            Assert.That(finalDamage, Is.EqualTo(180f).Within(0.01f), "Priority should order modifiers");
        }

        [Test]
        public void StatContainer_GenericTypes_IntValues()
        {
            // Arrange: Test with int value type
            var container = new StatContainer<TestStatType, int>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100);

            var modifier = new StatModifier<TestStatType, int>("bonus", TestStatType.Health, 50, StatModifierType.Additive);

            // Act
            container.AddModifier(modifier);
            int finalHealth = container.GetFinalStat(TestStatType.Health);

            // Assert
            Assert.That(finalHealth, Is.EqualTo(150), "Int stats should work correctly");
        }

        [Test]
        public void StatContainer_GetAllStatTypes_ReturnsAllStats()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Health, 100f);
            container.SetBaseStat(TestStatType.Damage, 50f);
            container.SetBaseStat(TestStatType.Defense, 25f);

            // Act
            var allStats = new System.Collections.Generic.List<TestStatType>(container.GetAllStatTypes());

            // Assert
            Assert.That(allStats.Count, Is.EqualTo(3), "Should return 3 stats");
            Assert.That(allStats, Contains.Item(TestStatType.Health));
            Assert.That(allStats, Contains.Item(TestStatType.Damage));
            Assert.That(allStats, Contains.Item(TestStatType.Defense));
        }

        [Test]
        public void StatContainer_NegativeModifiers_ApplyCorrectly()
        {
            // Arrange
            var container = new StatContainer<TestStatType, float>();
            container.Initialize();
            container.SetBaseStat(TestStatType.Speed, 100f);

            var debuff = new StatModifier<TestStatType, float>(
                "slow_debuff", TestStatType.Speed, -30f, StatModifierType.Additive);

            // Act
            container.AddModifier(debuff);
            float finalSpeed = container.GetFinalStat(TestStatType.Speed);

            // Assert
            Assert.That(finalSpeed, Is.EqualTo(70f), "Negative modifiers should reduce stat: 100 - 30 = 70");
        }
    }
}
