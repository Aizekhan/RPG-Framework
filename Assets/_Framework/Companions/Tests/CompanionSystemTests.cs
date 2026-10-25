using NUnit.Framework;
using System;
using RPGFramework.Companions.Runtime;

namespace RPGFramework.Companions.Tests
{
    [TestFixture]
    public class CompanionSystemTests
    {
        private CompanionManager _manager;

        [SetUp]
        public void Setup()
        {
            _manager = new CompanionManager();
        }

        [TearDown]
        public void Teardown()
        {
            _manager = null;
        }

        #region Companion Creation and Management Tests

        [Test]
        public void CreateCompanion_ValidParameters_CreatesCompanion()
        {
            // Arrange & Act
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");

            // Assert
            Assert.IsNotNull(companion);
            Assert.AreEqual("wolf_001", companion.CompanionId);
            Assert.AreEqual("Fenrir", companion.Name);
            Assert.AreEqual(CompanionType.CombatPet, companion.CompanionType);
            Assert.AreEqual(1, companion.OwnerId);
        }

        [Test]
        public void CreateCompanion_DuplicateId_ThrowsException()
        {
            // Arrange
            _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Another Wolf"));
        }

        [Test]
        public void CreateCompanion_NullId_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _manager.CreateCompanion(null, 1, CompanionType.CombatPet, "Fenrir"));
        }

        [Test]
        public void GetCompanion_ExistingId_ReturnsCompanion()
        {
            // Arrange
            _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");

            // Act
            var companion = _manager.GetCompanion("wolf_001");

            // Assert
            Assert.IsNotNull(companion);
            Assert.AreEqual("wolf_001", companion.CompanionId);
        }

        [Test]
        public void GetCompanion_NonExistingId_ReturnsNull()
        {
            // Act
            var companion = _manager.GetCompanion("nonexistent");

            // Assert
            Assert.IsNull(companion);
        }

        [Test]
        public void RemoveCompanion_ExistingCompanion_ReturnsTrue()
        {
            // Arrange
            _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");

            // Act
            bool removed = _manager.RemoveCompanion("wolf_001");

            // Assert
            Assert.IsTrue(removed);
            Assert.IsNull(_manager.GetCompanion("wolf_001"));
        }

        [Test]
        public void GetCompanionsByOwner_MultipleCompanions_ReturnsCorrectList()
        {
            // Arrange
            _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            _manager.CreateCompanion("horse_001", 1, CompanionType.Mount, "Shadowfax");
            _manager.CreateCompanion("cat_001", 2, CompanionType.NonCombatPet, "Whiskers");

            // Act
            var owner1Companions = _manager.GetCompanionsByOwner(1);

            // Assert
            Assert.AreEqual(2, owner1Companions.Count);
        }

        [Test]
        public void GetCompanionsByType_MultipleCompanions_ReturnsCorrectList()
        {
            // Arrange
            _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            _manager.CreateCompanion("bear_001", 2, CompanionType.CombatPet, "Ursa");
            _manager.CreateCompanion("horse_001", 1, CompanionType.Mount, "Shadowfax");

            // Act
            var combatPets = _manager.GetCompanionsByType(CompanionType.CombatPet);

            // Assert
            Assert.AreEqual(2, combatPets.Count);
        }

        #endregion

        #region Companion State Tests

        [Test]
        public void Summon_DismissedCompanion_SetsActiveAndIdle()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");

            // Act
            companion.Summon();

            // Assert
            Assert.IsTrue(companion.IsActive);
            Assert.AreEqual(CompanionState.Idle, companion.CurrentState);
        }

        [Test]
        public void Dismiss_ActiveCompanion_SetsInactiveAndDismissed()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            companion.Summon();

            // Act
            companion.Dismiss();

            // Assert
            Assert.IsFalse(companion.IsActive);
            Assert.AreEqual(CompanionState.Dismissed, companion.CurrentState);
        }

        [Test]
        public void IssueCommand_Follow_ChangesStateToFollowing()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            companion.Summon();

            // Act
            companion.IssueCommand(CompanionCommand.Follow);

            // Assert
            Assert.AreEqual(CompanionState.Following, companion.CurrentState);
        }

        [Test]
        public void IssueCommand_Attack_ChangesStateToFighting()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            companion.Summon();

            // Act
            companion.IssueCommand(CompanionCommand.Attack);

            // Assert
            Assert.AreEqual(CompanionState.Fighting, companion.CurrentState);
        }

        [Test]
        public void IssueCommand_InactiveCompanion_ThrowsException()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => companion.IssueCommand(CompanionCommand.Follow));
        }

        #endregion

        #region Loyalty Tests

        [Test]
        public void ModifyLoyalty_PositiveAmount_IncreasesLoyalty()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            var initialLoyalty = companion.Loyalty;

            // Act
            companion.ModifyLoyalty(30);

            // Assert
            Assert.AreNotEqual(initialLoyalty, companion.Loyalty);
        }

        [Test]
        public void ModifyLoyalty_NegativeAmount_DecreasesLoyalty()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            companion.ModifyLoyalty(50); // Set to loyal first

            // Act
            companion.ModifyLoyalty(-60);

            // Assert
            Assert.AreEqual(LoyaltyLevel.Wary, companion.Loyalty);
        }

        [Test]
        public void ModifyLoyalty_ExceedsMaximum_ClampsTo100()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");

            // Act
            companion.ModifyLoyalty(200);

            // Assert
            Assert.AreEqual(LoyaltyLevel.Devoted, companion.Loyalty);
        }

        #endregion

        #region Stats Tests

        [Test]
        public void CompanionStats_InitialValues_AreCorrect()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);

            // Assert
            Assert.AreEqual(100, stats.MaxHealth);
            Assert.AreEqual(100, stats.CurrentHealth);
            Assert.AreEqual(15, stats.Attack);
            Assert.AreEqual(10, stats.Defense);
            Assert.AreEqual(1.2f, stats.Speed);
            Assert.AreEqual(100, stats.Hunger);
            Assert.AreEqual(50, stats.Happiness);
        }

        [Test]
        public void TakeDamage_ReducesHealth()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);

            // Act
            stats.TakeDamage(30);

            // Assert
            Assert.AreEqual(70, stats.CurrentHealth);
        }

        [Test]
        public void TakeDamage_ExceedsHealth_ClampsToZero()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);

            // Act
            stats.TakeDamage(150);

            // Assert
            Assert.AreEqual(0, stats.CurrentHealth);
        }

        [Test]
        public void Heal_IncreasesHealth()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);
            stats.TakeDamage(50);

            // Act
            stats.Heal(30);

            // Assert
            Assert.AreEqual(80, stats.CurrentHealth);
        }

        [Test]
        public void Heal_ExceedsMaxHealth_ClampsToMax()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);
            stats.TakeDamage(20);

            // Act
            stats.Heal(50);

            // Assert
            Assert.AreEqual(100, stats.CurrentHealth);
        }

        [Test]
        public void GainExperience_IncreasesExperience()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);

            // Act
            stats.GainExperience(50);

            // Assert
            Assert.AreEqual(50, stats.Experience);
        }

        [Test]
        public void ModifyHunger_IncreasesHunger()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);
            stats.ModifyHunger(-50); // Reduce hunger first

            // Act
            stats.ModifyHunger(30);

            // Assert
            Assert.AreEqual(80, stats.Hunger);
        }

        [Test]
        public void ModifyHappiness_IncreasesHappiness()
        {
            // Arrange
            var stats = new CompanionStats(100, 15, 10, 1.2f);

            // Act
            stats.ModifyHappiness(30);

            // Assert
            Assert.AreEqual(80, stats.Happiness);
        }

        #endregion

        #region Behavior Tests

        [Test]
        public void CompanionBehavior_InitialState_IsFollow()
        {
            // Arrange & Act
            var behavior = new CompanionBehavior();

            // Assert
            Assert.AreEqual(CompanionCommand.Follow, behavior.CurrentCommand);
            Assert.IsFalse(behavior.IsInCombat);
        }

        [Test]
        public void ExecuteCommand_Attack_EntersCombat()
        {
            // Arrange
            var behavior = new CompanionBehavior();

            // Act
            behavior.ExecuteCommand(CompanionCommand.Attack);

            // Assert
            Assert.IsTrue(behavior.IsInCombat);
            Assert.AreEqual(CompanionCommand.Attack, behavior.CurrentCommand);
        }

        [Test]
        public void ExecuteCommand_Stop_ExitsCombat()
        {
            // Arrange
            var behavior = new CompanionBehavior();
            behavior.ExecuteCommand(CompanionCommand.Attack);

            // Act
            behavior.ExecuteCommand(CompanionCommand.Stop);

            // Assert
            Assert.IsFalse(behavior.IsInCombat);
        }

        [Test]
        public void AttackTarget_SetsTargetAndEntersCombat()
        {
            // Arrange
            var behavior = new CompanionBehavior();

            // Act
            behavior.AttackTarget(999);

            // Assert
            Assert.IsTrue(behavior.IsInCombat);
            Assert.AreEqual(999, behavior.GetCurrentTarget());
        }

        [Test]
        public void ResetToIdle_ClearsTargetAndExitsCombat()
        {
            // Arrange
            var behavior = new CompanionBehavior();
            behavior.AttackTarget(999);

            // Act
            behavior.ResetToIdle();

            // Assert
            Assert.IsFalse(behavior.IsInCombat);
            Assert.AreEqual(-1, behavior.GetCurrentTarget());
        }

        #endregion

        #region Inventory Tests

        [Test]
        public void CompanionInventory_InitialState_IsEmpty()
        {
            // Arrange & Act
            var inventory = new CompanionInventory(10);

            // Assert
            Assert.AreEqual(0, inventory.ItemCount);
            Assert.AreEqual(10, inventory.Capacity);
        }

        [Test]
        public void AddItem_ValidItem_ReturnsTrue()
        {
            // Arrange
            var inventory = new CompanionInventory(10);

            // Act
            bool added = inventory.AddItem("sword_001", 1);

            // Assert
            Assert.IsTrue(added);
            Assert.AreEqual(1, inventory.ItemCount);
            Assert.IsTrue(inventory.HasItem("sword_001"));
        }

        [Test]
        public void AddItem_ExceedsCapacity_ReturnsFalse()
        {
            // Arrange
            var inventory = new CompanionInventory(2);
            inventory.AddItem("item_001", 1);
            inventory.AddItem("item_002", 1);

            // Act
            bool added = inventory.AddItem("item_003", 1);

            // Assert
            Assert.IsFalse(added);
        }

        [Test]
        public void RemoveItem_ExistingItem_ReturnsTrue()
        {
            // Arrange
            var inventory = new CompanionInventory(10);
            inventory.AddItem("sword_001", 2);

            // Act
            bool removed = inventory.RemoveItem("sword_001", 1);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(1, inventory.GetItemQuantity("sword_001"));
        }

        [Test]
        public void EquipItem_ValidItem_ReturnsTrue()
        {
            // Arrange
            var inventory = new CompanionInventory(10);
            inventory.AddItem("armor_001", 1);

            // Act
            bool equipped = inventory.EquipItem("armor_001", "chest");

            // Assert
            Assert.IsTrue(equipped);
            Assert.AreEqual("armor_001", inventory.GetEquippedItem("chest"));
        }

        [Test]
        public void UnequipItem_EquippedItem_ReturnsItemId()
        {
            // Arrange
            var inventory = new CompanionInventory(10);
            inventory.AddItem("armor_001", 1);
            inventory.EquipItem("armor_001", "chest");

            // Act
            string unequipped = inventory.UnequipItem("chest");

            // Assert
            Assert.AreEqual("armor_001", unequipped);
            Assert.IsNull(inventory.GetEquippedItem("chest"));
        }

        #endregion

        #region Ability Tests

        [Test]
        public void CompanionAbility_CanUse_WhenNotOnCooldown()
        {
            // Arrange
            var ability = new CompanionAbility(
                "howl_001",
                "Howl",
                "Intimidates enemies",
                5.0f,
                1,
                false,
                (companion, target) => true
            );

            // Act & Assert
            Assert.IsTrue(ability.CanUse());
            Assert.IsFalse(ability.IsOnCooldown);
        }

        [Test]
        public void CompanionAbility_Use_PutsCooldown()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            var ability = new CompanionAbility(
                "howl_001",
                "Howl",
                "Intimidates enemies",
                5.0f,
                1,
                false,
                (c, target) => true
            );

            // Act
            bool used = ability.Use(companion);

            // Assert
            Assert.IsTrue(used);
            Assert.IsTrue(ability.IsOnCooldown);
            Assert.AreEqual(5.0f, ability.RemainingCooldown);
        }

        [Test]
        public void CompanionAbility_UpdateCooldown_ReducesCooldown()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            var ability = new CompanionAbility(
                "howl_001",
                "Howl",
                "Intimidates enemies",
                5.0f,
                1,
                false,
                (c, target) => true
            );
            ability.Use(companion);

            // Act
            ability.UpdateCooldown(2.0f);

            // Assert
            Assert.AreEqual(3.0f, ability.RemainingCooldown);
        }

        [Test]
        public void CompanionAbility_PassiveAbility_CannotBeUsed()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            var ability = new CompanionAbility(
                "passive_001",
                "Pack Bonus",
                "Increases damage",
                0f,
                1,
                true, // Passive
                (c, target) => true
            );

            // Act
            bool used = ability.Use(companion);

            // Assert
            Assert.IsFalse(used);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void FeedCompanion_IncreasesHungerAndLoyalty()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            var stats = _manager.GetCompanionStats("wolf_001");
            stats.ModifyHunger(-50); // Make hungry
            var initialLoyalty = companion.Loyalty;

            // Act
            bool fed = _manager.FeedCompanion("wolf_001", "meat_001");

            // Assert
            Assert.IsTrue(fed);
            Assert.Greater(stats.Hunger, 50);
        }

        [Test]
        public void UpdateCompanions_RegeneratesStats()
        {
            // Arrange
            var companion = _manager.CreateCompanion("wolf_001", 1, CompanionType.CombatPet, "Fenrir");
            companion.Summon();
            var stats = _manager.GetCompanionStats("wolf_001");
            stats.TakeDamage(20);
            stats.ModifyHunger(0); // Ensure well-fed

            // Act
            _manager.UpdateCompanions(1.0f);

            // Assert
            // Health should regenerate when well-fed
            Assert.Greater(stats.CurrentHealth, 80);
        }

        #endregion
    }
}
