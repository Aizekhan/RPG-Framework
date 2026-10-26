using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.DynamicEvents.Runtime;

namespace RPGFramework.DynamicEvents.Tests
{
    [TestFixture]
    public class DynamicEventsSystemTests
    {
        // EventObjective Tests
        [Test]
        public void EventObjective_Create_SetsPropertiesCorrectly()
        {
            var objective = new EventObjective(
                "obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10, false, true);

            Assert.AreEqual("obj1", objective.ObjectiveId);
            Assert.AreEqual("evt1", objective.EventId);
            Assert.AreEqual("Kill 10 enemies", objective.Description);
            Assert.AreEqual("Kill", objective.ObjectiveType);
            Assert.AreEqual("enemy_orc", objective.TargetId);
            Assert.AreEqual(10, objective.RequiredCount);
            Assert.AreEqual(0, objective.CurrentCount);
            Assert.IsFalse(objective.IsComplete);
            Assert.IsFalse(objective.IsOptional);
            Assert.IsTrue(objective.TracksContribution);
        }

        [Test]
        public void EventObjective_UpdateProgress_IncreasesCount()
        {
            var objective = new EventObjective("obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10);

            objective.UpdateProgress(3, "player1");

            Assert.AreEqual(3, objective.CurrentCount);
            Assert.AreEqual(0.3f, objective.GetProgressPercentage(), 0.01f);
            Assert.IsFalse(objective.IsComplete);
        }

        [Test]
        public void EventObjective_UpdateProgress_CompletesObjective()
        {
            var objective = new EventObjective("obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10);

            objective.UpdateProgress(10, "player1");

            Assert.AreEqual(10, objective.CurrentCount);
            Assert.AreEqual(1.0f, objective.GetProgressPercentage());
            Assert.IsTrue(objective.IsComplete);
        }

        [Test]
        public void EventObjective_UpdateProgress_DoesNotExceedRequired()
        {
            var objective = new EventObjective("obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10);

            objective.UpdateProgress(15, "player1");

            Assert.AreEqual(10, objective.CurrentCount);
            Assert.IsTrue(objective.IsComplete);
        }

        [Test]
        public void EventObjective_TracksContribution_PerParticipant()
        {
            var objective = new EventObjective("obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10);

            objective.UpdateProgress(3, "player1");
            objective.UpdateProgress(5, "player2");
            objective.UpdateProgress(2, "player1");

            Assert.AreEqual(5, objective.GetParticipantContribution("player1"));
            Assert.AreEqual(5, objective.GetParticipantContribution("player2"));
            Assert.AreEqual(10, objective.CurrentCount);
        }

        // EventParticipant Tests
        [Test]
        public void EventParticipant_Create_InitializesCorrectly()
        {
            var participant = new EventParticipant("player1", "evt1", 100f);

            Assert.AreEqual("player1", participant.ParticipantId);
            Assert.AreEqual("evt1", participant.EventId);
            Assert.AreEqual(100f, participant.JoinTime);
            Assert.AreEqual(0f, participant.ContributionScore);
            Assert.AreEqual(ParticipationLevel.Observer, participant.ParticipationLevel);
            Assert.IsTrue(participant.IsActive);
            Assert.IsFalse(participant.IsEligibleForRewards); // Observer tier earns "none"
        }

        [Test]
        public void EventParticipant_AddContribution_UpdatesLevel()
        {
            var participant = new EventParticipant("player1", "evt1", 100f);

            participant.AddContribution(60f);

            Assert.AreEqual(60f, participant.ContributionScore);
            Assert.AreEqual(ParticipationLevel.Contributor, participant.ParticipationLevel);
            Assert.IsTrue(participant.IsEligibleForRewards);
        }

        [Test]
        public void EventParticipant_AddContribution_ReachesHeroLevel()
        {
            var participant = new EventParticipant("player1", "evt1", 100f);

            participant.AddContribution(200f);

            Assert.AreEqual(200f, participant.ContributionScore);
            Assert.AreEqual(ParticipationLevel.Hero, participant.ParticipationLevel);
        }

        [Test]
        public void EventParticipant_AddContribution_ReachesLegendLevel()
        {
            var participant = new EventParticipant("player1", "evt1", 100f);

            participant.AddContribution(600f);

            Assert.AreEqual(600f, participant.ContributionScore);
            Assert.AreEqual(ParticipationLevel.Legend, participant.ParticipationLevel);
        }

        [Test]
        public void EventParticipant_Leave_MarksInactive()
        {
            var participant = new EventParticipant("player1", "evt1", 100f);

            participant.Leave(200f);

            Assert.IsFalse(participant.IsActive);
            Assert.AreEqual(200f, participant.LeaveTime);
        }

        [Test]
        public void EventParticipant_ObserverLevel_NotEligibleForRewards()
        {
            var participant = new EventParticipant("player1", "evt1", 100f);

            Assert.AreEqual(ParticipationLevel.Observer, participant.ParticipationLevel);
            Assert.IsFalse(participant.IsEligibleForRewards);
        }

        // DynamicEvent Tests
        [Test]
        public void DynamicEvent_Create_InitializesCorrectly()
        {
            var dynamicEvent = new DynamicEvent(
                "evt1", EventType.Defense, EventDifficulty.Normal,
                "Village Defense", "Defend the village", "village_01", 100f, 600f);

            Assert.AreEqual("evt1", dynamicEvent.EventId);
            Assert.AreEqual(EventType.Defense, dynamicEvent.EventType);
            Assert.AreEqual(EventState.Pending, dynamicEvent.State);
            Assert.AreEqual(EventDifficulty.Normal, dynamicEvent.Difficulty);
            Assert.AreEqual("Village Defense", dynamicEvent.Name);
            Assert.AreEqual("village_01", dynamicEvent.LocationId);
            Assert.AreEqual(600f, dynamicEvent.MaxDuration);
            Assert.AreEqual(600f, dynamicEvent.TimeRemaining);
        }

        [Test]
        public void DynamicEvent_Start_ChangesStateToActive()
        {
            var dynamicEvent = new DynamicEvent(
                "evt1", EventType.Defense, EventDifficulty.Normal,
                "Village Defense", "Defend the village", "village_01", 100f, 600f);

            dynamicEvent.Start();

            Assert.AreEqual(EventState.Active, dynamicEvent.State);
        }

        [Test]
        public void DynamicEvent_Update_DecreasesTimeRemaining()
        {
            var dynamicEvent = new DynamicEvent(
                "evt1", EventType.Defense, EventDifficulty.Normal,
                "Village Defense", "Defend the village", "village_01", 100f, 600f);

            dynamicEvent.Start();
            dynamicEvent.Update(10f);

            Assert.AreEqual(590f, dynamicEvent.TimeRemaining);
        }

        [Test]
        public void DynamicEvent_Update_FailsWhenTimeExpires()
        {
            var dynamicEvent = new DynamicEvent(
                "evt1", EventType.Defense, EventDifficulty.Normal,
                "Village Defense", "Defend the village", "village_01", 100f, 10f);

            dynamicEvent.Start();
            dynamicEvent.Update(15f);

            Assert.AreEqual(EventState.Failed, dynamicEvent.State);
            Assert.LessOrEqual(dynamicEvent.TimeRemaining, 0f);
        }

        [Test]
        public void DynamicEvent_WithObjectives_SucceedsWhenComplete()
        {
            var objective = new EventObjective("obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10);
            var dynamicEvent = new DynamicEvent(
                "evt1", EventType.Defense, EventDifficulty.Normal,
                "Village Defense", "Defend the village", "village_01", 100f, 600f,
                new[] { objective });

            dynamicEvent.Start();
            objective.UpdateProgress(10);
            dynamicEvent.Update(1f);

            Assert.AreEqual(EventState.Success, dynamicEvent.State);
        }

        [Test]
        public void DynamicEvent_GetProgress_ReturnsCorrectPercentage()
        {
            var objective1 = new EventObjective("obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10);
            var objective2 = new EventObjective("obj2", "evt1", "Collect 5 items", "Collect", "item_gold", 5);
            var dynamicEvent = new DynamicEvent(
                "evt1", EventType.Defense, EventDifficulty.Normal,
                "Village Defense", "Defend the village", "village_01", 100f, 600f,
                new IEventObjective[] { objective1, objective2 });

            objective1.UpdateProgress(5);
            objective2.UpdateProgress(5);

            float progress = dynamicEvent.GetProgress();
            Assert.AreEqual(0.75f, progress, 0.01f); // (0.5 + 1.0) / 2
        }

        [Test]
        public void DynamicEvent_CanJoin_OnlyWhenActive()
        {
            var dynamicEvent = new DynamicEvent(
                "evt1", EventType.Defense, EventDifficulty.Normal,
                "Village Defense", "Defend the village", "village_01", 100f, 600f);

            Assert.IsFalse(dynamicEvent.CanJoin("player1"));

            dynamicEvent.Start();
            Assert.IsTrue(dynamicEvent.CanJoin("player1"));

            dynamicEvent.End(true);
            Assert.IsFalse(dynamicEvent.CanJoin("player1"));
        }

        // EventManager Tests
        [Test]
        public void EventManager_CreateEvent_RegistersAndStartsEvent()
        {
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Normal,
                    "Village Defense", "Defend the village", loc, 100f, 600f));

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");

            Assert.IsNotNull(dynamicEvent);
            Assert.AreEqual(EventState.Active, dynamicEvent.State);
            Assert.AreEqual(1, manager.ActiveEvents.Count);
        }

        [Test]
        public void EventManager_GetEvent_ReturnsCorrectEvent()
        {
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Normal,
                    "Village Defense", "Defend the village", loc, 100f, 600f));

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");
            var retrieved = manager.GetEvent(dynamicEvent.EventId);

            Assert.AreSame(dynamicEvent, retrieved);
        }

        [Test]
        public void EventManager_JoinEvent_AddsParticipant()
        {
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Normal,
                    "Village Defense", "Defend the village", loc, 100f, 600f));

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");
            var participant = manager.JoinEvent(dynamicEvent.EventId, "player1");

            Assert.IsNotNull(participant);
            Assert.AreEqual("player1", participant.ParticipantId);
            Assert.AreEqual(dynamicEvent.EventId, participant.EventId);
        }

        [Test]
        public void EventManager_GetParticipants_ReturnsAllParticipants()
        {
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Normal,
                    "Village Defense", "Defend the village", loc, 100f, 600f));

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");
            manager.JoinEvent(dynamicEvent.EventId, "player1");
            manager.JoinEvent(dynamicEvent.EventId, "player2");

