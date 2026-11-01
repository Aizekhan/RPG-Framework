using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Quests.Tests
{
    public sealed class QuestStageTests
    {
        private EventBus _bus;
        private QuestRegistry _registry;
        private EntityManager _entities;
        private QuestManager _manager;
        private FakeRequirements _requirements;
        private DateTime _now;
        private int _hero;
        private readonly List<string> _log = new List<string>();

        private sealed class FakeRequirements : IQuestRequirements
        {
            public int Level = 1;
            public readonly HashSet<string> Flags = new HashSet<string>();

            public bool IsMet(int entityId, QuestRequirementData requirement) =>
                requirement.Kind == QuestRequirementKinds.Level ? Level >= requirement.Value
                : requirement.Kind == QuestRequirementKinds.Flag && Flags.Contains(requirement.Id);
        }

        // Into the forest (reach it) -> the guardian (kill the golem; golem_defeated / player_died_3x) -> return or retreat
        private static QuestData LostHammer() => new QuestData
        {
            Id = "hammer",
            Title = "The Lost Hammer",
            Type = "main",
            Objectives = new[]
            {
                new QuestObjectiveData { Id = "reach", Type = ObjectiveType.Explore, TargetId = "forge1", RequiredCount = 1 },
                new QuestObjectiveData { Id = "golem", Type = ObjectiveType.Kill, TargetId = "golem", RequiredCount = 1 },
            },
            Rewards = new[] { new QuestRewardData { Type = RewardType.Experience, RewardId = "xp", Amount = 200 } },
            Stages = new[]
            {
                new QuestStageData { Id = "s1", Title = "Into the forest", ObjectiveIds = new[] { "reach" }, Next = new[] { Link("s2") } },
                new QuestStageData
                {
                    Id = "s2", Title = "The guardian", ObjectiveIds = new[] { "golem" },
                    Next = new[] { Link("s3", "golem_defeated"), Link("s4", "player_died_3x") }
                },
                new QuestStageData { Id = "s3", Title = "Return", Outcome = QuestOutcome.Success },
                new QuestStageData { Id = "s4", Title = "Retreat", Outcome = QuestOutcome.Failure },
            },
        };

        private static QuestStageLinkData Link(string to, string condition = null) => new QuestStageLinkData { To = to, Condition = condition };

        [SetUp]
        public void SetUp()
        {
            _registry = new QuestRegistry();
            _registry.RegisterQuest(new Quest(LostHammer()));
            _entities = new EntityManager();
            _hero = _entities.CreateEntity();
            _bus = new EventBus();
            _requirements = new FakeRequirements();
            _now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
            _manager = new QuestManager(_entities, _registry, _bus, _requirements, () => _now);

            _log.Clear();
            _bus.Subscribe<QuestStageChangedEvent>(e => _log.Add($"stage {e.FromStageId ?? "-"}>{e.ToStageId}{(e.Condition != null ? " on " + e.Condition : "")}"));
            _bus.Subscribe<QuestCompletedEvent>(e => _log.Add($"completed {e.QuestId} rewards {e.Rewards.Count}"));
            _bus.Subscribe<QuestFailedEvent>(e => _log.Add($"failed {e.QuestId} {e.FailureReason}"));
        }

        [Test]
        public void TakingAQuest_PutsItInItsFirstStage()
        {
            Assert.That(_manager.AcceptQuest(_hero, "hammer"), Is.True);

            Assert.That(_manager.CurrentStageOf(_hero, "hammer"), Is.EqualTo("s1"));
            Assert.That(_log, Is.EqualTo(new[] { "stage ->s1" }));
            Assert.That(((IStagedQuest)_registry.GetQuest("hammer")).Type, Is.EqualTo("main"));
        }

        [Test]
        public void OnlyTheCurrentStagesObjectivesCount_AndADoneStageMovesOn()
        {
            _manager.AcceptQuest(_hero, "hammer");

            Assert.That(_manager.ReportProgress(_hero, ObjectiveType.Kill, "golem"), Is.EqualTo(0), "the golem counts only in s2");
            Assert.That(_manager.ReportProgress(_hero, ObjectiveType.Explore, "forge1"), Is.EqualTo(1));

            Assert.That(_manager.CurrentStageOf(_hero, "hammer"), Is.EqualTo("s2"));
            Assert.That(_log.Last(), Is.EqualTo("stage s1>s2"));
            Assert.That(_manager.ReportProgress(_hero, ObjectiveType.Kill, "golem"), Is.EqualTo(1));
            Assert.That(_manager.CurrentStageOf(_hero, "hammer"), Is.EqualTo("s2"), "s2 leaves only on a signal");
        }

        [Test]
        public void ASignalTakesTheLinkThatNamesIt_AndASuccessStageCompletesTheQuest()
        {
            _manager.AcceptQuest(_hero, "hammer");
            Assert.That(_manager.Signal(_hero, "golem_defeated"), Is.EqualTo(0), "not a link of s1");
            _manager.ReportProgress(_hero, ObjectiveType.Explore, "forge1");

            Assert.That(_manager.Signal(_hero, " Golem_Defeated "), Is.EqualTo(1));

            Assert.That(_log.Skip(2), Is.EqualTo(new[] { "stage s2>s3 on Golem_Defeated", "completed hammer rewards 1" }));
            Assert.That(_manager.GetCompletedQuests(_hero), Has.Member("hammer"));
            Assert.That(_manager.CurrentStageOf(_hero, "hammer"), Is.Null);
        }

        [Test]
        public void AFailureStageFailsTheQuest()
        {
            _manager.AcceptQuest(_hero, "hammer");
            _manager.ReportProgress(_hero, ObjectiveType.Explore, "forge1");

            _manager.Signal(_hero, "player_died_3x");

            Assert.That(_log.Last(), Is.EqualTo("failed hammer player_died_3x"));
            Assert.That(_manager.GetActiveQuests(_hero), Is.Empty);
            Assert.That(_manager.GetCompletedQuests(_hero), Has.No.Member("hammer"));
        }

        [Test]
        public void TheGameMovesAStageAlongItsLinks_OrAnywhereWithForce()
        {
            _manager.AcceptQuest(_hero, "hammer");

            Assert.That(_manager.AdvanceStage(_hero, "hammer", "s3"), Is.False, "s1 has no link to s3");
            Assert.That(_manager.CompleteQuest(_hero, "hammer"), Is.False, "a staged quest completes in a success stage");
            Assert.That(_manager.AdvanceStage(_hero, "hammer", "s2"), Is.True);
            Assert.That(_manager.AdvanceStage(_hero, "hammer", "nowhere", force: true), Is.False);
            Assert.That(_manager.AdvanceStage(_hero, "hammer", "s3", force: true), Is.True);
            Assert.That(_manager.GetCompletedQuests(_hero), Has.Member("hammer"));
        }

        [Test]
        public void RestoringAStage_FiresNothing()
        {
            _manager.AcceptQuest(_hero, "hammer");
            _log.Clear();

            Assert.That(_manager.RestoreStage(_hero, "hammer", "s3"), Is.True);

            Assert.That(_log, Is.Empty);
            Assert.That(_manager.CurrentStageOf(_hero, "hammer"), Is.EqualTo("s3"));
            Assert.That(_manager.CompleteQuest(_hero, "hammer"), Is.True, "a loaded quest in its success stage hands in");
        }

        [Test]
        public void RequirementsAndTheMinimumLevel_AreAskedOfTheGame()
        {
            var data = LostHammer();
            data.Id = "deep";
            data.MinimumLevel = 5;
            data.Requirements = new[] { new QuestRequirementData { Kind = QuestRequirementKinds.Flag, Id = "met_lyra", Value = 1 } };
            _registry.RegisterQuest(new Quest(data));

            Assert.That(_manager.WhyNot(_hero, "deep"), Is.EqualTo(QuestRefusal.LevelTooLow));
            _requirements.Level = 5;
            Assert.That(_manager.WhyNot(_hero, "deep"), Is.EqualTo(QuestRefusal.RequirementNotMet));
            _requirements.Flags.Add("met_lyra");
            Assert.That(_manager.WhyNot(_hero, "deep"), Is.EqualTo(QuestRefusal.None));
            Assert.That(new QuestManager(_entities, _registry).WhyNot(_hero, "deep"), Is.EqualTo(QuestRefusal.RequirementNotMet),
                "without the game's answers a requirement is not met");
            Assert.That(_manager.WhyNot(_hero, "nothing"), Is.EqualTo(QuestRefusal.UnknownQuest));
        }

        [Test]
        public void ARepeatableQuest_WaitsForItsCooldown()
        {
            var data = LostHammer();
            data.Id = "daily";
            data.IsRepeatable = true;
            data.RepeatCooldownSeconds = 86400;
            _registry.RegisterQuest(new Quest(data));
            _manager.AcceptQuest(_hero, "daily");
            _manager.AdvanceStage(_hero, "daily", "s3", force: true);

            _now = _now.AddHours(23);
            Assert.That(_manager.WhyNot(_hero, "daily"), Is.EqualTo(QuestRefusal.CoolingDown));
            Assert.That(_manager.CooldownLeft(_hero, "daily"), Is.EqualTo(TimeSpan.FromHours(1)));
            _now = _now.AddHours(1);
            Assert.That(_manager.WhyNot(_hero, "daily"), Is.EqualTo(QuestRefusal.None));

            _manager.RestoreCompletedAt(_hero, "daily", _now);
            Assert.That(_manager.WhyNot(_hero, "daily"), Is.EqualTo(QuestRefusal.CoolingDown), "a loaded completion time counts");
        }

        [Test]
        public void AStageWithoutObjectives_PassesOnAtOnce()
        {
            var data = LostHammer();
            data.Id = "talk";
            data.Stages = new[]
            {
                new QuestStageData { Id = "intro", Next = new[] { Link("end") } },
                new QuestStageData { Id = "end", Outcome = QuestOutcome.Success },
            };
            data.Objectives = new QuestObjectiveData[0];
            _registry.RegisterQuest(new Quest(data));

            _manager.AcceptQuest(_hero, "talk");

            Assert.That(_manager.GetCompletedQuests(_hero), Has.Member("talk"));
        }

        [Test]
        public void TheGraphCheck_PassesAGoodQuest_AndNamesEveryProblem()
        {
            Assert.That(QuestGraph.Check(new Quest(LostHammer())), Is.Empty);

            var data = LostHammer();
            data.Title = "";
            data.Stages = new[]
            {
                new QuestStageData { Id = "a", ObjectiveIds = new[] { "ghost" }, Next = new[] { Link("b"), Link("x") } },
                new QuestStageData { Id = "b", Next = new[] { Link("c") } },
                new QuestStageData { Id = "c", Next = new[] { Link("b") } },
                new QuestStageData { Id = "lost", Outcome = QuestOutcome.Success },
                new QuestStageData { Id = "stuck" },
            };
            var codes = QuestGraph.Check(new Quest(data)).Select(i => i.Code).ToList();

            Assert.That(codes, Is.EquivalentTo(new[]
            {
                "noTitle", "unknownObjective", "badLink", "deadEnd", "unreachable", "unreachable", "noEnd", "noWayOut",
                "noWayOut", "noWayOut"
            }));
            data.StartStageId = "none";
            Assert.That(QuestGraph.Check(new Quest(data)).Select(i => i.Code), Has.Member("badStart"));
        }

        [Test]
        public void TheGraphCheck_FindsMissingAndCircularPrerequisites()
        {
            var a = new QuestData { Id = "a", Title = "A", PrerequisiteQuestIds = new[] { "b" } };
            var b = new QuestData { Id = "b", Title = "B", PrerequisiteQuestIds = new[] { "a", "ghost" } };

            var issues = QuestGraph.Check(new IQuest[] { new Quest(a), new Quest(b) });

            Assert.That(issues.Select(i => i.Code), Is.EquivalentTo(new[] { "missingPrerequisite", "prerequisiteCycle" }));
            Assert.That(issues.Single(i => i.Code == "prerequisiteCycle").Message, Does.Contain("a -> b -> a"));
        }
    }
}
