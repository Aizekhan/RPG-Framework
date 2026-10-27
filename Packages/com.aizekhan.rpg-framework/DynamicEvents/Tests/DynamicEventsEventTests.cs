using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.DynamicEvents.Runtime;
using RPGFramework.Events;

namespace RPGFramework.DynamicEvents.Tests
{
    /// <summary>
    /// Proves that <see cref="EventManager"/> publishes the dynamic event lifecycle on a real <see cref="EventBus"/>:
    /// spawn, start, join, objective progress, success/failure and completion, each once and with the right values.
    /// </summary>
    public sealed class DynamicEventsEventTests
    {
        private const string Template = "defense_template";
        private const string Location = "village_01";

        private EventBus _bus;
        private EventManager _manager;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _manager = new EventManager(_bus);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        private IDynamicEvent CreateDefense(float maxDuration = 600f, int requiredKills = 10)
        {
            _manager.RegisterEventFactory(Template, (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Hard, "Village Defense", "Defend the village",
                    loc, 100f, maxDuration,
                    new[] { new EventObjective("kill", id, "Kill raiders", "Kill", "raider", requiredKills) }));
            return _manager.CreateEvent(Template, Location);
        }

        private List<T> Collect<T>() where T : IEvent
        {
            var list = new List<T>();
            _bus.Subscribe<T>(list.Add);
            return list;
        }

        [Test]
        public void Spawning_PublishesEventSpawned()
        {
            var spawned = Collect<EventSpawnedEvent>();
            _manager.RegisterSpawner(new EventSpawner("spawner1", Template, Location, new EventSpawnRule(0f, 0f)));

            _manager.Update(0.1f, new Dictionary<string, object>());

            Assert.That(spawned, Has.Count.EqualTo(1));
            Assert.That(spawned[0].EventId, Does.StartWith(Template));
            Assert.That(spawned[0].TemplateId, Is.EqualTo(Template));
            Assert.That(spawned[0].LocationId, Is.EqualTo(Location));
            Assert.That(spawned[0].SpawnerId, Is.EqualTo("spawner1"));
        }

        [Test]
        public void Creating_PublishesEventStarted()
        {
            var started = Collect<EventStartedEvent>();

            var dynamicEvent = CreateDefense();

            Assert.That(started, Has.Count.EqualTo(1));
            Assert.That(started[0].EventId, Is.EqualTo(dynamicEvent.EventId));
            Assert.That(started[0].EventType, Is.EqualTo(EventType.Defense));
            Assert.That(started[0].Name, Is.EqualTo("Village Defense"));
            Assert.That(started[0].LocationId, Is.EqualTo(Location));
            Assert.That(started[0].Difficulty, Is.EqualTo(EventDifficulty.Hard));
        }

        [Test]
        public void Joining_PublishesParticipantJoined_OncePerParticipant()
        {
            var joined = Collect<ParticipantJoinedEvent>();
            var dynamicEvent = CreateDefense();

            _manager.JoinEvent(dynamicEvent.EventId, "player1");
            _manager.JoinEvent(dynamicEvent.EventId, "player1"); // already in: no event
            _manager.JoinEvent(dynamicEvent.EventId, "player2");

            Assert.That(joined, Has.Count.EqualTo(2));
            Assert.That(joined[0].EventId, Is.EqualTo(dynamicEvent.EventId));
            Assert.That(joined[0].ParticipantId, Is.EqualTo("player1"));
            Assert.That(joined[0].NewParticipantCount, Is.EqualTo(1));
            Assert.That(joined[1].ParticipantId, Is.EqualTo("player2"));
            Assert.That(joined[1].NewParticipantCount, Is.EqualTo(2));
        }

        [Test]
        public void ProgressingObjective_PublishesObjectiveProgress()
        {
            var progress = Collect<ObjectiveProgressEvent>();
            var dynamicEvent = CreateDefense(requiredKills: 5);

            _manager.UpdateObjectiveProgress(dynamicEvent.EventId, "kill", 3, "player1");
            _manager.UpdateObjectiveProgress(dynamicEvent.EventId, "kill", 3, "player1");
            _manager.UpdateObjectiveProgress(dynamicEvent.EventId, "kill", 1, "player1"); // already complete: no event

            Assert.That(progress, Has.Count.EqualTo(2));
            Assert.That(progress[0].EventId, Is.EqualTo(dynamicEvent.EventId));
            Assert.That(progress[0].ObjectiveId, Is.EqualTo("kill"));
            Assert.That(progress[0].CurrentCount, Is.EqualTo(3));
            Assert.That(progress[0].RequiredCount, Is.EqualTo(5));
            Assert.That(progress[0].IsComplete, Is.False);
            Assert.That(progress[0].ContributorId, Is.EqualTo("player1"));
            Assert.That(progress[1].CurrentCount, Is.EqualTo(5));
            Assert.That(progress[1].IsComplete, Is.True);
        }

        [Test]
        public void CompletingObjectives_PublishesEventSucceeded()
        {
            var succeeded = Collect<EventSucceededEvent>();
            var failed = Collect<EventFailedEvent>();
            var dynamicEvent = CreateDefense(requiredKills: 1);
            _manager.JoinEvent(dynamicEvent.EventId, "player1");
            _manager.UpdateObjectiveProgress(dynamicEvent.EventId, "kill", 1, "player1");

            _manager.Update(2f, new Dictionary<string, object>());
            _manager.Update(2f, new Dictionary<string, object>()); // already removed: nothing more

            Assert.That(failed, Is.Empty);
            Assert.That(succeeded, Has.Count.EqualTo(1));
            Assert.That(succeeded[0].EventId, Is.EqualTo(dynamicEvent.EventId));
            Assert.That(succeeded[0].ParticipantCount, Is.EqualTo(1));
            Assert.That(succeeded[0].CompletionTime, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void RunningOutOfTime_PublishesEventFailed()
        {
            var failed = Collect<EventFailedEvent>();
            var succeeded = Collect<EventSucceededEvent>();
            var dynamicEvent = CreateDefense(maxDuration: 5f);

            _manager.Update(3f, new Dictionary<string, object>());
            _manager.Update(3f, new Dictionary<string, object>());

            Assert.That(succeeded, Is.Empty);
            Assert.That(failed, Has.Count.EqualTo(1));
            Assert.That(failed[0].EventId, Is.EqualTo(dynamicEvent.EventId));
            Assert.That(failed[0].Reason, Is.EqualTo("time expired"));
            Assert.That(failed[0].ParticipantCount, Is.EqualTo(0));
        }

        [Test]
        public void EndingEvent_PublishesEventCompleted_WithRewards()
        {
            var completed = Collect<EventCompletedEvent>();
            var dynamicEvent = CreateDefense(requiredKills: 2);
            _manager.JoinEvent(dynamicEvent.EventId, "player1");
            _manager.JoinEvent(dynamicEvent.EventId, "player2");
            _manager.UpdateObjectiveProgress(dynamicEvent.EventId, "kill", 2, "player1"); // 20 contribution: Helper

            _manager.Update(1f, new Dictionary<string, object>());

            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].EventId, Is.EqualTo(dynamicEvent.EventId));
            Assert.That(completed[0].FinalState, Is.EqualTo(EventState.Success));
            Assert.That(completed[0].TotalParticipants, Is.EqualTo(2));
            Assert.That(completed[0].Rewards.Count, Is.EqualTo(1), "player2 did nothing and earns no reward");
            Assert.That(completed[0].Rewards["player1"], Is.EqualTo("bronze"));
        }
    }
}
