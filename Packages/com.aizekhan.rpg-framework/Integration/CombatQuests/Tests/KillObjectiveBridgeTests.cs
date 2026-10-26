using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Combat;
using RPGFramework.ECS;
using RPGFramework.Quests;

namespace RPGFramework.Integration.CombatQuests.Tests
{
    public sealed class KillObjectiveBridgeTests
    {
        private const int Wolf = 100;
        private const int Bear = 101;
        private const int Pet = 200;

        private sealed class Mortal : ILifeStateHandler
        {
            public LifeState CurrentState => LifeState.Alive;
            public bool CanDie => true;
            public bool CanResurrect => true;
            public void OnDeath(int killerId, string reason) { }
            public void OnResurrect(int resurrectorId, string resurrectType) { }
            public void OnLifeStateChanged(LifeState oldState, LifeState newState) { }
            public bool IsAlive() => true;
            public bool IsDead() => false;
            public bool IsInvulnerable() => false;
        }

        private LifeStateManager _lifeStates;
        private QuestManager _quests;
        private int _hero;

        [SetUp]
        public void SetUp()
        {
            var registry = new QuestRegistry();
            registry.RegisterQuest(new Quest("wolves", "Wolves", "",
                new List<IQuestObjective> { new QuestObjective("kill_wolves", ObjectiveType.Kill, "wolf", 2, "", false) },
                new List<IQuestReward>(), new List<string>(), 1, false));
            registry.RegisterQuest(new Quest("slayer", "Slayer", "",
                new List<IQuestObjective> { new QuestObjective("kill_any", ObjectiveType.Kill, null, 3, "", false) },
                new List<IQuestReward>(), new List<string>(), 1, false));

            var entities = new EntityManager();
            _hero = entities.CreateEntity();
            _quests = new QuestManager(entities, registry);
            _quests.AcceptQuest(_hero, "wolves");
            _quests.AcceptQuest(_hero, "slayer");

            _lifeStates = new LifeStateManager();
            _lifeStates.RegisterEntity(Wolf, new Mortal());
            _lifeStates.RegisterEntity(Bear, new Mortal());
        }

        private int Progress(string questId, string objectiveId) =>
            _quests.GetQuestProgress(_hero, questId).ObjectiveProgress[objectiveId];

        [Test]
        public void Kill_AdvancesMatchingAndUntargetedObjectives()
        {
            using var bridge = new KillObjectiveBridge(_lifeStates, _quests, id => id == Wolf ? "wolf" : "bear");

            _lifeStates.Kill(Wolf, _hero, "combat");
            _lifeStates.Kill(Bear, _hero, "combat");

            Assert.That(Progress("wolves", "kill_wolves"), Is.EqualTo(1));
            Assert.That(Progress("slayer", "kill_any"), Is.EqualTo(2));
            Assert.That(bridge.ObjectivesAdvanced, Is.EqualTo(3));
        }

        [Test]
        public void Credit_CanBeRedirected_AndEnvironmentalDeathsAreIgnored()
        {
            using var bridge = new KillObjectiveBridge(_lifeStates, _quests, _ => "wolf",
                killer => killer == Pet ? _hero : -1);

            _lifeStates.Kill(Wolf, Pet, "combat");
            _lifeStates.Kill(Bear, -1, "environmental");

            Assert.That(Progress("wolves", "kill_wolves"), Is.EqualTo(1));
            Assert.That(bridge.ObjectivesAdvanced, Is.EqualTo(2));
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var bridge = new KillObjectiveBridge(_lifeStates, _quests, _ => "wolf");
            bridge.Dispose();

            _lifeStates.Kill(Wolf, _hero, "combat");

            Assert.That(Progress("wolves", "kill_wolves"), Is.EqualTo(0));
        }
    }
}
