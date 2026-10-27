using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Companions.Runtime;
using RPGFramework.Events;

namespace RPGFramework.Companions.Tests
{
    /// <summary>
    /// Proves that a CompanionManager built with an event bus publishes each companion event exactly once,
    /// with the right values, when the matching action happens (and that companions it creates share the bus).
    /// </summary>
    [TestFixture]
    public class CompanionEventTests
    {
        private EventBus _bus;
        private CompanionManager _manager;

        [SetUp]
        public void Setup()
        {
            _bus = new EventBus();
            _manager = new CompanionManager(_bus);
        }

        [TearDown]
        public void Teardown()
        {
            _bus.Dispose();
        }

        [Test]
        public void Summoning_PublishesCompanionSummoned()
        {
            var received = new List<CompanionSummonedEvent>();
            _bus.Subscribe<CompanionSummonedEvent>(received.Add);
            var companion = _manager.CreateCompanion("wolf_001", 7, CompanionType.CombatPet, "Fenrir");

            companion.Summon();
            companion.Summon(); // already active: no second event

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("wolf_001", received[0].CompanionId);
            Assert.AreEqual(companion.EntityId, received[0].EntityId);
            Assert.AreEqual(7, received[0].OwnerId);
            Assert.AreEqual(CompanionType.CombatPet, received[0].CompanionType);
        }

        [Test]
        public void Dismissing_PublishesCompanionDismissed()
        {
            var received = new List<CompanionDismissedEvent>();
            _bus.Subscribe<CompanionDismissedEvent>(received.Add);
            var companion = _manager.CreateCompanion("wolf_001", 7, CompanionType.CombatPet, "Fenrir");

            companion.Dismiss(); // never summoned: nothing to dismiss
            companion.Summon();
            companion.Dismiss();

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("wolf_001", received[0].CompanionId);
            Assert.AreEqual(companion.EntityId, received[0].EntityId);
            Assert.AreEqual(7, received[0].OwnerId);
        }

        [Test]
        public void Killing_PublishesCompanionDeath()
        {
            var received = new List<CompanionDeathEvent>();
            _bus.Subscribe<CompanionDeathEvent>(received.Add);
            var companion = (Companion)_manager.CreateCompanion("wolf_001", 7, CompanionType.CombatPet, "Fenrir");

            companion.Kill();
            companion.Kill(); // already dead: no second event

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("wolf_001", received[0].CompanionId);
            Assert.AreEqual(companion.EntityId, received[0].EntityId);
            Assert.AreEqual(7, received[0].OwnerId);
            Assert.AreEqual(-1, received[0].KillerEntityId);
        }

        [Test]
        public void LevelingUp_PublishesCompanionLevelUp()
        {
            var received = new List<CompanionLevelUpEvent>();
            _bus.Subscribe<CompanionLevelUpEvent>(received.Add);
            var companion = (Companion)_manager.CreateCompanion("wolf_001", 7, CompanionType.CombatPet, "Fenrir");

            companion.LevelUp();

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("wolf_001", received[0].CompanionId);
            Assert.AreEqual(companion.EntityId, received[0].EntityId);
            Assert.AreEqual(1, received[0].OldLevel);
            Assert.AreEqual(2, received[0].NewLevel);
        }

        [Test]
        public void Feeding_PublishesCompanionFed()
        {
            var received = new List<CompanionFedEvent>();
            _bus.Subscribe<CompanionFedEvent>(received.Add);
            var companion = _manager.CreateCompanion("wolf_001", 7, CompanionType.CombatPet, "Fenrir");
            _manager.GetCompanionStats("wolf_001").ModifyHunger(-50); // 100 -> 50

            Assert.IsTrue(_manager.FeedCompanion("wolf_001", "food_meat"));

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("wolf_001", received[0].CompanionId);
            Assert.AreEqual(companion.EntityId, received[0].EntityId);
            Assert.AreEqual("food_meat", received[0].FoodItemId);
            Assert.AreEqual(30, received[0].HungerRestored);
            Assert.AreEqual(10, received[0].HappinessGained);
            Assert.AreEqual(2, received[0].LoyaltyGained);
        }

        [Test]
        public void LoyaltyCrossingALevel_PublishesLoyaltyChanged()
        {
            var received = new List<LoyaltyChangedEvent>();
            _bus.Subscribe<LoyaltyChangedEvent>(received.Add);
            var companion = _manager.CreateCompanion("wolf_001", 7, CompanionType.CombatPet, "Fenrir");

            companion.ModifyLoyalty(10); // 10: still Neutral, no event
            companion.ModifyLoyalty(20); // 30: Friendly

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("wolf_001", received[0].CompanionId);
            Assert.AreEqual(companion.EntityId, received[0].EntityId);
            Assert.AreEqual(LoyaltyLevel.Neutral, received[0].OldLoyalty);
            Assert.AreEqual(LoyaltyLevel.Friendly, received[0].NewLoyalty);
            Assert.AreEqual(30, received[0].LoyaltyValue);
        }
    }
}
