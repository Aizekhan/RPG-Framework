using NUnit.Framework;
using RPGFramework.Achievements;
using RPGFramework.Achievements.Runtime;
using RPGFramework.Achievements.Runtime.Contracts;
using RPGFramework.ECS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Achievements.Tests
{
    /// <summary>
    /// Comprehensive test suite for RPG Framework Achievement System.
    /// Tests achievements, criteria, progress tracking, manager operations, and registry.
    /// Covers 25+ test cases for all Achievement System functionality.
    /// </summary>
    [TestFixture]
    public class AchievementSystemTests
    {
        #region Helper Methods

        /// <summary>
        /// Create a simple test criterion.
        /// </summary>
        private IAchievementCriterion CreateTestCriterion(
            string id = "crit_001",
            CriterionType type = CriterionType.KillCount,
            string targetId = "enemy_001",
            int requiredCount = 5,
            string description = "Kill 5 enemies",
            bool requiresSingleSession = false)
        {
            return new AchievementCriterion(id, type, targetId, requiredCount, description, requiresSingleSession);
        }

        /// <summary>
        /// Create a simple test reward.
        /// </summary>
        private IAchievementReward CreateTestReward(
            RewardType type = RewardType.Experience,
            string rewardId = "reward_001",
            int amount = 100)
        {
            return new AchievementReward(type, rewardId, amount);
        }

        /// <summary>
        /// Create a simple test achievement.
        /// </summary>
        private IAchievement CreateTestAchievement(
            string id = "ach_001",
            string name = "Test Achievement",
            string description = "Test achievement description",
            AchievementTier tier = AchievementTier.Common,
            IReadOnlyList<IAchievementCriterion> criteria = null,
            IReadOnlyList<IAchievementReward> rewards = null,
            IReadOnlyList<string> prerequisites = null,
            bool isHidden = false,
            bool isSecret = false,
            int points = 10)
        {
            return new Achievement(
                id,
                name,
                description,
                "Test Category",
                points,
                tier,
                criteria ?? new List<IAchievementCriterion>(),
                rewards ?? new List<IAchievementReward>(),
                prerequisites ?? new List<string>(),
                isHidden,
                isSecret);
        }

        #endregion

        #region Achievement Tests

        [Test]
        public void Achievement_Create_ValidProperties()
        {
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_001", CriterionType.KillCount, "enemy_001", 10)
            };
            var rewards = new List<IAchievementReward>
            {
                CreateTestReward(RewardType.Experience, "xp", 100)
            };

            var achievement = new Achievement(
                "ach_001",
                "Monster Slayer",
                "Defeat 10 enemies",
                "Combat",
                25,
                AchievementTier.Common,
                criteria,
                rewards,
                new List<string>());

            Assert.That(achievement.AchievementId, Is.EqualTo("ach_001"));
            Assert.That(achievement.Name, Is.EqualTo("Monster Slayer"));
            Assert.That(achievement.Description, Is.EqualTo("Defeat 10 enemies"));
            Assert.That(achievement.Category, Is.EqualTo("Combat"));
            Assert.That(achievement.Points, Is.EqualTo(25));
            Assert.That(achievement.Tier, Is.EqualTo(AchievementTier.Common));
            Assert.That(achievement.Criteria.Count, Is.EqualTo(1));
            Assert.That(achievement.Rewards.Count, Is.EqualTo(1));
            Assert.That(achievement.Prerequisites.Count, Is.EqualTo(0));
            Assert.That(achievement.IsHidden, Is.False);
            Assert.That(achievement.IsSecret, Is.False);
        }

        [Test]
        public void Achievement_Create_WithPrerequisites()
        {
            var prerequisites = new List<string> { "ach_beginner" };
            var achievement = CreateTestAchievement(
                prerequisites: prerequisites);

            Assert.That(achievement.Prerequisites.Count, Is.EqualTo(1));
            Assert.That(achievement.Prerequisites[0], Is.EqualTo("ach_beginner"));
        }

        [Test]
        public void Achievement_Create_HiddenFlag()
        {
            var achievement = CreateTestAchievement(isHidden: true);

            Assert.That(achievement.IsHidden, Is.True);
            Assert.That(achievement.IsSecret, Is.False);
        }

        [Test]
        public void Achievement_Create_SecretFlag()
        {
            var achievement = CreateTestAchievement(isSecret: true);

            Assert.That(achievement.IsSecret, Is.True);
            Assert.That(achievement.IsHidden, Is.False);
        }

        [Test]
        public void Achievement_Create_TierClassification()
        {
            var commonAch = CreateTestAchievement(tier: AchievementTier.Common);
            var legendaryAch = CreateTestAchievement(tier: AchievementTier.Legendary);
            var mythicAch = CreateTestAchievement(tier: AchievementTier.Mythic);

            Assert.That(commonAch.Tier, Is.EqualTo(AchievementTier.Common));
            Assert.That(legendaryAch.Tier, Is.EqualTo(AchievementTier.Legendary));
            Assert.That(mythicAch.Tier, Is.EqualTo(AchievementTier.Mythic));
        }

        [Test]
        public void Achievement_Create_ThrowsOnEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                new Achievement("", "Name", "Desc", "Cat", 10, AchievementTier.Common,
                    new List<IAchievementCriterion>(), new List<IAchievementReward>(), new List<string>()));
        }

        [Test]
        public void Achievement_Create_ThrowsOnEmptyName()
        {
            Assert.Throws<ArgumentException>(() =>
                new Achievement("id", "", "Desc", "Cat", 10, AchievementTier.Common,
                    new List<IAchievementCriterion>(), new List<IAchievementReward>(), new List<string>()));
        }

        #endregion

        #region Criterion Tests

        [Test]
        public void Criterion_Create_ValidProperties()
        {
            var criterion = new AchievementCriterion(
                "crit_001",
                CriterionType.KillCount,
                "enemy_goblin",
                50,
                "Kill 50 goblins",
                false);

            Assert.That(criterion.CriterionId, Is.EqualTo("crit_001"));
            Assert.That(criterion.Type, Is.EqualTo(CriterionType.KillCount));
            Assert.That(criterion.TargetId, Is.EqualTo("enemy_goblin"));
            Assert.That(criterion.RequiredCount, Is.EqualTo(50));
            Assert.That(criterion.Description, Is.EqualTo("Kill 50 goblins"));
            Assert.That(criterion.RequiresSingleSession, Is.False);
        }

        [Test]
        public void Criterion_Create_DifferentTypes()
        {
            var killCriterion = CreateTestCriterion(type: CriterionType.KillCount);
            var itemCriterion = CreateTestCriterion(type: CriterionType.ItemCollected);
            var questCriterion = CreateTestCriterion(type: CriterionType.QuestCompleted);

            Assert.That(killCriterion.Type, Is.EqualTo(CriterionType.KillCount));
            Assert.That(itemCriterion.Type, Is.EqualTo(CriterionType.ItemCollected));
            Assert.That(questCriterion.Type, Is.EqualTo(CriterionType.QuestCompleted));
        }

        [Test]
        public void Criterion_Create_SingleSessionRequirement()
        {
            var criterion = CreateTestCriterion(requiresSingleSession: true);

            Assert.That(criterion.RequiresSingleSession, Is.True);
        }

        [Test]
        public void Criterion_Create_ThrowsOnEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                new AchievementCriterion("", CriterionType.KillCount, "target", 5));
        }

        [Test]
        public void Criterion_Create_ThrowsOnZeroRequiredCount()
        {
            Assert.Throws<ArgumentException>(() =>
                new AchievementCriterion("id", CriterionType.KillCount, "target", 0));
        }

        #endregion

        #region Progress Tests

        [Test]
        public void Progress_Initialize_StartsLocked()
        {
            var criteria = new List<(string, int)>
            {
                ("crit_001", 10),
                ("crit_002", 5)
            };
            var progress = new AchievementProgress("ach_001", criteria);

            Assert.That(progress.AchievementId, Is.EqualTo("ach_001"));
            Assert.That(progress.State, Is.EqualTo(AchievementState.Locked));
            Assert.That(progress.IsComplete, Is.False);
            Assert.That(progress.CompletionPercentage, Is.EqualTo(0f));
            Assert.That(progress.CriterionProgress.Count, Is.EqualTo(2));
        }

        [Test]
        public void Progress_UpdateCriterion_TransitionsToInProgress()
        {
            var criteria = new List<(string, int)> { ("crit_001", 10) };
            var progress = new AchievementProgress("ach_001", criteria);

            bool updated = progress.UpdateCriterion("crit_001", 5);

            Assert.That(updated, Is.True);
            Assert.That(progress.State, Is.EqualTo(AchievementState.InProgress));
        }

        [Test]
        public void Progress_UpdateCriterion_TracksProgress()
        {
            var criteria = new List<(string, int)> { ("crit_001", 10) };
            var progress = new AchievementProgress("ach_001", criteria);

            progress.UpdateCriterion("crit_001", 7);

            var criterionProgress = progress.GetCriterionProgress("crit_001");
            Assert.That(criterionProgress, Is.Not.Null);
            Assert.That(criterionProgress.Value.CurrentValue, Is.EqualTo(7));
            Assert.That(criterionProgress.Value.RequiredValue, Is.EqualTo(10));
            Assert.That(criterionProgress.Value.IsCompleted, Is.False);
        }

        [Test]
        public void Progress_UpdateCriterion_CompletesWhenReached()
        {
            var criteria = new List<(string, int)> { ("crit_001", 10) };
            var progress = new AchievementProgress("ach_001", criteria);

            progress.UpdateCriterion("crit_001", 10);

            var criterionProgress = progress.GetCriterionProgress("crit_001");
            Assert.That(criterionProgress.Value.IsCompleted, Is.True);
        }

        [Test]
        public void Progress_CompletionPercentage_SingleCriterion()
        {
            var criteria = new List<(string, int)> { ("crit_001", 10) };
            var progress = new AchievementProgress("ach_001", criteria);

            progress.UpdateCriterion("crit_001", 5);

            Assert.That(progress.CompletionPercentage, Is.EqualTo(0.5f));
        }

        [Test]
        public void Progress_CompletionPercentage_MultipleCriteria()
        {
            var criteria = new List<(string, int)>
            {
                ("crit_001", 10),
                ("crit_002", 20)
            };
            var progress = new AchievementProgress("ach_001", criteria);

            progress.UpdateCriterion("crit_001", 10); // 100%
            progress.UpdateCriterion("crit_002", 10); // 50%

            // Average: (1.0 + 0.5) / 2 = 0.75
            Assert.That(progress.CompletionPercentage, Is.EqualTo(0.75f));
        }

        [Test]
        public void Progress_Unlock_TransitionsToUnlocked()
        {
            var criteria = new List<(string, int)> { ("crit_001", 10) };
            var progress = new AchievementProgress("ach_001", criteria);

            progress.Unlock();

            Assert.That(progress.State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(progress.IsComplete, Is.True);
        }

        [Test]
        public void Progress_StateTransitions_LockedToInProgressToUnlocked()
        {
            var criteria = new List<(string, int)> { ("crit_001", 5) };
            var progress = new AchievementProgress("ach_001", criteria);

            // Initial: Locked
            Assert.That(progress.State, Is.EqualTo(AchievementState.Locked));

            // Update: InProgress
            progress.UpdateCriterion("crit_001", 3);
            Assert.That(progress.State, Is.EqualTo(AchievementState.InProgress));

            // Complete: Still InProgress until Unlock() called
            progress.UpdateCriterion("crit_001", 5);
            Assert.That(progress.State, Is.EqualTo(AchievementState.InProgress));

            // Unlock: Unlocked
            progress.Unlock();
            Assert.That(progress.State, Is.EqualTo(AchievementState.Unlocked));
        }

        [Test]
        public void Progress_MultipleCriteria_AllMustComplete()
        {
            var criteria = new List<(string, int)>
            {
                ("crit_001", 10),
                ("crit_002", 5),
                ("crit_003", 3)
            };
            var progress = new AchievementProgress("ach_001", criteria);

            progress.UpdateCriterion("crit_001", 10);
            progress.UpdateCriterion("crit_002", 5);
            Assert.That(progress.AreAllCriteriaCompleted(), Is.False);

            progress.UpdateCriterion("crit_003", 3);
            Assert.That(progress.AreAllCriteriaCompleted(), Is.True);
        }

        #endregion

        #region Registry Tests

        [Test]
        public void Registry_RegisterAchievement_Success()
        {
            var registry = new AchievementRegistry();
            var achievement = CreateTestAchievement("ach_001", "Test");

            registry.RegisterAchievement(achievement);

            Assert.That(registry.HasAchievement("ach_001"), Is.True);
            Assert.That(registry.GetAchievement("ach_001"), Is.EqualTo(achievement));
        }

        [Test]
        public void Registry_RegisterDuplicate_Overwrites()
        {
            var registry = new AchievementRegistry();
            var achievement1 = CreateTestAchievement("ach_001", "First");
            var achievement2 = CreateTestAchievement("ach_001", "Second");

            registry.RegisterAchievement(achievement1);
            registry.RegisterAchievement(achievement2);

            var retrieved = registry.GetAchievement("ach_001");
            Assert.That(retrieved.Name, Is.EqualTo("Second"));
        }

        [Test]
        public void Registry_UnregisterAchievement_Success()
        {
            var registry = new AchievementRegistry();
            var achievement = CreateTestAchievement("ach_001", "Test");

            registry.RegisterAchievement(achievement);
            bool removed = registry.UnregisterAchievement("ach_001");

            Assert.That(removed, Is.True);
            Assert.That(registry.HasAchievement("ach_001"), Is.False);
        }

        [Test]
        public void Registry_GetAchievementsByTier_FiltersCorrectly()
        {
            var registry = new AchievementRegistry();
            registry.RegisterAchievement(CreateTestAchievement("ach_common1", tier: AchievementTier.Common));
            registry.RegisterAchievement(CreateTestAchievement("ach_rare1", tier: AchievementTier.Rare));
            registry.RegisterAchievement(CreateTestAchievement("ach_common2", tier: AchievementTier.Common));
            registry.RegisterAchievement(CreateTestAchievement("ach_legendary", tier: AchievementTier.Legendary));

            var commonAchievements = registry.GetAchievementsByTier(AchievementTier.Common);
            var rareAchievements = registry.GetAchievementsByTier(AchievementTier.Rare);

            Assert.That(commonAchievements.Count, Is.EqualTo(2));
            Assert.That(rareAchievements.Count, Is.EqualTo(1));
        }

        [Test]
        public void Registry_GetAchievementsByCriterionType_FiltersCorrectly()
        {
            var registry = new AchievementRegistry();

            var killCriteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion(type: CriterionType.KillCount)
            };
            var itemCriteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion(type: CriterionType.ItemCollected)
            };
            var mixedCriteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_001", CriterionType.KillCount),
                CreateTestCriterion("crit_002", CriterionType.QuestCompleted)
            };

            registry.RegisterAchievement(CreateTestAchievement("ach_kill", criteria: killCriteria));
            registry.RegisterAchievement(CreateTestAchievement("ach_item", criteria: itemCriteria));
            registry.RegisterAchievement(CreateTestAchievement("ach_mixed", criteria: mixedCriteria));

            var killAchievements = registry.GetAchievementsByCriterionType(CriterionType.KillCount);
            var itemAchievements = registry.GetAchievementsByCriterionType(CriterionType.ItemCollected);

            Assert.That(killAchievements.Count, Is.EqualTo(2)); // ach_kill and ach_mixed
            Assert.That(itemAchievements.Count, Is.EqualTo(1)); // ach_item only
        }

        [Test]
        public void Registry_Clear_RemovesAllAchievements()
        {
            var registry = new AchievementRegistry();
            registry.RegisterAchievement(CreateTestAchievement("ach_001"));
            registry.RegisterAchievement(CreateTestAchievement("ach_002"));

            registry.Clear();

            Assert.That(registry.GetAllAchievements().Count, Is.EqualTo(0));
        }

        #endregion

        #region Manager Tests

        [Test]
        public void Manager_InitializeEntityAchievements_CreatesEmptyData()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var manager = new AchievementManager(registry, entityManager);

            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            var data = entityManager.GetComponent<AchievementData>(entityId);
            Assert.That(data.UnlockedAchievementIds, Is.Not.Null);
            Assert.That(data.UnlockedAchievementIds.Length, Is.EqualTo(0));
            Assert.That(data.TotalPoints, Is.EqualTo(0));
        }

        [Test]
        public void Manager_InitializeEntityAchievements_CreatesProgressTracking()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_001", CriterionType.KillCount, "enemy_001", 10)
            };
            registry.RegisterAchievement(CreateTestAchievement("ach_001", criteria: criteria));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();

            manager.InitializeEntityAchievements(entityId);

            var progress = manager.GetAchievementProgress(entityId, "ach_001");
            Assert.That(progress, Is.Not.Null);
            Assert.That(progress.State, Is.EqualTo(AchievementState.Locked));
        }

        [Test]
        public void Manager_UpdateCriterionProgress_UpdatesCorrectly()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_001", CriterionType.KillCount, "enemy_001", 10)
            };
            registry.RegisterAchievement(CreateTestAchievement("ach_001", criteria: criteria));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            bool updated = manager.UpdateCriterionProgress(entityId, "ach_001", "crit_001", 5);

            Assert.That(updated, Is.True);
            var progress = manager.GetAchievementProgress(entityId, "ach_001");
            Assert.That(progress.State, Is.EqualTo(AchievementState.InProgress));
        }

        [Test]
        public void Manager_UpdateCriterionProgress_AutoUnlocksWhenComplete()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_001", CriterionType.KillCount, "enemy_001", 10)
            };
            registry.RegisterAchievement(CreateTestAchievement("ach_001", criteria: criteria));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            manager.UpdateCriterionProgress(entityId, "ach_001", "crit_001", 10);

            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_001"), Is.True);
        }

        [Test]
        public void Manager_UnlockAchievement_ManualUnlock()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var achievement = CreateTestAchievement("ach_001", points: 50);
            registry.RegisterAchievement(achievement);

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            bool unlocked = manager.UnlockAchievement(entityId, "ach_001");

            Assert.That(unlocked, Is.True);
            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_001"), Is.True);

            var data = entityManager.GetComponent<AchievementData>(entityId);
            Assert.That(data.TotalPoints, Is.EqualTo(50));
        }

        [Test]
        public void Manager_CheckRetroactiveUnlocks_UnlocksMatchingAchievements()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();

            // Achievement: Kill 10 goblins
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_goblin", CriterionType.KillCount, "enemy_goblin", 10)
            };
            registry.RegisterAchievement(CreateTestAchievement("ach_goblin_slayer", criteria: criteria));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            // Player has already killed 15 goblins
            var criterionData = new Dictionary<string, int>
            {
                { "enemy_goblin", 15 }
            };

            var unlocked = manager.CheckRetroactiveUnlocks(entityId, CriterionType.KillCount, criterionData);

            Assert.That(unlocked.Count, Is.EqualTo(1));
            Assert.That(unlocked[0], Is.EqualTo("ach_goblin_slayer"));
            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_goblin_slayer"), Is.True);
        }

        [Test]
        public void Manager_GetUnlockedAchievements_ReturnsCorrectList()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            registry.RegisterAchievement(CreateTestAchievement("ach_001"));
            registry.RegisterAchievement(CreateTestAchievement("ach_002"));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            manager.UnlockAchievement(entityId, "ach_001");

            var unlockedAchievements = manager.GetUnlockedAchievements(entityId);
            Assert.That(unlockedAchievements.Count, Is.EqualTo(1));
            Assert.That(unlockedAchievements[0], Is.EqualTo("ach_001"));
        }

        [Test]
        public void Manager_GetInProgressAchievements_ReturnsCorrectList()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_001", CriterionType.KillCount, "enemy_001", 10)
            };
            registry.RegisterAchievement(CreateTestAchievement("ach_001", criteria: criteria));
            registry.RegisterAchievement(CreateTestAchievement("ach_002", criteria: criteria));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            manager.UpdateCriterionProgress(entityId, "ach_001", "crit_001", 5);

            var inProgress = manager.GetInProgressAchievements(entityId);
            Assert.That(inProgress.Count, Is.EqualTo(1));
            Assert.That(inProgress[0].AchievementId, Is.EqualTo("ach_001"));
        }

        [Test]
        public void Manager_GetAchievementsByTier_ReturnsCorrectIds()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            registry.RegisterAchievement(CreateTestAchievement("ach_common1", tier: AchievementTier.Common));
            registry.RegisterAchievement(CreateTestAchievement("ach_rare", tier: AchievementTier.Rare));
            registry.RegisterAchievement(CreateTestAchievement("ach_common2", tier: AchievementTier.Common));

            var manager = new AchievementManager(registry, entityManager);

            var commonIds = manager.GetAchievementsByTier(AchievementTier.Common);
            Assert.That(commonIds.Count, Is.EqualTo(2));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Integration_CompleteAchievementFlow()
        {
            // Setup
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_kill", CriterionType.KillCount, "enemy_goblin", 5)
            };
            var rewards = new List<IAchievementReward>
            {
                CreateTestReward(RewardType.Experience, "xp", 100)
            };
            var achievement = CreateTestAchievement(
                "ach_goblin_hunter",
                "Goblin Hunter",
                "Kill 5 goblins",
                AchievementTier.Common,
                criteria,
                rewards,
                points: 25);

            registry.RegisterAchievement(achievement);
            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();

            // Initialize
            manager.InitializeEntityAchievements(entityId);
            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_goblin_hunter"), Is.False);

            // Track progress
            manager.UpdateCriterionProgress(entityId, "ach_goblin_hunter", "crit_kill", 2);
            var progress = manager.GetAchievementProgress(entityId, "ach_goblin_hunter");
            Assert.That(progress.State, Is.EqualTo(AchievementState.InProgress));
            Assert.That(progress.CompletionPercentage, Is.EqualTo(0.4f));

            // Complete achievement
            manager.UpdateCriterionProgress(entityId, "ach_goblin_hunter", "crit_kill", 5);
            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_goblin_hunter"), Is.True);

            // Verify data
            var data = entityManager.GetComponent<AchievementData>(entityId);
            Assert.That(data.TotalPoints, Is.EqualTo(25));
            Assert.That(data.UnlockedAchievementIds.Contains("ach_goblin_hunter"), Is.True);
        }

        [Test]
        public void Integration_MultiCriteriaAchievementCompletion()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();
            var criteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_kill", CriterionType.KillCount, "enemy_goblin", 10),
                CreateTestCriterion("crit_item", CriterionType.ItemCollected, "item_trophy", 1),
                CreateTestCriterion("crit_quest", CriterionType.QuestCompleted, "quest_hunter", 1)
            };
            registry.RegisterAchievement(CreateTestAchievement("ach_master_hunter", criteria: criteria));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            // Complete criteria in different order
            manager.UpdateCriterionProgress(entityId, "ach_master_hunter", "crit_quest", 1);
            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_master_hunter"), Is.False);

            manager.UpdateCriterionProgress(entityId, "ach_master_hunter", "crit_kill", 10);
            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_master_hunter"), Is.False);

            manager.UpdateCriterionProgress(entityId, "ach_master_hunter", "crit_item", 1);
            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_master_hunter"), Is.True);
        }

        [Test]
        public void Integration_ChainedAchievementsWithPrerequisites()
        {
            var entityManager = new EntityManager();
            var registry = new AchievementRegistry();

            // Base achievement
            var beginnerCriteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_beginner", CriterionType.KillCount, "any", 1)
            };
            registry.RegisterAchievement(CreateTestAchievement("ach_beginner", "Beginner", criteria: beginnerCriteria));

            // Advanced achievement with prerequisite
            var advancedCriteria = new List<IAchievementCriterion>
            {
                CreateTestCriterion("crit_advanced", CriterionType.KillCount, "any", 50)
            };
            var prerequisites = new List<string> { "ach_beginner" };
            registry.RegisterAchievement(
                CreateTestAchievement("ach_advanced", "Advanced", criteria: advancedCriteria, prerequisites: prerequisites));

            var manager = new AchievementManager(registry, entityManager);
            int entityId = entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            // Verify prerequisites are stored
            var advancedAchievement = registry.GetAchievement("ach_advanced");
            Assert.That(advancedAchievement.Prerequisites.Count, Is.EqualTo(1));
            Assert.That(advancedAchievement.Prerequisites[0], Is.EqualTo("ach_beginner"));
        }

        #endregion
    }
}
