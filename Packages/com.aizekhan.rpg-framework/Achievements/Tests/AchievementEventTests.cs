using System;
using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Achievements.Runtime;
using RPGFramework.Achievements.Runtime.Contracts;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Achievements.Tests
{
    /// <summary>
    /// Proves AchievementManager publishes its events on a real event bus: progress on every criterion change,
    /// criterion completed when a criterion reaches its target, and achievement unlocked once per unlock.
    /// </summary>
    [TestFixture]
    public class AchievementEventTests
    {
        private EventBus _bus;
        private EntityManager _entityManager;
        private AchievementRegistry _registry;
        private AchievementManager _manager;
        private int _entityId;

        private readonly List<AchievementProgressEvent> _progress = new List<AchievementProgressEvent>();
        private readonly List<CriterionCompletedEvent> _criteria = new List<CriterionCompletedEvent>();
        private readonly List<AchievementUnlockedEvent> _unlocks = new List<AchievementUnlockedEvent>();

        [SetUp]
        public void SetUp()
        {
            _progress.Clear();
            _criteria.Clear();
            _unlocks.Clear();

            _bus = new EventBus();
            _bus.Subscribe<AchievementProgressEvent>(e => _progress.Add(e));
            _bus.Subscribe<CriterionCompletedEvent>(e => _criteria.Add(e));
            _bus.Subscribe<AchievementUnlockedEvent>(e => _unlocks.Add(e));

            _entityManager = new EntityManager();
            _registry = new AchievementRegistry();

            // Two criteria, so one can complete without unlocking the achievement
            _registry.RegisterAchievement(new Achievement(
                "ach_hunter", "Hunter", "Kill wolves and collect pelts", "Combat", 50, AchievementTier.Rare,
                new List<IAchievementCriterion>
                {
                    new AchievementCriterion("crit_kills", CriterionType.KillCount, "wolf", 10),
                    new AchievementCriterion("crit_pelts", CriterionType.ItemCollected, "pelt", 4)
                },
                new List<IAchievementReward>(),
                new List<string>()));

            _manager = new AchievementManager(_registry, _entityManager, _bus);
            _entityId = _entityManager.CreateEntity();
            _manager.InitializeEntityAchievements(_entityId);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        [Test]
        public void UpdatingProgress_PublishesAchievementProgress()
        {
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_kills", 5);

            Assert.That(_progress.Count, Is.EqualTo(1));
            var e = _progress[0];
            Assert.That(e.EntityId, Is.EqualTo(_entityId));
            Assert.That(e.AchievementId, Is.EqualTo("ach_hunter"));
            Assert.That(e.CriterionId, Is.EqualTo("crit_kills"));
            Assert.That(e.PreviousValue, Is.EqualTo(0));
            Assert.That(e.NewValue, Is.EqualTo(5));
            Assert.That(e.RequiredValue, Is.EqualTo(10));
            Assert.That(e.CriterionCompleted, Is.False);
            Assert.That(e.CompletionPercentage, Is.EqualTo(0.25f).Within(0.0001f)); // (0.5 + 0) / 2
            Assert.That(_criteria, Is.Empty);
            Assert.That(_unlocks, Is.Empty);
        }

        [Test]
        public void UnchangedProgress_PublishesNothing()
        {
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_kills", 5);
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_kills", 5);

            Assert.That(_progress.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReachingRequiredValue_PublishesCriterionCompleted()
        {
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_pelts", 4);

            Assert.That(_criteria.Count, Is.EqualTo(1));
            var e = _criteria[0];
            Assert.That(e.EntityId, Is.EqualTo(_entityId));
            Assert.That(e.AchievementId, Is.EqualTo("ach_hunter"));
            Assert.That(e.CriterionId, Is.EqualTo("crit_pelts"));
            Assert.That(e.CriterionType, Is.EqualTo(CriterionType.ItemCollected));
            Assert.That(e.FinalValue, Is.EqualTo(4));
            Assert.That(e.RequiredValue, Is.EqualTo(4));

            Assert.That(_progress.Count, Is.EqualTo(1));
            Assert.That(_progress[0].CriterionCompleted, Is.True);
            Assert.That(_unlocks, Is.Empty, "the other criterion is still incomplete");

            // Going further past the target is progress, not a second completion
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_pelts", 6);
            Assert.That(_criteria.Count, Is.EqualTo(1));
            Assert.That(_progress.Count, Is.EqualTo(2));
            Assert.That(_progress[1].CriterionCompleted, Is.False);
        }

        [Test]
        public void Unlocking_PublishesAchievementUnlocked()
        {
            var before = DateTime.UtcNow;
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_pelts", 4);
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_kills", 10);
            var after = DateTime.UtcNow;

            Assert.That(_unlocks.Count, Is.EqualTo(1));
            var e = _unlocks[0];
            Assert.That(e.EntityId, Is.EqualTo(_entityId));
            Assert.That(e.AchievementId, Is.EqualTo("ach_hunter"));
            Assert.That(e.Tier, Is.EqualTo(AchievementTier.Rare));
            Assert.That(e.Points, Is.EqualTo(50));
            Assert.That(e.UnlockedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(e.UnlockedAt, Is.LessThanOrEqualTo(after));
            Assert.That(_criteria.Count, Is.EqualTo(2));

            // Already unlocked: no second event
            Assert.That(_manager.UnlockAchievement(_entityId, "ach_hunter"), Is.False);
            Assert.That(_unlocks.Count, Is.EqualTo(1));
        }

        [Test]
        public void ManualUnlock_PublishesAchievementUnlocked()
        {
            Assert.That(_manager.UnlockAchievement(_entityId, "ach_hunter"), Is.True);

            Assert.That(_unlocks.Count, Is.EqualTo(1));
            Assert.That(_unlocks[0].AchievementId, Is.EqualTo("ach_hunter"));
            Assert.That(_progress, Is.Empty);
        }

        [Test]
        public void RetroactiveUnlock_PublishesProgressAndUnlock()
        {
            _manager.UpdateCriterionProgress(_entityId, "ach_hunter", "crit_pelts", 4);
            _progress.Clear();
            _criteria.Clear();

            var unlocked = _manager.CheckRetroactiveUnlocks(_entityId, CriterionType.KillCount,
                new Dictionary<string, int> { { "wolf", 12 } });

            Assert.That(unlocked, Is.EquivalentTo(new[] { "ach_hunter" }));
            Assert.That(_progress.Count, Is.EqualTo(1));
            Assert.That(_progress[0].NewValue, Is.EqualTo(12));
            Assert.That(_criteria.Count, Is.EqualTo(1));
            Assert.That(_criteria[0].CriterionId, Is.EqualTo("crit_kills"));
            Assert.That(_unlocks.Count, Is.EqualTo(1));
        }

        [Test]
        public void WithoutPublisher_ManagerStillWorks()
        {
            var manager = new AchievementManager(_registry, _entityManager);
            int entityId = _entityManager.CreateEntity();
            manager.InitializeEntityAchievements(entityId);

            manager.UpdateCriterionProgress(entityId, "ach_hunter", "crit_pelts", 4);
            manager.UpdateCriterionProgress(entityId, "ach_hunter", "crit_kills", 10);

            Assert.That(manager.IsAchievementUnlocked(entityId, "ach_hunter"), Is.True);
            Assert.That(_progress, Is.Empty);
            Assert.That(_unlocks, Is.Empty);
        }
    }
}
