using NUnit.Framework;
using RPGFramework.Quests;
using RPGFramework.ECS;
using System;
using System.Collections.Generic;
// using Moq; // TODO: Add Moq package for mocking tests

namespace RPGFramework.Quests.Tests
{
    /// <summary>
    /// Comprehensive test suite for RPG Framework Quest System.
    /// Tests quest definitions, objectives, rewards, registry, manager, and progression.
    /// Covers 20+ test cases for all Quest System functionality.
    /// </summary>
    [TestFixture]
    public class QuestSystemTests
    {
        #region Helper Methods

        /// <summary>
        /// Create a mock IEntityManager for ECS testing.
        /// </summary>
        // TODO: Uncomment when Moq package is added
        // private Mock<IEntityManager> CreateMockEntityManager()
        // {
        //     var mock = new Mock<IEntityManager>();
        //     return mock;
        // }

        /// <summary>
        /// Create a simple test objective.
        /// </summary>
        private IQuestObjective CreateTestObjective(
            string id = "obj_001",
            ObjectiveType type = ObjectiveType.Kill,
            string targetId = "enemy_001",
            int requiredCount = 5,
            string description = "Kill 5 enemies",
            bool isOptional = false)
        {
            return new QuestObjective(id, type, targetId, requiredCount, description, isOptional);
        }

        /// <summary>
        /// Create a simple test reward.
        /// </summary>
        private IQuestReward CreateTestReward(
            RewardType type = RewardType.Experience,
            string rewardId = "reward_001",
            int amount = 100)
        {
            return new QuestReward(type, rewardId, amount);
        }

        /// <summary>
        /// Create a simple test quest.
        /// </summary>
        private IQuest CreateTestQuest(
            string id = "quest_001",
            string title = "Test Quest",
            string description = "A test quest",
            IReadOnlyList<IQuestObjective> objectives = null,
            IReadOnlyList<IQuestReward> rewards = null,
            IReadOnlyList<string> prerequisites = null,
            int minimumLevel = 1,
            bool isRepeatable = false)
        {
            objectives = objectives ?? new List<IQuestObjective> { CreateTestObjective() };
            rewards = rewards ?? new List<IQuestReward> { CreateTestReward() };
            prerequisites = prerequisites ?? new List<string>();

            return new Quest(id, title, description, objectives, rewards, prerequisites, minimumLevel, isRepeatable);
        }

        #endregion

        #region Quest Creation Tests (3 tests)

        [Test]
        public void CreateQuest_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var objectives = new List<IQuestObjective> { CreateTestObjective() };
            var rewards = new List<IQuestReward> { CreateTestReward() };

            var quest = new Quest(
                id: "quest_001",
                title: "Kill Bandits",
                description: "Eliminate the bandits in the forest",
                objectives: objectives,
                rewards: rewards,
                prerequisiteQuestIds: new List<string>(),
                minimumLevel: 5,
                isRepeatable: false
            );

            // Assert
            Assert.That(quest.Id, Is.EqualTo("quest_001"));
            Assert.That(quest.Title, Is.EqualTo("Kill Bandits"));
            Assert.That(quest.Description, Is.EqualTo("Eliminate the bandits in the forest"));
            Assert.That(quest.MinimumLevel, Is.EqualTo(5));
            Assert.That(quest.IsRepeatable, Is.False);
            Assert.That(quest.Objectives.Count, Is.EqualTo(1));
            Assert.That(quest.Rewards.Count, Is.EqualTo(1));
        }

        [Test]
        public void CreateQuest_WithPrerequisites_StoresPrerequisiteIds()
        {
            // Arrange
            var prerequisites = new List<string> { "quest_prerequisite_1", "quest_prerequisite_2" };

            // Act
            var quest = CreateTestQuest(prerequisites: prerequisites);

            // Assert
            Assert.That(quest.PrerequisiteQuestIds.Count, Is.EqualTo(2));
            Assert.That(quest.PrerequisiteQuestIds, Contains.Item("quest_prerequisite_1"));
            Assert.That(quest.PrerequisiteQuestIds, Contains.Item("quest_prerequisite_2"));
        }

        [Test]
        public void CreateQuest_AsRepeatable_SetsRepeatableFlag()
        {
            // Arrange & Act
            var quest = CreateTestQuest(isRepeatable: true);

            // Assert
            Assert.That(quest.IsRepeatable, Is.True);
        }