            var participants = manager.GetParticipants(dynamicEvent.EventId);

            Assert.AreEqual(2, participants.Count);
        }

        [Test]
        public void EventManager_LeaveEvent_MarksParticipantInactive()
        {
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Normal,
                    "Village Defense", "Defend the village", loc, 100f, 600f));

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");
            manager.JoinEvent(dynamicEvent.EventId, "player1");
            manager.LeaveEvent(dynamicEvent.EventId, "player1");

            var participant = manager.GetParticipant(dynamicEvent.EventId, "player1");
            Assert.IsFalse(participant!.IsActive);
        }

        [Test]
        public void EventManager_UpdateObjectiveProgress_UpdatesObjective()
        {
            var objective = new EventObjective("obj1", "evt1", "Kill 10 enemies", "Kill", "enemy_orc", 10);
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
            {
                var evt = new DynamicEvent(id, EventType.Defense, EventDifficulty.Normal,
                    "Village Defense", "Defend the village", loc, 100f, 600f);
                evt.AddObjective(objective);
                return evt;
            });

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");
            manager.JoinEvent(dynamicEvent.EventId, "player1");
            manager.UpdateObjectiveProgress(dynamicEvent.EventId, "obj1", 5, "player1");

            Assert.AreEqual(5, objective.CurrentCount);
        }

        [Test]
        public void EventManager_ScaleEvent_AdjustsDifficulty()
        {
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Easy,
                    "Village Defense", "Defend the village", loc, 100f, 600f));

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");
            manager.JoinEvent(dynamicEvent.EventId, "player1");
            manager.JoinEvent(dynamicEvent.EventId, "player2");

            manager.ScaleEvent(dynamicEvent.EventId);

            // 2-3 participants scale Easy -> Normal
            Assert.AreEqual(EventDifficulty.Normal, dynamicEvent.Difficulty);
        }

        [Test]
        public void EventManager_DistributeRewards_ReturnsCorrectTiers()
        {
            var manager = new EventManager();
            manager.RegisterEventFactory("defense_template", (id, loc) =>
                new DynamicEvent(id, EventType.Defense, EventDifficulty.Normal,
                    "Village Defense", "Defend the village", loc, 100f, 600f));

            var dynamicEvent = manager.CreateEvent("defense_template", "village_01");
            manager.JoinEvent(dynamicEvent.EventId, "player1");
            manager.JoinEvent(dynamicEvent.EventId, "player2");

            var participant1 = manager.GetParticipant(dynamicEvent.EventId, "player1");
            var participant2 = manager.GetParticipant(dynamicEvent.EventId, "player2");
            participant1!.AddContribution(60f); // Contributor
            participant2!.AddContribution(200f); // Hero

            var rewards = manager.DistributeRewards(dynamicEvent.EventId);

            Assert.AreEqual("silver", rewards["player1"]);
            Assert.AreEqual("gold", rewards["player2"]);
        }

        // EventSpawner Tests
        [Test]
        public void EventSpawner_Create_InitializesCorrectly()
        {
            var spawnRule = new EventSpawnRule(60f, 120f, 1, 10, 1.0f);
            var spawner = new EventSpawner("spawner1", "defense_template", "village_01", spawnRule);

            Assert.AreEqual("spawner1", spawner.SpawnerId);
            Assert.AreEqual("defense_template", spawner.EventTemplateId);
            Assert.AreEqual("village_01", spawner.LocationId);
            Assert.IsTrue(spawner.IsActive);
            Assert.AreEqual(0, spawner.ActiveEventCount);
        }

        [Test]
        public void EventSpawner_CanSpawn_ChecksPlayerCount()
        {
            var spawnRule = new EventSpawnRule(60f, 120f, 3, 10, 1.0f);
            var spawner = new EventSpawner("spawner1", "defense_template", "village_01", spawnRule);
            var worldState = new Dictionary<string, object>
            {
                { "PlayerCount_village_01", 2 }
            };

            Assert.IsFalse(spawner.CanSpawn(worldState));

            worldState["PlayerCount_village_01"] = 5;
            Assert.IsTrue(spawner.CanSpawn(worldState));
        }

        [Test]
        public void EventSpawner_CanSpawn_ChecksTimeOfDay()
        {
            var spawnRule = new EventSpawnRule(60f, 120f, 0, 0, 1.0f, requiredTimeOfDay: 12f);
            var spawner = new EventSpawner("spawner1", "defense_template", "village_01", spawnRule);
            var worldState = new Dictionary<string, object>
            {
                { "TimeOfDay", 6f }
            };

            Assert.IsFalse(spawner.CanSpawn(worldState));

            worldState["TimeOfDay"] = 12.5f;
            Assert.IsTrue(spawner.CanSpawn(worldState));
        }

        [Test]
        public void EventSpawner_CanSpawn_ChecksWeather()
        {
            var spawnRule = new EventSpawnRule(60f, 120f, 0, 0, 1.0f, requiredWeather: "Storm");
            var spawner = new EventSpawner("spawner1", "defense_template", "village_01", spawnRule);
            var worldState = new Dictionary<string, object>
            {
                { "Weather", "Clear" }
            };

            Assert.IsFalse(spawner.CanSpawn(worldState));

            worldState["Weather"] = "Storm";
            Assert.IsTrue(spawner.CanSpawn(worldState));
        }

        [Test]
        public void EventSpawner_Update_SpawnsEventWhenReady()
        {
            var spawnRule = new EventSpawnRule(1f, 2f, 0, 0, 1.0f);
            var spawner = new EventSpawner("spawner1", "defense_template", "village_01", spawnRule);
            var worldState = new Dictionary<string, object>();

            // First update shouldn't spawn immediately
            var eventId = spawner.Update(0.5f, worldState);
            Assert.IsNull(eventId);

            // After enough time, should spawn
            eventId = spawner.Update(2f, worldState);
            Assert.IsNotNull(eventId);
            Assert.AreEqual(1, spawner.ActiveEventCount);
        }

        [Test]
        public void EventSpawner_OnEventEnded_DecreasesCount()
        {
            var spawnRule = new EventSpawnRule(1f, 2f, 0, 0, 1.0f);
            var spawner = new EventSpawner("spawner1", "defense_template", "village_01", spawnRule);
            var worldState = new Dictionary<string, object>();

            var eventId = spawner.Update(2f, worldState);
            Assert.AreEqual(1, spawner.ActiveEventCount);

            spawner.OnEventEnded(eventId!);
            Assert.AreEqual(0, spawner.ActiveEventCount);
        }

        // EventChain Tests
        [Test]
        public void EventChain_Create_InitializesCorrectly()
        {
            var chain = new EventChain("chain1", "Village Defense Chain", 3);

            Assert.AreEqual("chain1", chain.ChainId);
            Assert.AreEqual("Village Defense Chain", chain.Name);
            Assert.AreEqual(3, chain.TotalSteps);
            Assert.AreEqual(0, chain.CurrentStep);
            Assert.IsFalse(chain.IsComplete);
            Assert.IsFalse(chain.IsSuccessful);
        }

        [Test]
        public void EventChain_AdvanceChain_ProgressesThroughEvents()
        {
            var chain = new EventChain("chain1", "Village Defense Chain", 3);
            chain.AddChainLink("evt1", "evt2", "evt_fail1", "First wave");
            chain.AddChainLink("evt2", "evt3", "evt_fail2", "Second wave");
            chain.AddChainLink("evt3", "", "", "Final wave");
            chain.SetInitialEvent("evt1");

            Assert.AreEqual("evt1", chain.CurrentEventId);

            var nextEvent = chain.AdvanceChain(true);
            Assert.AreEqual("evt2", nextEvent);
            Assert.AreEqual(1, chain.CurrentStep);

            nextEvent = chain.AdvanceChain(true);
            Assert.AreEqual("evt3", nextEvent);
            Assert.AreEqual(2, chain.CurrentStep);
        }

        [Test]
        public void EventChain_AdvanceChain_BranchesOnFailure()
        {
            var chain = new EventChain("chain1", "Village Defense Chain", 2);
            chain.AddChainLink("evt1", "evt_success", "evt_failure", "First wave");
            chain.SetInitialEvent("evt1");

            var nextEvent = chain.AdvanceChain(false);
            Assert.AreEqual("evt_failure", nextEvent);
        }

        [Test]
        public void EventChain_AdvanceChain_CompletesChain()
        {
            var chain = new EventChain("chain1", "Village Defense Chain", 2);
            chain.AddChainLink("evt1", "evt2", "evt_fail", "First wave");
            chain.AddChainLink("evt2", "", "", "Final wave");
            chain.SetInitialEvent("evt1");

            chain.AdvanceChain(true);
            var nextEvent = chain.AdvanceChain(true);

            Assert.IsNull(nextEvent);
            Assert.IsTrue(chain.IsComplete);
            Assert.IsTrue(chain.IsSuccessful);
        }

        [Test]
        public void EventChain_Reset_ClearsState()
        {
            var chain = new EventChain("chain1", "Village Defense Chain", 2);
            chain.AddChainLink("evt1", "evt2", "evt_fail", "First wave");
            chain.SetInitialEvent("evt1");
            chain.AdvanceChain(true);

            chain.Reset();

            Assert.AreEqual(0, chain.CurrentStep);
            Assert.IsFalse(chain.IsComplete);
            Assert.IsFalse(chain.IsSuccessful);
            Assert.IsNull(chain.CurrentEventId);
        }
    }
}
