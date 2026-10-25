using NUnit.Framework;
using RPGFramework.Abilities;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Abilities.Tests
{
    /// <summary>
    /// Comprehensive test suite for RPG Framework Ability System.
    /// Tests ability definitions, instances, cooldowns, registry, and executor.
    /// </summary>
    [TestFixture]
    public class AbilitySystemTests
    {
        #region Ability Definition Tests (5 tests)

        [Test]
        public void CreateAbility_WithMetadata_StoresCorrectly()
        {
            // Arrange
            var metadata = new AbilityMetadata(
                "fireball",
                "Fireball",
                AbilityType.Active,
                cooldown: 5f,
                range: 10f,
                aoeRadius: 3f);

            // Act
            var ability = new Ability(metadata);

            // Assert
            Assert.That(ability.AbilityId, Is.EqualTo("fireball"), "AbilityId should match");
            Assert.That(ability.Name, Is.EqualTo("Fireball"), "Name should match");
            Assert.That(ability.Type, Is.EqualTo(AbilityType.Active), "Type should be Active");
            Assert.That(ability.Cooldown, Is.EqualTo(5f), "Cooldown should be 5 seconds");
            Assert.That(ability.Range, Is.EqualTo(10f), "Range should be 10 units");
            Assert.That(ability.Metadata.AoeRadius, Is.EqualTo(3f), "AoE radius should be 3 units");
        }

        [Test]
        public void CreateAbility_WithResourceCosts_ValidatesCorrectly()
        {
            // Arrange
            var metadata = new AbilityMetadata("heal", "Heal", AbilityType.Active);
            var costs = new List<ResourceCost>
            {
                new ResourceCost("MP", 50f),
                new ResourceCost("HP", 0.1f, isPercentage: true)
            };

            // Act
            var ability = new Ability(metadata, costs);

            // Assert
            Assert.That(ability.ResourceCosts.Count, Is.EqualTo(2), "Should have 2 resource costs");
            Assert.That(ability.ResourceCosts[0].ResourceType, Is.EqualTo("MP"), "First cost should be MP");
            Assert.That(ability.ResourceCosts[0].Amount, Is.EqualTo(50f), "MP cost should be 50");
            Assert.That(ability.ResourceCosts[1].IsPercentage, Is.True, "HP cost should be percentage");
        }

        [Test]
        public void CreateAbility_WithCastInfo_ReturnsExpectedValues()
        {
            // Arrange
            var metadata = new AbilityMetadata("channel_beam", "Channel Beam", AbilityType.Channeled);
            var castInfo = new CastInfo(
                castTime: 2f,
                channelDuration: 5f,
                isInterruptible: true,
                canMoveWhileCasting: false);

            // Act
            var ability = new Ability(metadata, castInfo: castInfo);

            // Assert
            Assert.That(ability.CastInfo.CastTime, Is.EqualTo(2f), "Cast time should be 2 seconds");
            Assert.That(ability.CastInfo.ChannelDuration, Is.EqualTo(5f), "Channel duration should be 5 seconds");
            Assert.That(ability.CastInfo.IsInterruptible, Is.True, "Should be interruptible");
            Assert.That(ability.CastInfo.CanMoveWhileCasting, Is.False, "Cannot move while casting");
        }

        [Test]
        public void CreateAbility_WithTargetType_MatchesDefinition()
        {
            // Arrange
            var metadata = new AbilityMetadata("bless", "Bless", AbilityType.Active);

            // Act
            var ability = new Ability(metadata, targetType: TargetType.Ally);

            // Assert
            Assert.That(ability.TargetType, Is.EqualTo(TargetType.Ally), "Target type should be Ally");
        }

        [Test]
        public void AbilityMetadata_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var metadata = new AbilityMetadata("passive_skill", "Passive Skill");

            // Assert
            Assert.That(metadata.Type, Is.EqualTo(AbilityType.Active), "Default type should be Active");
            Assert.That(metadata.Cooldown, Is.EqualTo(0f), "Default cooldown should be 0");
            Assert.That(metadata.Range, Is.EqualTo(0f), "Default range should be 0");
            Assert.That(metadata.MinLevel, Is.EqualTo(0), "Default min level should be 0");
            Assert.That(metadata.MaxCharges, Is.EqualTo(0), "Default max charges should be 0");
        }

        #endregion

        #region Ability Instance Tests (6 tests)

        [Test]
        public void CreateAbilityInstance_WithAbility_Initializes()
        {
            // Arrange
            var metadata = new AbilityMetadata("test_ability", "Test", AbilityType.Active, cooldown: 10f);
            var ability = new Ability(metadata);

            // Act
            var instance = new AbilityInstance(ability, ownerEntityId: 1);

            // Assert
            Assert.That(instance.Ability, Is.EqualTo(ability), "Ability reference should match");
            Assert.That(instance.OwnerEntityId, Is.EqualTo(1), "Owner entity ID should be 1");
            Assert.That(instance.IsCasting, Is.False, "Should not be casting initially");
            Assert.That(instance.RemainingCharges, Is.EqualTo(-1), "Should have unlimited charges by default");
        }

        [Test]
        public void AbilityInstance_CanUse_ReturnsTrueWhenReady()
        {
            // Arrange
            var metadata = new AbilityMetadata("ready_ability", "Ready", AbilityType.Active);
            var ability = new Ability(metadata);
            var instance = new AbilityInstance(ability, ownerEntityId: 1);

            // Act
            bool canUse = instance.CanUse(currentTime: 0f);

            // Assert
            Assert.That(canUse, Is.True, "Should be able to use when ready");
        }

        [Test]
        public void AbilityInstance_CanUse_ReturnsFalseOnCooldown()
        {
            // Arrange
            var metadata = new AbilityMetadata("cooldown_ability", "Cooldown", AbilityType.Active, cooldown: 10f);
            var ability = new Ability(metadata);
            var instance = new AbilityInstance(ability, ownerEntityId: 1);

            // Act
            instance.CompleteCast(currentTime: 0f); // Start cooldown
            bool canUse = instance.CanUse(currentTime: 5f); // 5 seconds later, still on cooldown

            // Assert
            Assert.That(canUse, Is.False, "Should not be able to use while on cooldown");
        }

        [Test]
        public void AbilityInstance_StartCast_SetsIsCasting()
        {
            // Arrange
            var metadata = new AbilityMetadata("cast_ability", "Cast", AbilityType.Active);
            var ability = new Ability(metadata, castInfo: new CastInfo(castTime: 2f));
            var instance = new AbilityInstance(ability, ownerEntityId: 1);

            // Act
            instance.StartCast(currentTime: 10f);

            // Assert
            Assert.That(instance.IsCasting, Is.True, "Should be casting");
            Assert.That(instance.CastStartTime, Is.EqualTo(10f), "Cast start time should be recorded");
        }

        [Test]
        public void AbilityInstance_CancelCast_ClearsState()
        {
            // Arrange
            var metadata = new AbilityMetadata("cancel_ability", "Cancel", AbilityType.Active);
            var ability = new Ability(metadata, castInfo: new CastInfo(castTime: 2f));
            var instance = new AbilityInstance(ability, ownerEntityId: 1);
            instance.StartCast(currentTime: 0f);

            // Act
            instance.CancelCast();

            // Assert
            Assert.That(instance.IsCasting, Is.False, "Should not be casting after cancel");
            Assert.That(instance.CastStartTime, Is.EqualTo(0f), "Cast start time should be reset");
        }

        [Test]
        public void AbilityInstance_CompleteCast_StartsCooldown()
        {
            // Arrange
            var metadata = new AbilityMetadata("complete_ability", "Complete", AbilityType.Active, cooldown: 15f);
            var ability = new Ability(metadata);
            var instance = new AbilityInstance(ability, ownerEntityId: 1);
            instance.StartCast(currentTime: 0f);

            // Act
            instance.CompleteCast(currentTime: 2f);

            // Assert
            Assert.That(instance.IsCasting, Is.False, "Should not be casting after complete");
            Assert.That(instance.CooldownState.CooldownDuration, Is.EqualTo(15f), "Cooldown duration should match");
            Assert.That(instance.CooldownState.StartTime, Is.EqualTo(2f), "Cooldown should start at completion time");
        }

        #endregion

        #region Cooldown Tests (5 tests)

        [Test]
        public void CooldownTracker_StartCooldown_RecordsCorrectly()
        {
            // Arrange
            var tracker = new CooldownTracker();

            // Act
            tracker.StartCooldown(entityId: 1, "fireball", duration: 5f, currentTime: 0f);

            // Assert
            Assert.That(tracker.IsOnCooldown(1, "fireball", currentTime: 0f), Is.True, "Should be on cooldown immediately");
            Assert.That(tracker.GetRemainingCooldown(1, "fireball", currentTime: 0f), Is.EqualTo(5f), "Full cooldown remaining");
        }

        [Test]
        public void CooldownTracker_IsOnCooldown_ReturnsTrueDuringCooldown()
        {
            // Arrange
            var tracker = new CooldownTracker();
            tracker.StartCooldown(entityId: 1, "heal", duration: 10f, currentTime: 0f);

            // Act & Assert
            Assert.That(tracker.IsOnCooldown(1, "heal", currentTime: 5f), Is.True, "Should be on cooldown at 5 seconds");
            Assert.That(tracker.IsOnCooldown(1, "heal", currentTime: 9.9f), Is.True, "Should be on cooldown at 9.9 seconds");
            Assert.That(tracker.IsOnCooldown(1, "heal", currentTime: 10f), Is.False, "Should not be on cooldown at 10 seconds");
        }

        [Test]
        public void CooldownTracker_GetRemainingCooldown_ReturnsCorrectTime()
        {
            // Arrange
            var tracker = new CooldownTracker();
            tracker.StartCooldown(entityId: 1, "shield", duration: 20f, currentTime: 0f);

            // Act & Assert
            Assert.That(tracker.GetRemainingCooldown(1, "shield", currentTime: 0f), Is.EqualTo(20f), "20 seconds remaining");
            Assert.That(tracker.GetRemainingCooldown(1, "shield", currentTime: 5f), Is.EqualTo(15f), "15 seconds remaining");
            Assert.That(tracker.GetRemainingCooldown(1, "shield", currentTime: 15f), Is.EqualTo(5f), "5 seconds remaining");
            Assert.That(tracker.GetRemainingCooldown(1, "shield", currentTime: 20f), Is.EqualTo(0f), "0 seconds remaining");
        }

        [Test]
        public void CooldownTracker_ClearCooldown_RemovesCooldown()
        {
            // Arrange
            var tracker = new CooldownTracker();
            tracker.StartCooldown(entityId: 1, "cleanse", duration: 30f, currentTime: 0f);
            Assert.That(tracker.IsOnCooldown(1, "cleanse", currentTime: 0f), Is.True, "Should be on cooldown initially");

            // Act
            tracker.ClearCooldown(entityId: 1, "cleanse");

            // Assert
            Assert.That(tracker.IsOnCooldown(1, "cleanse", currentTime: 0f), Is.False, "Should not be on cooldown after clear");
        }

        [Test]
        public void CooldownTracker_MultipleAbilities_TracksSeparately()
        {
            // Arrange
            var tracker = new CooldownTracker();

            // Act
            tracker.StartCooldown(entityId: 1, "fireball", duration: 5f, currentTime: 0f);
            tracker.StartCooldown(entityId: 1, "heal", duration: 10f, currentTime: 0f);
            tracker.StartCooldown(entityId: 2, "fireball", duration: 5f, currentTime: 0f);

            // Assert
            Assert.That(tracker.IsOnCooldown(1, "fireball", currentTime: 0f), Is.True, "Entity 1 fireball should be on cooldown");
            Assert.That(tracker.IsOnCooldown(1, "heal", currentTime: 0f), Is.True, "Entity 1 heal should be on cooldown");
            Assert.That(tracker.IsOnCooldown(2, "fireball", currentTime: 0f), Is.True, "Entity 2 fireball should be on cooldown");
            Assert.That(tracker.IsOnCooldown(2, "heal", currentTime: 0f), Is.False, "Entity 2 heal should not be on cooldown");
        }

        #endregion

        #region Resource Cost Tests (4 tests)

        [Test]
        public void ResourceCost_FlatAmount_CreatesCorrectly()
        {
            // Arrange & Act
            var cost = new ResourceCost("MP", 100f);

            // Assert
            Assert.That(cost.ResourceType, Is.EqualTo("MP"), "Resource type should be MP");
            Assert.That(cost.Amount, Is.EqualTo(100f), "Amount should be 100");
            Assert.That(cost.IsPercentage, Is.False, "Should be flat amount, not percentage");
        }

        [Test]
        public void ResourceCost_Percentage_CalculatesCorrectly()
        {
            // Arrange & Act
            var cost = new ResourceCost("HP", 0.25f, isPercentage: true);

            // Assert
            Assert.That(cost.ResourceType, Is.EqualTo("HP"), "Resource type should be HP");
            Assert.That(cost.Amount, Is.EqualTo(0.25f), "Amount should be 0.25 (25%)");
            Assert.That(cost.IsPercentage, Is.True, "Should be percentage");
        }

        [Test]
        public void MultipleResourceCosts_ValidateAll()
        {
            // Arrange
            var costs = new List<ResourceCost>
            {
                new ResourceCost("HP", 50f),
                new ResourceCost("MP", 100f),
                new ResourceCost("Stamina", 0.5f, isPercentage: true)
            };

            // Assert
            Assert.That(costs.Count, Is.EqualTo(3), "Should have 3 costs");
            Assert.That(costs[0].ResourceType, Is.EqualTo("HP"), "First cost should be HP");
            Assert.That(costs[1].ResourceType, Is.EqualTo("MP"), "Second cost should be MP");
            Assert.That(costs[2].IsPercentage, Is.True, "Third cost should be percentage");
        }

        [Test]
        public void Ability_WithMultipleCosts_StoresAll()
        {
            // Arrange
            var metadata = new AbilityMetadata("expensive_spell", "Expensive Spell", AbilityType.Active);
            var costs = new List<ResourceCost>
            {
                new ResourceCost("HP", 200f),
                new ResourceCost("MP", 500f)
            };

            // Act
            var ability = new Ability(metadata, costs);

            // Assert
            Assert.That(ability.ResourceCosts.Count, Is.EqualTo(2), "Should store both costs");
            var totalHPCost = ability.ResourceCosts.Where(c => c.ResourceType == "HP").Sum(c => c.Amount);
            Assert.That(totalHPCost, Is.EqualTo(200f), "Total HP cost should be 200");
        }

        #endregion

        #region Registry Tests (4 tests)

        [Test]
        public void AbilityRegistry_RegisterAbility_AddsToRegistry()
        {
            // Arrange
            var registry = new AbilityRegistry();
            var metadata = new AbilityMetadata("registered_ability", "Registered", AbilityType.Active);
            var ability = new Ability(metadata);

            // Act
            registry.RegisterAbility(ability);

            // Assert
            Assert.That(registry.HasAbility("registered_ability"), Is.True, "Registry should contain ability");
        }

        [Test]
        public void AbilityRegistry_GetAbility_ReturnsRegistered()
        {
            // Arrange
            var registry = new AbilityRegistry();
            var metadata = new AbilityMetadata("fireball", "Fireball", AbilityType.Active);
            var ability = new Ability(metadata);
            registry.RegisterAbility(ability);

            // Act
            var retrieved = registry.GetAbility("fireball");

            // Assert
            Assert.That(retrieved, Is.Not.Null, "Should retrieve ability");
            Assert.That(retrieved.AbilityId, Is.EqualTo("fireball"), "AbilityId should match");
            Assert.That(retrieved.Name, Is.EqualTo("Fireball"), "Name should match");
        }

        [Test]
        public void AbilityRegistry_GetAbilitiesByType_FiltersCorrectly()
        {
            // Arrange
            var registry = new AbilityRegistry();
            registry.RegisterAbility(new Ability(new AbilityMetadata("active1", "Active1", AbilityType.Active)));
            registry.RegisterAbility(new Ability(new AbilityMetadata("active2", "Active2", AbilityType.Active)));
            registry.RegisterAbility(new Ability(new AbilityMetadata("passive1", "Passive1", AbilityType.Passive)));

            // Act
            var activeAbilities = registry.GetAbilitiesByType(AbilityType.Active);
            var passiveAbilities = registry.GetAbilitiesByType(AbilityType.Passive);

            // Assert
            Assert.That(activeAbilities.Count, Is.EqualTo(2), "Should have 2 active abilities");
            Assert.That(passiveAbilities.Count, Is.EqualTo(1), "Should have 1 passive ability");
        }

        [Test]
        public void AbilityRegistry_UnregisterAbility_Removes()
        {
            // Arrange
            var registry = new AbilityRegistry();
            var metadata = new AbilityMetadata("temp_ability", "Temp", AbilityType.Active);
            var ability = new Ability(metadata);
            registry.RegisterAbility(ability);
            Assert.That(registry.HasAbility("temp_ability"), Is.True, "Ability should be registered");

            // Act
            bool removed = registry.UnregisterAbility("temp_ability");

            // Assert
            Assert.That(removed, Is.True, "Should return true when removing");
            Assert.That(registry.HasAbility("temp_ability"), Is.False, "Ability should no longer be registered");
        }

        #endregion

        #region Target Validator Tests (5 tests)

        [Test]
        public void TargetValidator_SelfTarget_ValidatesCorrectly()
        {
            // Arrange
            var validator = new TargetValidator();
            var metadata = new AbilityMetadata("self_buff", "Self Buff", AbilityType.Active);
            var ability = new Ability(metadata, targetType: TargetType.Self);

            // Act
            bool validSelf = validator.ValidateTarget(ability, casterEntityId: 1, targetEntityId: 1, null, null);
            bool validNoTarget = validator.ValidateTarget(ability, casterEntityId: 1, targetEntityId: -1, null, null);
            bool invalidOther = validator.ValidateTarget(ability, casterEntityId: 1, targetEntityId: 2, null, null);

            // Assert
            Assert.That(validSelf, Is.True, "Self targeting self should be valid");
            Assert.That(validNoTarget, Is.True, "No target should be valid for self-cast");
            Assert.That(invalidOther, Is.False, "Targeting other entity should be invalid");
        }

        [Test]
        public void TargetValidator_EnemyTarget_RequiresEnemy()
        {
            // Arrange
            var validator = new TargetValidator();
            var metadata = new AbilityMetadata("attack", "Attack", AbilityType.Active);
            var ability = new Ability(metadata, targetType: TargetType.Enemy);

            // Act
            bool validEnemy = validator.ValidateTarget(
                ability,
                casterEntityId: 1,
                targetEntityId: 2,
                distanceCheck: null,
                relationshipCheck: (caster, target) => true); // true = enemy

            bool invalidAlly = validator.ValidateTarget(
                ability,
                casterEntityId: 1,
                targetEntityId: 3,
                distanceCheck: null,
                relationshipCheck: (caster, target) => false); // false = ally

            // Assert
            Assert.That(validEnemy, Is.True, "Enemy target should be valid");
            Assert.That(invalidAlly, Is.False, "Ally target should be invalid for enemy-only ability");
        }

        [Test]
        public void TargetValidator_AllyTarget_RequiresAlly()
        {
            // Arrange
            var validator = new TargetValidator();
            var metadata = new AbilityMetadata("heal", "Heal", AbilityType.Active);
            var ability = new Ability(metadata, targetType: TargetType.Ally);

            // Act
            bool validSelf = validator.ValidateTarget(
                ability,
                casterEntityId: 1,
                targetEntityId: 1,
                distanceCheck: null,
                relationshipCheck: null); // Self is always valid ally

            bool validAlly = validator.ValidateTarget(
                ability,
                casterEntityId: 1,
                targetEntityId: 2,
                distanceCheck: null,
                relationshipCheck: (caster, target) => false); // false = ally

            bool invalidEnemy = validator.ValidateTarget(
                ability,
                casterEntityId: 1,
                targetEntityId: 3,
                distanceCheck: null,
                relationshipCheck: (caster, target) => true); // true = enemy

            // Assert
            Assert.That(validSelf, Is.True, "Self should be valid ally");
            Assert.That(validAlly, Is.True, "Ally target should be valid");
            Assert.That(invalidEnemy, Is.False, "Enemy target should be invalid for ally-only ability");
        }

        [Test]
        public void TargetValidator_RangeCheck_EnforcesDistance()
        {
            // Arrange
            var validator = new TargetValidator();
            var metadata = new AbilityMetadata("ranged_attack", "Ranged Attack", AbilityType.Active, range: 10f);
            var ability = new Ability(metadata, targetType: TargetType.Enemy);

            // Act
            bool validInRange = validator.ValidateTarget(
                ability,
                casterEntityId: 1,
                targetEntityId: 2,
                distanceCheck: (caster, target) => 8f, // Within range
                relationshipCheck: (caster, target) => true);

            bool invalidOutOfRange = validator.ValidateTarget(
                ability,
                casterEntityId: 1,
                targetEntityId: 3,
                distanceCheck: (caster, target) => 15f, // Out of range
                relationshipCheck: (caster, target) => true);

            // Assert
            Assert.That(validInRange, Is.True, "Target within range should be valid");
            Assert.That(invalidOutOfRange, Is.False, "Target out of range should be invalid");
        }

        [Test]
        public void TargetValidator_NoTarget_AllowsSelfCast()
        {
            // Arrange
            var validator = new TargetValidator();
            var metadata = new AbilityMetadata("aoe_blast", "AoE Blast", AbilityType.Active);
            var ability = new Ability(metadata, targetType: TargetType.None);

            // Act
            bool valid = validator.ValidateTarget(ability, casterEntityId: 1, targetEntityId: -1, null, null);

            // Assert
            Assert.That(valid, Is.True, "No target requirement should always be valid");
        }

        #endregion

        #region Ability Charges Tests (2 tests)

        [Test]
        public void AbilityInstance_WithCharges_TracksRemaining()
        {
            // Arrange
            var metadata = new AbilityMetadata("charge_ability", "Charge Ability", AbilityType.Active, maxCharges: 3);
            var ability = new Ability(metadata);
            var instance = new AbilityInstance(ability, ownerEntityId: 1);

            // Assert
            Assert.That(instance.RemainingCharges, Is.EqualTo(3), "Should start with 3 charges");
        }

        [Test]
        public void AbilityInstance_UseCharge_DecreasesCount()
        {
            // Arrange
            var metadata = new AbilityMetadata("dash", "Dash", AbilityType.Active, maxCharges: 2);
            var ability = new Ability(metadata);
            var instance = new AbilityInstance(ability, ownerEntityId: 1);

            // Act
            instance.UseCharge();

            // Assert
            Assert.That(instance.RemainingCharges, Is.EqualTo(1), "Should have 1 charge remaining");
            Assert.That(instance.CanUse(currentTime: 0f), Is.True, "Should still be able to use with charges remaining");

            // Act
            instance.UseCharge();

            // Assert
            Assert.That(instance.RemainingCharges, Is.EqualTo(0), "Should have 0 charges remaining");
            Assert.That(instance.CanUse(currentTime: 0f), Is.False, "Should not be able to use without charges");
        }

        #endregion
    }
}
