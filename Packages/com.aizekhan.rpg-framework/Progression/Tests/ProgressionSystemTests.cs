using NUnit.Framework;
using RPGFramework.Progression;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Progression.Tests
{
    [TestFixture]
    public class ProgressionSystemTests
    {
        // ==================== Experience & Leveling Tests ====================

        [Test]
        public void AddExperience_ValidAmount_IncreasesTotalXP()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);

            // Act
            var result = tracker.AddExperience(entityId: 1, xpAmount: 50);

            // Assert
            var expData = tracker.GetExperienceData(1);
            Assert.That(expData.TotalXP, Is.EqualTo(50));
        }

        [Test]
        public void AddExperience_MultipleGains_AccumulatesCorrectly()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);

            // Act
            tracker.AddExperience(entityId: 1, xpAmount: 30);
            tracker.AddExperience(entityId: 1, xpAmount: 20);
            var result = tracker.AddExperience(entityId: 1, xpAmount: 50);

            // Assert
            var expData = tracker.GetExperienceData(1);
            Assert.That(expData.TotalXP, Is.EqualTo(100));
        }

        [Test]
        public void AddExperience_EnoughForLevelUp_IncreasesLevel()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);

            // Act
            var result = tracker.AddExperience(entityId: 1, xpAmount: 100);

            // Assert
            Assert.That(result.LeveledUp, Is.True);
            Assert.That(result.NewLevel, Is.EqualTo(2));
            Assert.That(result.OldLevel, Is.EqualTo(1));
        }

        [Test]
        public void AddExperience_EnoughForMultipleLevels_GainsAllLevels()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);

            // Act
            var result = tracker.AddExperience(entityId: 1, xpAmount: 600); // Should reach level 4

            // Assert
            Assert.That(result.LeveledUp, Is.True);
            Assert.That(result.NewLevel, Is.EqualTo(4));
            Assert.That(result.OldLevel, Is.EqualTo(1));
            Assert.That(result.LevelsGained, Is.EqualTo(3));
        }

        [Test]
        public void AddExperience_AtMaxLevel_CapsXP()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 5, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);
            tracker.SetLevel(1, 5); // Set to max level

            // Act
            var result = tracker.AddExperience(entityId: 1, xpAmount: 1000);

            // Assert
            Assert.That(result.LeveledUp, Is.False);
            var expData = tracker.GetExperienceData(1);
            Assert.That(expData.Level, Is.EqualTo(5));
        }

        [Test]
        public void SetLevel_ValidLevel_UpdatesEntityLevel()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);

            // Act
            tracker.SetLevel(entityId: 1, level: 5);

            // Assert
            var expData = tracker.GetExperienceData(1);
            Assert.That(expData.Level, Is.EqualTo(5));
            Assert.That(expData.TotalXP, Is.EqualTo(1000)); // 100 + 200 + 300 + 400
        }

        [Test]
        public void GetExperienceData_NewEntity_ReturnsLevelOne()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);

            // Act
            var expData = tracker.GetExperienceData(entityId: 1);

            // Assert
            Assert.That(expData.Level, Is.EqualTo(1));
            Assert.That(expData.CurrentXP, Is.EqualTo(0));
            Assert.That(expData.TotalXP, Is.EqualTo(0));
            Assert.That(expData.XPToNextLevel, Is.EqualTo(100));
        }

        [Test]
        public void ExperienceData_ProgressToNextLevel_CalculatesCorrectly()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);

            // Act
            tracker.AddExperience(entityId: 1, xpAmount: 50); // 50/100 = 50%
            var expData = tracker.GetExperienceData(1);

            // Assert
            Assert.That(expData.ProgressToNextLevel, Is.EqualTo(0.5f).Within(0.01f));
        }

        // ==================== Level Table Tests ====================

        [Test]
        public void LinearLevelTable_CalculatesCorrectXP()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);

            // Act & Assert
            Assert.That(levelTable.GetXPForNextLevel(1), Is.EqualTo(100));
            Assert.That(levelTable.GetXPForNextLevel(2), Is.EqualTo(200));
            Assert.That(levelTable.GetXPForNextLevel(3), Is.EqualTo(300));
        }

        [Test]
        public void ExponentialLevelTable_CalculatesCorrectXP()
        {
            // Arrange
            var levelTable = LevelTable.CreateExponential(maxLevel: 10, baseXP: 100, multiplier: 1.5f);

            // Act & Assert
            // Level 2 total: baseXP * 1.5^1 = 150, so XP to level 2 = 150
            Assert.That(levelTable.GetXPForNextLevel(1), Is.EqualTo(150).Within(1));
            // Level 3 total: 150 + (baseXP * 1.5^2) = 150 + 225 = 375, so XP from level 2 to 3 = 225
            Assert.That(levelTable.GetXPForNextLevel(2), Is.EqualTo(225).Within(1));
        }

        [Test]
        public void PolynomialLevelTable_CalculatesCorrectXP()
        {
            // Arrange
            var levelTable = LevelTable.CreatePolynomial(maxLevel: 10, baseXP: 100, power: 2.0f);

            // Act & Assert
            Assert.That(levelTable.GetXPForNextLevel(1), Is.EqualTo(100));
            Assert.That(levelTable.GetXPForNextLevel(2), Is.EqualTo(400).Within(1));
            Assert.That(levelTable.GetXPForNextLevel(3), Is.EqualTo(900).Within(1));
        }

        [Test]
        public void CustomLevelTable_UsesProvidedXPValues()
        {
            // Arrange
            var customXP = new Dictionary<int, long>
            {
                { 1, 0 },
                { 2, 150 },
                { 3, 400 },
                { 4, 800 }
            };
            var levelTable = LevelTable.CreateCustom(customXP);

            // Act & Assert
            Assert.That(levelTable.GetXPForNextLevel(1), Is.EqualTo(150));
            Assert.That(levelTable.GetXPForNextLevel(2), Is.EqualTo(250)); // 400 - 150
            Assert.That(levelTable.GetXPForNextLevel(3), Is.EqualTo(400)); // 800 - 400
        }

        [Test]
        public void CalculateLevelFromXP_VariousXPAmounts_ReturnsCorrectLevel()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);

            // Act & Assert
            Assert.That(levelTable.CalculateLevelFromXP(0), Is.EqualTo(1));
            Assert.That(levelTable.CalculateLevelFromXP(50), Is.EqualTo(1));
            Assert.That(levelTable.CalculateLevelFromXP(100), Is.EqualTo(2));
            Assert.That(levelTable.CalculateLevelFromXP(300), Is.EqualTo(3));
            Assert.That(levelTable.CalculateLevelFromXP(600), Is.EqualTo(4));
        }

        [Test]
        public void GetLevelInfo_ValidLevel_ReturnsCorrectInfo()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);

            // Act
            var levelInfo = levelTable.GetLevelInfo(3);

            // Assert
            Assert.That(levelInfo.Level, Is.EqualTo(3));
            Assert.That(levelInfo.TotalXPRequired, Is.EqualTo(300)); // 100 + 200
            Assert.That(levelInfo.XPFromPrevious, Is.EqualTo(200)); // XP from level 2 to 3
        }

        // ==================== Stat Growth Tests ====================

        [Test]
        public void LinearStatGrowth_CalculatesCorrectly()
        {
            // Arrange
            var statGrowth = new StatGrowthData(
                statId: "Strength",
                baseValue: 10f,
                growthPerLevel: 2f,
                growthPercentage: 0f,
                curveType: GrowthCurveType.Linear
            );

            // Act & Assert
            Assert.That(statGrowth.CalculateAtLevel(1), Is.EqualTo(10f));
            Assert.That(statGrowth.CalculateAtLevel(2), Is.EqualTo(12f));
            Assert.That(statGrowth.CalculateAtLevel(5), Is.EqualTo(18f));
        }

        [Test]
        public void ExponentialStatGrowth_CalculatesCorrectly()
        {
            // Arrange
            var statGrowth = new StatGrowthData(
                statId: "HP",
                baseValue: 100f,
                growthPerLevel: 10f,
                growthPercentage: 0.1f,
                curveType: GrowthCurveType.Exponential
            );

            // Act
            float level1 = statGrowth.CalculateAtLevel(1);
            float level2 = statGrowth.CalculateAtLevel(2);
            float level3 = statGrowth.CalculateAtLevel(3);

            // Assert
            Assert.That(level1, Is.EqualTo(100f));
            Assert.That(level2, Is.EqualTo(121f).Within(0.1f)); // 100 + 10 = 110, 110 * 1.1 = 121
            Assert.That(level3, Is.EqualTo(144.1f).Within(0.1f)); // 121 + 10 = 131, 131 * 1.1 = 144.1
        }

        [Test]
        public void PolynomialStatGrowth_CalculatesCorrectly()
        {
            // Arrange
            var statGrowth = new StatGrowthData(
                statId: "Damage",
                baseValue: 20f,
                growthPerLevel: 3f,
                growthPercentage: 0f,
                curveType: GrowthCurveType.Polynomial
            );

            // Act & Assert
            Assert.That(statGrowth.CalculateAtLevel(1), Is.EqualTo(20f));
            Assert.That(statGrowth.CalculateAtLevel(2), Is.EqualTo(23f)); // 20 + 3*1
            Assert.That(statGrowth.CalculateAtLevel(3), Is.EqualTo(32f)); // 20 + 3*4
            Assert.That(statGrowth.CalculateAtLevel(4), Is.EqualTo(47f)); // 20 + 3*9
        }

        [Test]
        public void StatGrowthFormula_RegisterStatGrowth_StoresCorrectly()
        {
            // Arrange
            var formula = new StatGrowthFormula();
            var growth = new StatGrowthData("Strength", 10f, 2f, 0f, GrowthCurveType.Linear);

            // Act
            formula.RegisterStatGrowth(growth);

            // Assert
            Assert.That(formula.HasStatGrowth("Strength"), Is.True);
            Assert.That(formula.HasStatGrowth("Dexterity"), Is.False);
        }

        [Test]
        public void StatGrowthFormula_CalculateStatAtLevel_ReturnsCorrectValue()
        {
            // Arrange
            var formula = new StatGrowthFormula();
            formula.RegisterStatGrowth(new StatGrowthData("Strength", 10f, 2f, 0f, GrowthCurveType.Linear));

            // Act
            float value = formula.CalculateStatAtLevel("Strength", 5);

            // Assert
            Assert.That(value, Is.EqualTo(18f)); // 10 + 2*4
        }

        [Test]
        public void StatGrowthFormula_GetStatChangesForLevelUp_ReturnsAllStats()
        {
            // Arrange
            var formula = new StatGrowthFormula();
            formula.RegisterStatGrowth(new StatGrowthData("Strength", 10f, 2f, 0f, GrowthCurveType.Linear));
            formula.RegisterStatGrowth(new StatGrowthData("HP", 100f, 10f, 0.1f, GrowthCurveType.Exponential));

            // Act
            var stats = formula.GetStatChangesForLevelUp(1, 3);

            // Assert
            Assert.That(stats.Count, Is.EqualTo(2));
            Assert.That(stats["Strength"], Is.EqualTo(14f)); // 10 + 2*2
            Assert.That(stats.ContainsKey("HP"), Is.True);
        }

        [Test]
        public void StatGrowthFormula_Profiles_OverrideSharedGrowthPerEntity()
        {
            var formula = new StatGrowthFormula();
            formula.RegisterStatGrowth(new StatGrowthData("Strength", 10f, 2f));
            formula.RegisterStatGrowth("rogue", new StatGrowthData("Strength", 6f, 1f));
            formula.RegisterStatGrowth("rogue", new StatGrowthData("Agility", 12f, 3f));
            formula.AssignProfile(7, "rogue");

            Assert.That(formula.GetProfile(7), Is.EqualTo("rogue"));
            Assert.That(formula.GetProfile(8), Is.Null);
            Assert.That(formula.CalculateStatAtLevel(7, "Strength", 5), Is.EqualTo(10f));
            Assert.That(formula.CalculateStatAtLevel(8, "Strength", 5), Is.EqualTo(18f));
            Assert.That(formula.CalculateStatAtLevel("Strength", 5), Is.EqualTo(18f), "the shared table is untouched");
            Assert.That(formula.HasStatGrowth(7, "Agility"), Is.True);
            Assert.That(formula.HasStatGrowth(8, "Agility"), Is.False);
            Assert.That(formula.HasStatGrowth("Agility"), Is.False);
            Assert.That(formula.GetStatChangesForLevelUp(7, 1, 2),
                Is.EquivalentTo(new Dictionary<string, float> { ["Strength"] = 7f, ["Agility"] = 15f }));

            formula.AssignProfile(7, null);

            Assert.That(formula.GetProfile(7), Is.Null);
            Assert.That(formula.CalculateStatAtLevel(7, "Strength", 5), Is.EqualTo(18f));
        }

        // ==================== Unlock System Tests ====================

        [Test]
        public void UnlockManager_RegisterUnlock_StoresCorrectly()
        {
            // Arrange
            var unlockManager = new UnlockManager();
            var unlock = new UnlockData("fireball", "Fireball", 3, "Ability");

            // Act
            unlockManager.RegisterUnlock(unlock);

            // Assert
            Assert.That(unlockManager.IsUnlockAvailable("fireball", 3), Is.True);
        }

        [Test]
        public void UnlockManager_GetUnlocksForLevel_ReturnsCorrectUnlocks()
        {
            // Arrange
            var unlockManager = new UnlockManager();
            unlockManager.RegisterUnlock(new UnlockData("fireball", "Fireball", 3, "Ability"));
            unlockManager.RegisterUnlock(new UnlockData("ice_armor", "Ice Armor", 5, "Ability"));
            unlockManager.RegisterUnlock(new UnlockData("dash", "Dash", 3, "Feature"));

            // Act
            var level3Unlocks = unlockManager.GetUnlocksForLevel(3);

            // Assert
            Assert.That(level3Unlocks.Count, Is.EqualTo(2));
            Assert.That(level3Unlocks.Any(u => u.UnlockId == "fireball"), Is.True);
            Assert.That(level3Unlocks.Any(u => u.UnlockId == "dash"), Is.True);
        }

        [Test]
        public void UnlockManager_AchieveUnlock_MarksAsUnlocked()
        {
            // Arrange
            var unlockManager = new UnlockManager();
            unlockManager.RegisterUnlock(new UnlockData("fireball", "Fireball", 3, "Ability"));

            // Act
            unlockManager.AchieveUnlock(entityId: 1, unlockId: "fireball");

            // Assert
            Assert.That(unlockManager.HasUnlocked(entityId: 1, unlockId: "fireball"), Is.True);
            Assert.That(unlockManager.HasUnlocked(entityId: 2, unlockId: "fireball"), Is.False);
        }

        [Test]
        public void UnlockManager_GetUnlockedContent_ReturnsAllUnlockIds()
        {
            // Arrange
            var unlockManager = new UnlockManager();
            unlockManager.RegisterUnlock(new UnlockData("fireball", "Fireball", 3, "Ability"));
            unlockManager.RegisterUnlock(new UnlockData("dash", "Dash", 3, "Feature"));

            // Act
            unlockManager.AchieveUnlock(entityId: 1, unlockId: "fireball");
            unlockManager.AchieveUnlock(entityId: 1, unlockId: "dash");
            var unlockIds = unlockManager.GetUnlockedContent(1);

            // Assert
            Assert.That(unlockIds.Count, Is.EqualTo(2));
            Assert.That(unlockIds.Contains("fireball"), Is.True);
            Assert.That(unlockIds.Contains("dash"), Is.True);
        }

        // ==================== Integration Tests ====================

        [Test]
        public void Integration_LevelUpWithUnlocks_GrantsCorrectUnlocks()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);
            var unlockManager = new UnlockManager();
            unlockManager.RegisterUnlock(new UnlockData("fireball", "Fireball", 3, "Ability"));

            // Act
            tracker.AddExperience(entityId: 1, xpAmount: 300); // Level 3
            var unlocksForLevel = unlockManager.GetUnlocksForLevel(3);
            foreach (var unlock in unlocksForLevel)
            {
                unlockManager.AchieveUnlock(entityId: 1, unlock.UnlockId);
            }

            // Assert
            Assert.That(tracker.GetLevel(1), Is.EqualTo(3));
            Assert.That(unlockManager.HasUnlocked(entityId: 1, unlockId: "fireball"), Is.True);
        }

        [Test]
        public void Integration_StatGrowthOnLevelUp_CalculatesCorrectStats()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);
            var statFormula = new StatGrowthFormula();
            statFormula.RegisterStatGrowth(new StatGrowthData("Strength", 10f, 2f, 0f, GrowthCurveType.Linear));
            statFormula.RegisterStatGrowth(new StatGrowthData("HP", 100f, 10f, 0f, GrowthCurveType.Linear));

            // Act
            tracker.AddExperience(entityId: 1, xpAmount: 300); // Level 3
            var stats = statFormula.GetStatChangesForLevelUp(1, tracker.GetLevel(1));

            // Assert
            Assert.That(stats["Strength"], Is.EqualTo(14f)); // 10 + 2*2
            Assert.That(stats["HP"], Is.EqualTo(120f)); // 100 + 10*2
        }

        [Test]
        public void Integration_CompleteProgression_WorksEndToEnd()
        {
            // Arrange
            var levelTable = LevelTable.CreateLinear(maxLevel: 10, baseXP: 100);
            var tracker = new ProgressionTracker(levelTable);
            var statFormula = new StatGrowthFormula();
            statFormula.RegisterStatGrowth(new StatGrowthData("Strength", 10f, 2f, 0f, GrowthCurveType.Linear));
            var unlockManager = new UnlockManager();
            unlockManager.RegisterUnlock(new UnlockData("fireball", "Fireball", 3, "Ability"));
            unlockManager.RegisterUnlock(new UnlockData("lightning", "Lightning", 5, "Ability"));

            // Act - Level to 5
            var result = tracker.AddExperience(entityId: 1, xpAmount: 1000);

            // Grant unlocks for achieved levels
            for (int level = 2; level <= result.NewLevel; level++)
            {
                var unlocksForLevel = unlockManager.GetUnlocksForLevel(level);
                foreach (var unlock in unlocksForLevel)
                {
                    unlockManager.AchieveUnlock(entityId: 1, unlock.UnlockId);
                }
            }

            // Assert
            Assert.That(tracker.GetLevel(1), Is.EqualTo(5));
            Assert.That(statFormula.CalculateStatAtLevel("Strength", 5), Is.EqualTo(18f));
            Assert.That(unlockManager.HasUnlocked(entityId: 1, "fireball"), Is.True);
            Assert.That(unlockManager.HasUnlocked(entityId: 1, "lightning"), Is.True);
        }
    }
}