        #endregion

        #region QuestObjective Tests (3 tests)

        [Test]
        public void CreateQuestObjective_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var objective = new QuestObjective(
                id: "obj_kill_bandits",
                type: ObjectiveType.Kill,
                targetId: "bandit_01",
                requiredCount: 10,
                description: "Kill 10 bandits",
                isOptional: false
            );

            // Assert
            Assert.That(objective.Id, Is.EqualTo("obj_kill_bandits"));
            Assert.That(objective.Type, Is.EqualTo(ObjectiveType.Kill));
            Assert.That(objective.TargetId, Is.EqualTo("bandit_01"));
            Assert.That(objective.RequiredCount, Is.EqualTo(10));
            Assert.That(objective.Description, Is.EqualTo("Kill 10 bandits"));
            Assert.That(objective.IsOptional, Is.False);
        }

        [Test]
        public void CreateQuestObjective_AsOptional_MarksAsOptional()
        {
            // Arrange & Act
            var objective = CreateTestObjective(isOptional: true);

            // Assert
            Assert.That(objective.IsOptional, Is.True);
        }

        [Test]
        public void CreateQuestObjective_WithDifferentTypes_StoresCorrectType()
        {
            // Arrange & Act
            var collectObjective = new QuestObjective("obj_1", ObjectiveType.Collect, "item_1", 5, "Collect items", false);
            var interactObjective = new QuestObjective("obj_2", ObjectiveType.Interact, "npc_1", 1, "Interact with NPC", false);
            var exploreObjective = new QuestObjective("obj_3", ObjectiveType.Explore, "location_1", 1, "Explore location", false);

            // Assert
            Assert.That(collectObjective.Type, Is.EqualTo(ObjectiveType.Collect));
            Assert.That(interactObjective.Type, Is.EqualTo(ObjectiveType.Interact));
            Assert.That(exploreObjective.Type, Is.EqualTo(ObjectiveType.Explore));
        }

        #endregion

        #region QuestReward Tests (3 tests)

        [Test]
        public void CreateQuestReward_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var reward = new QuestReward(
                type: RewardType.Experience,
                rewardId: "xp_reward",
                amount: 500
            );

            // Assert
            Assert.That(reward.Type, Is.EqualTo(RewardType.Experience));
            Assert.That(reward.RewardId, Is.EqualTo("xp_reward"));
            Assert.That(reward.Amount, Is.EqualTo(500));
        }

        [Test]
        public void CreateQuestReward_WithDifferentTypes_StoresCorrectType()
        {
            // Arrange & Act
            var itemReward = new QuestReward(RewardType.Item, "sword_001", 1);
            var currencyReward = new QuestReward(RewardType.Currency, "gold", 250);
            var unlockReward = new QuestReward(RewardType.Unlock, "ability_001", 1);
            var customReward = new QuestReward(RewardType.Custom, "custom_reward", 10);

            // Assert
            Assert.That(itemReward.Type, Is.EqualTo(RewardType.Item));
            Assert.That(currencyReward.Type, Is.EqualTo(RewardType.Currency));
            Assert.That(unlockReward.Type, Is.EqualTo(RewardType.Unlock));
            Assert.That(customReward.Type, Is.EqualTo(RewardType.Custom));
        }

        [Test]
        public void CreateQuestReward_WithMultipleRewards_AllPropertiesIndependent()
        {
            // Arrange & Act
            var rewards = new List<IQuestReward>
            {
                new QuestReward(RewardType.Experience, "xp", 1000),
                new QuestReward(RewardType.Item, "item_1", 2),
                new QuestReward(RewardType.Currency, "gold", 500)
            };

            // Assert
            Assert.That(rewards.Count, Is.EqualTo(3));
            Assert.That(rewards[0].Amount, Is.EqualTo(1000));
            Assert.That(rewards[1].Amount, Is.EqualTo(2));
            Assert.That(rewards[2].Amount, Is.EqualTo(500));
        }

        #endregion

        #region QuestRegistry Tests (3 tests)

        [Test]
        public void QuestRegistry_RegisterQuest_AddsQuestSuccessfully()
        {
            // Arrange
            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001");

            // Act
            registry.RegisterQuest(quest);

            // Assert
            var retrieved = registry.GetQuest("quest_001");
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Id, Is.EqualTo("quest_001"));
        }

        [Test]
        public void QuestRegistry_GetAllQuests_ReturnsAllRegisteredQuests()
        {
            // Arrange
            var registry = new QuestRegistry();
            var quest1 = CreateTestQuest("quest_001");
            var quest2 = CreateTestQuest("quest_002");
            var quest3 = CreateTestQuest("quest_003");

            // Act
            registry.RegisterQuest(quest1);
            registry.RegisterQuest(quest2);
            registry.RegisterQuest(quest3);
            var allQuests = registry.GetAllQuests();

            // Assert
            Assert.That(allQuests.Count, Is.EqualTo(3));
            Assert.That(allQuests, Contains.Item(quest1));
            Assert.That(allQuests, Contains.Item(quest2));
            Assert.That(allQuests, Contains.Item(quest3));
        }

        [Test]
        public void QuestRegistry_GetQuest_ReturnsNullForNonexistentQuest()
        {
            // Arrange
            var registry = new QuestRegistry();

            // Act
            var result = registry.GetQuest("nonexistent_quest");

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region QuestManager CanAcceptQuest Tests (4 tests)

        [Test]
        public void QuestManager_CanAcceptQuest_ReturnsFalseForNonexistentQuest()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var registry = new QuestRegistry();
            var manager = new QuestManager(mockEntityManager.Object, registry);
            int entityId = 1;

            // Act
            bool canAccept = manager.CanAcceptQuest(entityId, "nonexistent_quest");

            // Assert
            Assert.That(canAccept, Is.False);
        }

        [Test]
        public void QuestManager_CanAcceptQuest_ReturnsTrueForNewQuest()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out It.Ref<ActiveQuestsData>.IsAny))
                .Returns(false);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001");
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool canAccept = manager.CanAcceptQuest(1, "quest_001");

            // Assert
            Assert.That(canAccept, Is.True);
        }

        [Test]
        public void QuestManager_CanAcceptQuest_ReturnsFalseIfAlreadyActive()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", State = QuestState.Active } }
                },
                CompletedQuestIds = new List<string>(),
                TotalQuestsCompleted = 0
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001");
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool canAccept = manager.CanAcceptQuest(1, "quest_001");

            // Assert
            Assert.That(canAccept, Is.False);
        }

        [Test]
        public void QuestManager_CanAcceptQuest_ReturnsFalseIfCompletedAndNotRepeatable()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>(),
                CompletedQuestIds = new List<string> { "quest_001" },
                TotalQuestsCompleted = 1
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", isRepeatable: false);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool canAccept = manager.CanAcceptQuest(1, "quest_001");

            // Assert
            Assert.That(canAccept, Is.False);
        }

        #endregion

        #region QuestManager AcceptQuest Tests (2 tests)

        [Test]
        public void QuestManager_AcceptQuest_CreatesActiveQuestAndInitializesObjectives()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out It.Ref<ActiveQuestsData>.IsAny))
                .Returns(false);

            var objectives = new List<IQuestObjective>
            {
                CreateTestObjective("obj_001", ObjectiveType.Kill, "enemy_1", 5),
                CreateTestObjective("obj_002", ObjectiveType.Collect, "item_1", 10)
            };

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: objectives);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool accepted = manager.AcceptQuest(1, "quest_001");

            // Assert
            Assert.That(accepted, Is.True);
            mockEntityManager.Verify(
                em => em.AddComponent(It.IsAny<int>(), It.IsAny<ActiveQuestsData>()),
                Times.Once,
                "AddComponent should be called once");
        }

        [Test]
        public void QuestManager_AcceptQuest_ReturnsFalseIfCannotAccept()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001" } }
                },
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001");
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool accepted = manager.AcceptQuest(1, "quest_001");

            // Assert
            Assert.That(accepted, Is.False);
        }

        #endregion

        #region QuestManager UpdateObjectiveProgress Tests (3 tests)

        [Test]
        public void QuestManager_UpdateObjectiveProgress_IncrementsProgressCorrectly()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int> { { "obj_001", 0 } };
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: new List<IQuestObjective> { CreateTestObjective("obj_001", ObjectiveType.Kill, "enemy_1", 5) });
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool updated = manager.UpdateObjectiveProgress(1, "quest_001", "obj_001", 2);

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(activeQuestsData.ActiveQuests["quest_001"].ObjectiveProgress["obj_001"], Is.EqualTo(2));
        }

        [Test]
        public void QuestManager_UpdateObjectiveProgress_CapsProgressAtRequiredCount()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int> { { "obj_001", 4 } };
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: new List<IQuestObjective> { CreateTestObjective("obj_001", ObjectiveType.Kill, "enemy_1", 5) });
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool updated = manager.UpdateObjectiveProgress(1, "quest_001", "obj_001", 10);

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(activeQuestsData.ActiveQuests["quest_001"].ObjectiveProgress["obj_001"], Is.EqualTo(5), "Progress should not exceed required count");
        }

        [Test]
        public void QuestManager_UpdateObjectiveProgress_ReturnsFalseForInvalidObjective()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int> { { "obj_001", 0 } };
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: new List<IQuestObjective> { CreateTestObjective("obj_001") });
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool updated = manager.UpdateObjectiveProgress(1, "quest_001", "nonexistent_objective", 2);

            // Assert
            Assert.That(updated, Is.False);
        }

        #endregion

        #region QuestManager CompleteQuest Tests (3 tests)

        [Test]
        public void QuestManager_CompleteQuest_SucceedsWhenAllObjectivesComplete()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int> { { "obj_001", 5 }, { "obj_002", 10 } };
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", State = QuestState.Active, ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>(),
                TotalQuestsCompleted = 0
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            // Capture updated component when AddComponent is called
            ActiveQuestsData capturedData = activeQuestsData;
            mockEntityManager
                .Setup(em => em.AddComponent<ActiveQuestsData>(It.IsAny<int>(), It.IsAny<ActiveQuestsData>()))
                .Callback<int, ActiveQuestsData>((id, data) => capturedData = data);

            var objectives = new List<IQuestObjective>
            {
                CreateTestObjective("obj_001", ObjectiveType.Kill, "enemy_1", 5),
                CreateTestObjective("obj_002", ObjectiveType.Collect, "item_1", 10)
            };

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: objectives);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool completed = manager.CompleteQuest(1, "quest_001");

            // Assert
            Assert.That(completed, Is.True);
            Assert.That(capturedData.ActiveQuests, Does.Not.ContainKey("quest_001"));
            Assert.That(capturedData.CompletedQuestIds, Contains.Item("quest_001"));
            Assert.That(capturedData.TotalQuestsCompleted, Is.EqualTo(1));
        }

        [Test]
        public void QuestManager_CompleteQuest_IgnoresOptionalObjectives()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int> { { "obj_001", 5 }, { "obj_optional", 0 } };
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", State = QuestState.Active, ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>(),
                TotalQuestsCompleted = 0
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var objectives = new List<IQuestObjective>
            {
                CreateTestObjective("obj_001", ObjectiveType.Kill, "enemy_1", 5, isOptional: false),
                CreateTestObjective("obj_optional", ObjectiveType.Kill, "rare_enemy", 1, isOptional: true)
            };

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: objectives);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool completed = manager.CompleteQuest(1, "quest_001");

            // Assert
            Assert.That(completed, Is.True, "Should complete even with optional objectives incomplete");
        }

        [Test]
        public void QuestManager_CompleteQuest_FailsIfRequiredObjectiveNotComplete()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int> { { "obj_001", 3 } };
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", State = QuestState.Active, ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: new List<IQuestObjective> { CreateTestObjective("obj_001", ObjectiveType.Kill, "enemy_1", 5) });
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool completed = manager.CompleteQuest(1, "quest_001");

            // Assert
            Assert.That(completed, Is.False);
        }

        #endregion

        #region QuestManager AbandonQuest Tests (2 tests)

        [Test]
        public void QuestManager_AbandonQuest_RemovesQuestFromActive()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", State = QuestState.Active } }
                },
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool abandoned = manager.AbandonQuest(1, "quest_001");

            // Assert
            Assert.That(abandoned, Is.True);
            Assert.That(activeQuestsData.ActiveQuests, Does.Not.ContainKey("quest_001"));
        }

        [Test]
        public void QuestManager_AbandonQuest_ReturnsFalseIfNotActive()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>(),
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool abandoned = manager.AbandonQuest(1, "nonexistent_quest");

            // Assert
            Assert.That(abandoned, Is.False);
        }

        #endregion

        #region QuestProgress State Transitions Tests (2 tests)

        [Test]
        public void QuestProgress_NewProgress_StartsAsActive()
        {
            // Arrange & Act
            var progress = new QuestProgress("quest_001");

            // Assert
            Assert.That(progress.State, Is.EqualTo(QuestState.Active));
            Assert.That(progress.StartedAt, Is.Not.Null);
            Assert.That(progress.CompletedAt, Is.Null);
        }

        [Test]
        public void QuestProgress_StateTransitions_WorkCorrectly()
        {
            // Arrange
            var progress = new QuestProgress("quest_001");
            Assert.That(progress.State, Is.EqualTo(QuestState.Active));

            // Act & Assert - Complete
            progress.Complete();
            Assert.That(progress.State, Is.EqualTo(QuestState.Completed));
            Assert.That(progress.CompletedAt, Is.Not.Null);

            // Arrange new progress
            var progressToAbandon = new QuestProgress("quest_002");

            // Act & Assert - Abandon
            progressToAbandon.Abandon();
            Assert.That(progressToAbandon.State, Is.EqualTo(QuestState.Abandoned));

            // Arrange new progress
            var progressToFail = new QuestProgress("quest_003");

            // Act & Assert - Fail
            progressToFail.Fail();
            Assert.That(progressToFail.State, Is.EqualTo(QuestState.Failed));
        }

        #endregion

        #region Multiple Objectives Tests (2 tests)

        [Test]
        public void QuestManager_MultipleObjectives_TracksEachObjectiveIndependently()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int>
            {
                { "obj_001", 0 },
                { "obj_002", 0 },
                { "obj_003", 0 }
            };

            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>()
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var objectives = new List<IQuestObjective>
            {
                CreateTestObjective("obj_001", ObjectiveType.Kill, "enemy_1", 5),
                CreateTestObjective("obj_002", ObjectiveType.Collect, "item_1", 3),
                CreateTestObjective("obj_003", ObjectiveType.Interact, "npc_1", 2)
            };

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: objectives);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            manager.UpdateObjectiveProgress(1, "quest_001", "obj_001", 2);
            manager.UpdateObjectiveProgress(1, "quest_001", "obj_002", 1);
            manager.UpdateObjectiveProgress(1, "quest_001", "obj_003", 1);

            // Assert
            Assert.That(objectiveData["obj_001"], Is.EqualTo(2));
            Assert.That(objectiveData["obj_002"], Is.EqualTo(1));
            Assert.That(objectiveData["obj_003"], Is.EqualTo(1));
        }

        [Test]
        public void Quest_WithMultipleObjectives_AllObjectivesRequired()
        {
            // Arrange
            var objectives = new List<IQuestObjective>
            {
                CreateTestObjective("obj_1", ObjectiveType.Kill, "enemy_1", 3),
                CreateTestObjective("obj_2", ObjectiveType.Kill, "enemy_2", 3),
                CreateTestObjective("obj_3", ObjectiveType.Kill, "enemy_3", 3)
            };

            // Act
            var quest = CreateTestQuest("quest_001", objectives: objectives);

            // Assert
            Assert.That(quest.Objectives.Count, Is.EqualTo(3));
            Assert.That(quest.Objectives[0].RequiredCount, Is.EqualTo(3));
            Assert.That(quest.Objectives[1].RequiredCount, Is.EqualTo(3));
            Assert.That(quest.Objectives[2].RequiredCount, Is.EqualTo(3));
        }

        #endregion

        #region Optional Objectives Tests (2 tests)

        [Test]
        public void Quest_WithOptionalObjectives_MarksObjectivesAsOptional()
        {
            // Arrange
            var objectives = new List<IQuestObjective>
            {
                CreateTestObjective("obj_1", ObjectiveType.Kill, "enemy_1", 5, isOptional: false),
                CreateTestObjective("obj_2", ObjectiveType.Kill, "rare_enemy", 1, isOptional: true),
                CreateTestObjective("obj_3", ObjectiveType.Collect, "treasure", 1, isOptional: true)
            };

            // Act
            var quest = CreateTestQuest("quest_001", objectives: objectives);

            // Assert
            Assert.That(quest.Objectives[0].IsOptional, Is.False);
            Assert.That(quest.Objectives[1].IsOptional, Is.True);
            Assert.That(quest.Objectives[2].IsOptional, Is.True);
        }

        [Test]
        public void QuestManager_CompleteQuest_AllowsCompletionWithUnfinishedOptionalObjectives()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int>
            {
                { "obj_required", 5 },
                { "obj_optional_1", 0 },
                { "obj_optional_2", 1 }  // Partially complete
            };

            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_001", new QuestProgressData { QuestId = "quest_001", State = QuestState.Active, ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string>(),
                TotalQuestsCompleted = 0
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var objectives = new List<IQuestObjective>
            {
                CreateTestObjective("obj_required", ObjectiveType.Kill, "enemy_1", 5, isOptional: false),
                CreateTestObjective("obj_optional_1", ObjectiveType.Collect, "item_1", 10, isOptional: true),
                CreateTestObjective("obj_optional_2", ObjectiveType.Collect, "item_2", 5, isOptional: true)
            };

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", objectives: objectives);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool completed = manager.CompleteQuest(1, "quest_001");

            // Assert
            Assert.That(completed, Is.True);
        }

        #endregion

        #region Quest Chain (Prerequisites) Tests (2 tests)

        [Test]
        public void QuestManager_CanAcceptQuest_RequiresPrerequisiteCompletion()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>(),
                CompletedQuestIds = new List<string> { "quest_prerequisite" },
                TotalQuestsCompleted = 1
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var prerequisites = new List<string> { "quest_prerequisite" };
            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", prerequisites: prerequisites);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool canAccept = manager.CanAcceptQuest(1, "quest_001");

            // Assert
            Assert.That(canAccept, Is.True);
        }

        [Test]
        public void QuestManager_CanAcceptQuest_ReturnsFalseIfPrerequisiteNotComplete()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>(),
                CompletedQuestIds = new List<string>(),
                TotalQuestsCompleted = 0
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var prerequisites = new List<string> { "quest_prerequisite" };
            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_001", prerequisites: prerequisites);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool canAccept = manager.CanAcceptQuest(1, "quest_001");

            // Assert
            Assert.That(canAccept, Is.False);
        }

        #endregion

        #region Repeatable Quest Tests (2 tests)

        [Test]
        public void QuestManager_CanAcceptQuest_AllowsRepeatableQuestAfterCompletion()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>(),
                CompletedQuestIds = new List<string> { "quest_repeatable" },
                TotalQuestsCompleted = 1
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_repeatable", isRepeatable: true);
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool canAccept = manager.CanAcceptQuest(1, "quest_repeatable");

            // Assert
            Assert.That(canAccept, Is.True);
        }

        [Test]
        public void QuestManager_CompleteQuest_AllowsRepeatingQuestMultipleTimes()
        {
            // Arrange
            var mockEntityManager = CreateMockEntityManager();
            var objectiveData = new Dictionary<string, int> { { "obj_001", 5 } };
            var activeQuestsData = new ActiveQuestsData
            {
                ActiveQuests = new Dictionary<string, QuestProgressData>
                {
                    { "quest_repeatable", new QuestProgressData { QuestId = "quest_repeatable", State = QuestState.Active, ObjectiveProgress = objectiveData } }
                },
                CompletedQuestIds = new List<string> { "quest_repeatable", "quest_repeatable" },
                TotalQuestsCompleted = 2
            };

            mockEntityManager
                .Setup(em => em.TryGetComponent<ActiveQuestsData>(It.IsAny<int>(), out activeQuestsData))
                .Returns(true);

            // Capture updated component when AddComponent is called
            ActiveQuestsData capturedData = activeQuestsData;
            mockEntityManager
                .Setup(em => em.AddComponent<ActiveQuestsData>(It.IsAny<int>(), It.IsAny<ActiveQuestsData>()))
                .Callback<int, ActiveQuestsData>((id, data) => capturedData = data);

            var registry = new QuestRegistry();
            var quest = CreateTestQuest("quest_repeatable", isRepeatable: true, objectives: new List<IQuestObjective> { CreateTestObjective("obj_001") });
            registry.RegisterQuest(quest);

            var manager = new QuestManager(mockEntityManager.Object, registry);

            // Act
            bool completed = manager.CompleteQuest(1, "quest_repeatable");

            // Assert
            Assert.That(completed, Is.True);
            Assert.That(capturedData.TotalQuestsCompleted, Is.EqualTo(3));
        }

        #endregion
    }
}
