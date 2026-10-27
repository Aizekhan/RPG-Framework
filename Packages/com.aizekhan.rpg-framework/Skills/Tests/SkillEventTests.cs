// Packages/com.aizekhan.rpg-framework/Skills/Tests/SkillEventTests.cs

using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Skills.Tests
{
    /// <summary>
    /// Proves SkillManager publishes SkillLearned and SkillLevelUp on a real event bus, once per successful change
    /// and never for a refused one. The base manager keeps no skill data, so the tests use a small in-memory
    /// subclass, the same way a game-layer adapter would supply storage.
    /// </summary>
    [TestFixture]
    public class SkillEventTests
    {
        private sealed class InMemorySkillManager : SkillManager
        {
            private readonly Dictionary<int, SkillProgressData> _data = new Dictionary<int, SkillProgressData>();

            public InMemorySkillManager(IEntityManager entityManager, ISkillTree skillTree, IEventPublisher events)
                : base(entityManager, skillTree, events)
            {
            }

            public void Track(int entityId) => _data[entityId] = SkillProgressData.CreateEmpty();

            protected override bool TryGetSkillProgressData(int entityId, out SkillProgressData data) =>
                _data.TryGetValue(entityId, out data);

            protected override void UpdateSkillProgressData(int entityId, SkillProgressData data) =>
                _data[entityId] = data;
        }

        private EventBus _bus;
        private InMemorySkillManager _manager;
        private const int EntityId = 7;
        private const string SkillId = "skill_fireball";

        private readonly List<SkillLearnedEvent> _learned = new List<SkillLearnedEvent>();
        private readonly List<SkillLevelUpEvent> _levelUps = new List<SkillLevelUpEvent>();

        [SetUp]
        public void SetUp()
        {
            _learned.Clear();
            _levelUps.Clear();

            _bus = new EventBus();
            _bus.Subscribe<SkillLearnedEvent>(e => _learned.Add(e));
            _bus.Subscribe<SkillLevelUpEvent>(e => _levelUps.Add(e));

            var tree = new SkillTree("tree_mage", "Mage Skills", "Spells");
            tree.AddSkill(new Skill(SkillId, "Fireball", "Throws fire", 2, SkillType.Active));

            _manager = new InMemorySkillManager(new EntityManager(), tree, _bus);
            _manager.Track(EntityId);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        [Test]
        public void Learning_PublishesSkillLearned()
        {
            Assert.That(_manager.LearnSkill(EntityId, SkillId, 1), Is.True);

            Assert.That(_learned.Count, Is.EqualTo(1));
            Assert.That(_learned[0].EntityId, Is.EqualTo(EntityId));
            Assert.That(_learned[0].SkillId, Is.EqualTo(SkillId));
            Assert.That(_learned[0].Level, Is.EqualTo(1));
            Assert.That(_learned[0].TimeStamp, Is.EqualTo(0f));

            // Learning it again is refused and publishes nothing
            Assert.That(_manager.LearnSkill(EntityId, SkillId, 1), Is.False);
            Assert.That(_learned.Count, Is.EqualTo(1));
            Assert.That(_levelUps, Is.Empty);
        }

        [Test]
        public void LevelingUp_PublishesSkillLevelUp()
        {
            _manager.LearnSkill(EntityId, SkillId, 1);

            Assert.That(_manager.IncreaseSkillLevel(EntityId, SkillId), Is.True);

            Assert.That(_levelUps.Count, Is.EqualTo(1));
            Assert.That(_levelUps[0].EntityId, Is.EqualTo(EntityId));
            Assert.That(_levelUps[0].SkillId, Is.EqualTo(SkillId));
            Assert.That(_levelUps[0].OldLevel, Is.EqualTo(1));
            Assert.That(_levelUps[0].NewLevel, Is.EqualTo(2));
            Assert.That(_levelUps[0].TimeStamp, Is.EqualTo(0f));

            // At max level (2) the increase is refused and publishes nothing
            Assert.That(_manager.IncreaseSkillLevel(EntityId, SkillId), Is.False);
            Assert.That(_levelUps.Count, Is.EqualTo(1));
        }

        [Test]
        public void LevelingUnlearnedSkill_PublishesNothing()
        {
            Assert.That(_manager.IncreaseSkillLevel(EntityId, SkillId), Is.False);

            Assert.That(_levelUps, Is.Empty);
            Assert.That(_learned, Is.Empty);
        }
    }
}
