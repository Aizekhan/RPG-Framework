using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Achievements.Runtime.Contracts;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Achievements.Tests
{
    /// <summary>
    /// The tracker counts deeds by type and target, moves the criteria that count them (no target: all of them),
    /// keeps high-water marks, and puts a profile back without announcing old unlocks again.
    /// </summary>
    public sealed class AchievementTrackerTests
    {
        private EventBus _bus;
        private EntityManager _entities;
        private AchievementRegistry _registry;
        private readonly List<AchievementUnlockedEvent> _unlocks = new List<AchievementUnlockedEvent>();
        private int _player;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _bus.Subscribe<AchievementUnlockedEvent>(_unlocks.Add);
            _entities = new EntityManager();
            _player = _entities.CreateEntity();
            _registry = new AchievementRegistry();
            _registry.RegisterAchievement(Make("wolfbane", new AchievementCriterion("wolves", CriterionType.KillCount, "wolf", 3)));
            _registry.RegisterAchievement(Make("slayer", new AchievementCriterion("any", CriterionType.KillCount, "", 5)));
            _registry.RegisterAchievement(Make("veteran", new AchievementCriterion("level", CriterionType.LevelReached, "", 10)));
            _unlocks.Clear();
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        private static Achievement Make(string id, IAchievementCriterion criterion) =>
            new Achievement(id, id, "", "combat", 10, AchievementTier.Common, new List<IAchievementCriterion> { criterion },
                new List<Runtime.IAchievementReward>(), new List<string>());

        [Test]
        public void Deeds_MoveTheirCriteria_AndAnyTargetCountsThemAll()
        {
            var tracker = new AchievementTracker(_registry, _entities, _bus);
            tracker.Add(_player, CriterionType.KillCount, "wolf", 2);
            tracker.Add(_player, CriterionType.KillCount, "boar");

            Assert.That(tracker.CountOf(_player, CriterionType.KillCount, "wolf"), Is.EqualTo(2));
            Assert.That(tracker.CountOf(_player, CriterionType.KillCount), Is.EqualTo(3));
            Assert.That(tracker.ProgressOf(_player, "wolfbane"), Is.EqualTo(2f / 3f).Within(1e-4f));
            Assert.That(_unlocks, Is.Empty);

            tracker.Add(_player, CriterionType.KillCount, "wolf");
            Assert.That(tracker.IsUnlocked(_player, "wolfbane"), Is.True);
            tracker.Add(_player, CriterionType.KillCount, "wolf");
            Assert.That(tracker.IsUnlocked(_player, "slayer"), Is.True, "five of any kind");
            Assert.That(_unlocks.ConvertAll(u => u.AchievementId), Is.EqualTo(new[] { "wolfbane", "slayer" }));

            tracker.Reach(_player, CriterionType.LevelReached, "", 7);
            tracker.Reach(_player, CriterionType.LevelReached, "", 4);
            Assert.That(tracker.CountOf(_player, CriterionType.LevelReached), Is.EqualTo(7), "a high-water mark");
        }

        [Test]
        public void AProfile_ComesBack_WithoutAnnouncingOldUnlocks()
        {
            var first = new AchievementTracker(_registry, _entities, _bus);
            first.Add(_player, CriterionType.KillCount, "wolf", 3);
            first.Reach(_player, CriterionType.LevelReached, "", 12);
            var record = first.Capture(_player);
            Assert.That(record.Unlocked, Is.EquivalentTo(new[] { "wolfbane", "veteran" }));

            var otherEntities = new EntityManager();
            int player = otherEntities.CreateEntity();
            // An update added an achievement the saved counts already reach
            _registry.RegisterAchievement(Make("hunter", new AchievementCriterion("any", CriterionType.KillCount, "", 2)));
            _unlocks.Clear();
            var second = new AchievementTracker(_registry, otherEntities, _bus);
            second.Restore(player, record);

            Assert.That(second.IsUnlocked(player, "wolfbane"), Is.True);
            Assert.That(second.IsUnlocked(player, "veteran"), Is.True);
            Assert.That(second.CountOf(player, CriterionType.KillCount, "wolf"), Is.EqualTo(3));
            Assert.That(second.IsUnlocked(player, "hunter"), Is.True);
            Assert.That(_unlocks.ConvertAll(u => u.AchievementId), Is.EqualTo(new[] { "hunter" }), "only the new one is news");
        }
    }
}
