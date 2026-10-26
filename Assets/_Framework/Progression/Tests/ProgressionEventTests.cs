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
