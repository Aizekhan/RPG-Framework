// Packages/com.aizekhan.rpg-framework/Combat/Tests/DamageCalculatorTests.cs

using NUnit.Framework;
using RPGFramework.Combat;

namespace RPGFramework.Combat.Tests
{
    [TestFixture]
    public class DamageCalculatorTests
    {
        private IDamageCalculator _calculator;

        [SetUp]
        public void SetUp()
        {
            _calculator = new DamageCalculator();
        }

        #region Basic Damage Calculation Tests

        [Test]
        public void CalculateDamage_NoDamageModifiers_ReturnsBaseDamage()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense);

            // Assert
            Assert.That(result, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_ZeroDefense_ReturnsFullDamage()
        {
            // Arrange
            float baseDamage = 150f;
            float defense = 0f;

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense);

            // Assert
            Assert.That(result, Is.EqualTo(150f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_50Defense_Reduces33Percent()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 50f;
            // Expected: 100 * (100 / (100 + 50)) = 100 * 0.6667 = 66.67

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense);

            // Assert
            Assert.That(result, Is.EqualTo(66.67f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_100Defense_Reduces50Percent()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 100f;
            // Expected: 100 * (100 / (100 + 100)) = 100 * 0.5 = 50

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense);

            // Assert
            Assert.That(result, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_200Defense_Reduces66Percent()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 200f;
            // Expected: 100 * (100 / (100 + 200)) = 100 * 0.333 = 33.33

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense);

            // Assert
            Assert.That(result, Is.EqualTo(33.33f).Within(0.01f));
        }

        #endregion

        #region Critical Hit Tests

        [Test]
        public void CalculateDamage_CriticalHit2x_DoublesBaseDamage()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;
            bool isCritical = true;
            float critMultiplier = 2.0f;

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, isCritical, critMultiplier);

            // Assert
            Assert.That(result, Is.EqualTo(200f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_CriticalHit3x_TriplesDamage()
        {
            // Arrange
            float baseDamage = 50f;
            float defense = 0f;
            bool isCritical = true;
            float critMultiplier = 3.0f;

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, isCritical, critMultiplier);

            // Assert
            Assert.That(result, Is.EqualTo(150f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_CriticalWithDefense_AppliesBothModifiers()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 100f;
            bool isCritical = true;
            float critMultiplier = 2.0f;
            // Expected: (100 * 2.0) * (100 / (100 + 100)) = 200 * 0.5 = 100

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, isCritical, critMultiplier);

            // Assert
            Assert.That(result, Is.EqualTo(100f).Within(0.01f));
        }

        #endregion

        #region Damage Multiplier Tests

        [Test]
        public void CalculateDamage_SingleMultiplier_AppliesCorrectly()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;
            float[] multipliers = { 1.5f }; // +50% damage buff

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, false, 1.0f, multipliers);

            // Assert
            Assert.That(result, Is.EqualTo(150f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_MultipleMultipliers_StackMultiplicatively()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;
            float[] multipliers = { 1.2f, 1.3f }; // +20% and +30% buffs
            // Expected: 100 * 1.2 * 1.3 = 156

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, false, 1.0f, multipliers);

            // Assert
            Assert.That(result, Is.EqualTo(156f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_BlockMultiplier_ReducesDamageBy50Percent()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;
            float[] multipliers = { 0.5f }; // Block reduces damage to 50%

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, false, 1.0f, multipliers);

            // Assert
            Assert.That(result, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_CritAndBuffAndBlock_CombinesAllMultipliers()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;
            bool isCritical = true;
            float critMultiplier = 2.0f;
            float[] multipliers = { 1.5f, 0.5f }; // +50% buff, then 50% block
            // Expected: 100 * 2.0 * 1.5 * 0.5 = 150

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, isCritical, critMultiplier, multipliers);

            // Assert
            Assert.That(result, Is.EqualTo(150f).Within(0.01f));
        }

        #endregion

        #region Armor Penetration Tests

        [Test]
        public void CalculateDamage_ArmorPenetration_ReducesDefense()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 100f;
            float armorPen = 30f;
            // Effective defense: 100 - 30 = 70
            // Expected: 100 * (100 / (100 + 70)) = 100 * 0.588 = 58.82

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, false, 1.0f, null, armorPen);

            // Assert
            Assert.That(result, Is.EqualTo(58.82f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_ArmorPenExceedsDefense_ClampsTo0()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 50f;
            float armorPen = 80f;
            // Effective defense: max(0, 50 - 80) = 0
            // Expected: 100 * (100 / (100 + 0)) = 100

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense, false, 1.0f, null, armorPen);

            // Assert
            Assert.That(result, Is.EqualTo(100f).Within(0.01f));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void CalculateDamage_ZeroBaseDamage_ReturnsMinimumDamage()
        {
            // Arrange
            float baseDamage = 0f;
            float defense = 0f;

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense);

            // Assert (minimum damage is 1)
            Assert.That(result, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void CalculateDamage_VeryHighDefense_StillDealsSomeDamage()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 1000f;
            // Expected: 100 * (100 / (100 + 1000)) = 100 * 0.0909 = 9.09
            // But minimum damage is 1, so should be at least 1

            // Act
            float result = _calculator.CalculateDamage(baseDamage, defense);

            // Assert
            Assert.That(result, Is.GreaterThan(1f));
        }

        #endregion

        #region DamageResult Tests

        [Test]
        public void CalculateDamageWithResult_ReturnsCorrectFields()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 50f;
            float critChance = 1.0f; // 100% crit for deterministic test
            float critMultiplier = 2.0f;

            // Act
            DamageResult result = _calculator.CalculateDamageWithResult(
                baseDamage, defense, critChance, critMultiplier);

            // Assert
            Assert.That(result.BaseDamage, Is.EqualTo(100f));
            Assert.That(result.EffectiveDefense, Is.EqualTo(50f));
            Assert.That(result.IsCritical, Is.True); // 100% crit chance
            Assert.That(result.CriticalMultiplier, Is.EqualTo(2.0f));
            Assert.That(result.TotalMultiplier, Is.EqualTo(2.0f));
            Assert.That(result.FinalDamage, Is.GreaterThan(0f));
        }

        [Test]
        public void CalculateDamageWithResult_ZeroCritChance_NeverCrits()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;
            float critChance = 0.0f; // 0% crit chance
            float critMultiplier = 2.0f;

            // Act (run multiple times to ensure no crits)
            for (int i = 0; i < 10; i++)
            {
                DamageResult result = _calculator.CalculateDamageWithResult(
                    baseDamage, defense, critChance, critMultiplier);

                // Assert
                Assert.That(result.IsCritical, Is.False);
                Assert.That(result.CriticalMultiplier, Is.EqualTo(1.0f)); // No crit, so multiplier is 1.0
            }
        }

        #endregion

        #region Helper Method Tests

        [Test]
        public void CalculateEffectiveDefense_NoArmorPen_ReturnsOriginalDefense()
        {
            // Arrange
            float defense = 100f;
            float armorPen = 0f;

            // Act
            float result = _calculator.CalculateEffectiveDefense(defense, armorPen);

            // Assert
            Assert.That(result, Is.EqualTo(100f));
        }

        [Test]
        public void CalculateEffectiveDefense_WithArmorPen_SubtractsCorrectly()
        {
            // Arrange
            float defense = 100f;
            float armorPen = 30f;

            // Act
            float result = _calculator.CalculateEffectiveDefense(defense, armorPen);

            // Assert
            Assert.That(result, Is.EqualTo(70f));
        }

        [Test]
        public void CalculateEffectiveDefense_ArmorPenExceedsDefense_ReturnsZero()
        {
            // Arrange
            float defense = 50f;
            float armorPen = 100f;

            // Act
            float result = _calculator.CalculateEffectiveDefense(defense, armorPen);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void CalculateDamageReduction_ZeroDefense_ReturnsZeroReduction()
        {
            // Arrange
            float defense = 0f;

            // Act
            float result = _calculator.CalculateDamageReduction(defense);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void CalculateDamageReduction_100Defense_Returns50PercentReduction()
        {
            // Arrange
            float defense = 100f;
            // Expected: 100 / (100 + 100) = 0.5

            // Act
            float result = _calculator.CalculateDamageReduction(defense);

            // Assert
            Assert.That(result, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void CalculateDamageReduction_200Defense_Returns66PercentReduction()
        {
            // Arrange
            float defense = 200f;
            // Expected: 200 / (100 + 200) = 0.6667

            // Act
            float result = _calculator.CalculateDamageReduction(defense);

            // Assert
            Assert.That(result, Is.EqualTo(0.6667f).Within(0.001f));
        }

        #endregion

        #region Validation Tests

        [Test]
        public void CalculateDamage_NegativeBaseDamage_ThrowsException()
        {
            // Arrange
            float baseDamage = -10f;
            float defense = 0f;

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _calculator.CalculateDamage(baseDamage, defense));
        }

        [Test]
        public void CalculateDamage_NegativeDefense_ThrowsException()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = -50f;

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _calculator.CalculateDamage(baseDamage, defense));
        }

        [Test]
        public void CalculateDamage_NegativeArmorPen_ThrowsException()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 50f;
            float armorPen = -10f;

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _calculator.CalculateDamage(baseDamage, defense, false, 1.0f, null, armorPen));
        }

        [Test]
        public void CalculateDamage_NegativeCritMultiplier_ThrowsException()
        {
            // Arrange
            float baseDamage = 100f;
            float defense = 0f;
            float critMultiplier = -2.0f;

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _calculator.CalculateDamage(baseDamage, defense, true, critMultiplier));
        }

        #endregion
    }
}
