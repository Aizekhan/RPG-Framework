using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Progression.Tests
{
    public sealed class ProgressionEventTests
    {
        [Test]
        public void Tracker_PublishesExperienceAndLevelUp()
        {
            var bus = new EventBus();
            var gained = new List<ExperienceGainedEvent>();
            var levels = new List<LevelUpEvent>();
            bus.Subscribe<ExperienceGainedEvent>(gained.Add);
            bus.Subscribe<LevelUpEvent>(levels.Add);
            var tracker = new ProgressionTracker(LevelTable.CreateLinear(maxLevel: 10, baseXP: 100), bus, () => 12.5f);

            tracker.AddExperience(1, 1);
            tracker.AddExperience(1, 1000);

            Assert.That(gained.Count, Is.EqualTo(2));
            Assert.That(gained[1].XPAmount, Is.EqualTo(1000));
            Assert.That(gained[1].TotalXP, Is.EqualTo(1001));
            Assert.That(gained[1].Timestamp, Is.EqualTo(12.5f));
            Assert.That(levels.Count, Is.EqualTo(1));
            Assert.That(levels[0].OldLevel, Is.EqualTo(1));
            Assert.That(levels[0].NewLevel, Is.EqualTo(tracker.GetLevel(1)).And.GreaterThan(1));
            Assert.That(levels[0].LevelsGained, Is.EqualTo(levels[0].NewLevel - 1));
        }

        [Test]
        public void StatGrowthPublisher_PublishesGrowthForEveryChangedStatOnLevelUp()
        {
            var bus = new EventBus();
            var formula = new StatGrowthFormula();
            formula.RegisterStatGrowth(new StatGrowthData("Strength", 10f, 2f));
            formula.RegisterStatGrowth(new StatGrowthData("Luck", 5f, 0f));
            var increased = new List<StatIncreasedEvent>();
            bus.Subscribe<StatIncreasedEvent>(increased.Add);
            using var publisher = new StatGrowthPublisher(bus, formula);
            var tracker = new ProgressionTracker(LevelTable.CreateLinear(maxLevel: 10, baseXP: 100), bus);

            tracker.SetLevel(3, 1);
            tracker.AddExperience(3, tracker.GetXPRequiredForLevel(3));

            int level = tracker.GetLevel(3);
            Assert.That(level, Is.GreaterThan(1));
            Assert.That(increased.Count, Is.EqualTo(1), "Luck does not grow");
            Assert.That(increased[0].EntityId, Is.EqualTo(3));
            Assert.That(increased[0].StatId, Is.EqualTo("Strength"));
            Assert.That(increased[0].OldValue, Is.EqualTo(10f));
            Assert.That(increased[0].NewValue, Is.EqualTo(10f + 2f * (level - 1)));
            Assert.That(increased[0].Level, Is.EqualTo(level));
        }

        [Test]
        public void StatGrowthPublisher_StopsAfterDispose()
        {
            var bus = new EventBus();
            var formula = new StatGrowthFormula();
            formula.RegisterStatGrowth(new StatGrowthData("Strength", 10f, 2f));
            var publisher = new StatGrowthPublisher(bus, formula);
            publisher.Dispose();

            bus.Publish(new LevelUpEvent { EntityId = 1, OldLevel = 1, NewLevel = 2, LevelsGained = 1 });

            Assert.That(publisher.PublishedCount, Is.EqualTo(0));
        }

        [Test]
        public void UnlockManager_PublishesFirstUnlockOnly()
        {
            var bus = new EventBus();
            var unlocks = new List<UnlockAchievedEvent>();
            bus.Subscribe<UnlockAchievedEvent>(unlocks.Add);
            var manager = new UnlockManager(bus);
            manager.RegisterUnlock(new UnlockData("mount", "Mount", 5, "Feature"));

            manager.AchieveUnlock(7, "mount");
            manager.AchieveUnlock(7, "mount");

            Assert.That(unlocks.Count, Is.EqualTo(1));
            Assert.That(unlocks[0].EntityId, Is.EqualTo(7));
            Assert.That(unlocks[0].UnlockName, Is.EqualTo("Mount"));
            Assert.That(unlocks[0].Level, Is.EqualTo(5));
        }
    }
}
