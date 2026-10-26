using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Quests.Tests
{
    public sealed class QuestEventTests
    {
        private EventBus _bus;
        private QuestManager _manager;
        private int _hero;
        private readonly List<string> _log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            var registry = new QuestRegistry();
            registry.RegisterQuest(new Quest("wolves", "Wolves", "Hunt wolves",
                new List<IQuestObjective> { new QuestObjective("kill", ObjectiveType.Kill, "wolf", 2, "Kill 2 wolves", false) },
                new List<IQuestReward> { new QuestReward(RewardType.Experience, "xp", 50) },
                new List<string>(), 1, true));

            var entities = new EntityManager();
            _hero = entities.CreateEntity();
            _bus = new EventBus();
            _manager = new QuestManager(entities, registry, _bus);

            _log.Clear();
            _bus.Subscribe<QuestAcceptedEvent>(e => _log.Add($"accepted {e.QuestId}"));
            _bus.Subscribe<ObjectiveProgressedEvent>(e => _log.Add($"progress {e.ObjectiveId} {e.CurrentProgress}/{e.RequiredProgress} {e.IsCompleted}"));
            _bus.Subscribe<QuestCompletedEvent>(e => _log.Add($"completed {e.QuestId} rewards {e.Rewards.Count}"));
            _bus.Subscribe<QuestAbandonedEvent>(e => _log.Add($"abandoned {e.QuestId}"));
            _bus.Subscribe<QuestFailedEvent>(e => _log.Add($"failed {e.QuestId} {e.FailureReason}"));
        }

        [Test]
        public void Manager_PublishesLifecycleEvents()
        {
            Assert.That(_manager.AcceptQuest(_hero, "wolves"), Is.True);
            _manager.UpdateObjectiveProgress(_hero, "wolves", "kill", 1);
            _manager.UpdateObjectiveProgress(_hero, "wolves", "kill", 5);
            _manager.UpdateObjectiveProgress(_hero, "wolves", "kill", 1); // already capped: no event
            Assert.That(_manager.CompleteQuest(_hero, "wolves"), Is.True);

            CollectionAssert.AreEqual(new[]
            {
                "accepted wolves",
                "progress kill 1/2 False",
                "progress kill 2/2 True",
                "completed wolves rewards 1"
            }, _log);
        }

        [Test]
        public void AbandonAndFail_PublishAndFreeTheQuest()
        {
            _manager.AcceptQuest(_hero, "wolves");
            Assert.That(_manager.AbandonQuest(_hero, "wolves"), Is.True);
            _manager.AcceptQuest(_hero, "wolves");
            Assert.That(_manager.FailQuest(_hero, "wolves", "timeout"), Is.True);
            Assert.That(_manager.FailQuest(_hero, "wolves"), Is.False);

            CollectionAssert.AreEqual(new[]
            {
                "accepted wolves", "abandoned wolves", "accepted wolves", "failed wolves timeout"
            }, _log);
            Assert.That(_manager.GetActiveQuests(_hero), Is.Empty);
            Assert.That(_manager.CanAcceptQuest(_hero, "wolves"), Is.True);
        }

        [Test]
        public void Manager_WithoutPublisher_StillWorks()
        {
            var registry = new QuestRegistry();
            registry.RegisterQuest(new Quest("q", "Q", "", new List<IQuestObjective>(), new List<IQuestReward>(),
                new List<string>(), 1, false));
            var entities = new EntityManager();
            int entity = entities.CreateEntity();
            var manager = new QuestManager(entities, registry);

            Assert.That(manager.AcceptQuest(entity, "q"), Is.True);
            Assert.That(manager.CompleteQuest(entity, "q"), Is.True);
        }
    }
}
