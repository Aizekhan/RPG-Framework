using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Dialogue;
using RPGFramework.Events;
using RPGFramework.Factions;
using RPGFramework.Logging;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Reputation in the kit: killing a bandit pleases the guards and angers the bandits, a quest's "rep:" reward
    /// raises it, a dialogue changes it and reads it, and it is saved.
    /// </summary>
    public sealed class ActorReputationTests
    {
        private const string Guards = "guards", Bandits = "bandits", Bounty = "bounty", Captain = "captain";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private ActionRpgModule _module;

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 500f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7
            };
            _settings.Enemies.Add(new CharacterData { Id = "bandit", DisplayName = "Bandit", MaxHealth = 10f, Faction = Bandits, ReputationOnKill = -20 });
            _settings.StartingQuests.Add(Bounty);

            _content = new ActionRpgContent();
            _content.Factions.Add(new Faction(Guards, "City Guard", "", FactionType.Military));
            _content.Factions.Add(new Faction(Bandits, "Bandits", "", FactionType.Criminal, defaultReputation: -30));
            _content.FactionRelations.Add(new FactionRelationship(Guards, Bandits, FactionRelationType.Enemy, -0.5f));
            _content.Quests.Add(new Quest(Bounty, "Bounty", "", new List<IQuestObjective>
                { new QuestObjective("slay", ObjectiveType.Kill, "bandit", 1, "") },
                new List<IQuestReward> { new QuestReward(RewardType.Custom, ActorReputation.Prefix + Guards, 30) }, new List<string>(), 1, false));

            var nodes = new List<IDialogueNode>
            {
                new DialogueNode("hail", DialogueNodeType.Choice, Captain, "Citizen.", new string[0], new IDialogueChoice[]
                {
                    new DialogueChoice("bribe", "A gift.", "bye"),
                    new DialogueChoice("honor", "At your service.", "bye", new DialogueCondition(ConditionType.StatCheck, "rep:" + Guards, 50))
                }),
                new DialogueNode("bye", DialogueNodeType.End, Captain, "Move along.", new string[0], new IDialogueChoice[0])
            };
            _content.Dialogues.Add(new RPGFramework.Dialogue.Dialogue(Captain, "The Captain", "", "hail", nodes));
            _content.DialogueEffects.Add(DialogueEffect.OnChoice(Captain, "bribe", DialogueEffectKind.ChangeReputation, Guards, 5));
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame() =>
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));

        [Test]
        public void Kills_Quests_AndDialogue_MoveReputation_WithItsRelations()
        {
            StartGame();
            int hero = World.PlayerId;
            var changes = new List<ReputationChangedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ReputationChangedEvent>(changes.Add);
            Assert.That(World.Reputation.Of(hero, Bandits), Is.EqualTo(-30), "its default");
            Assert.That(World.Reputation.StandingOf(hero, Guards), Is.EqualTo(FactionStanding.Neutral));

            int bandit = World.SpawnEnemy("bandit");
            World.DealDamage(hero, bandit, 1000f);
            Assert.That(World.Get(bandit), Is.Null, "slain");

            Assert.That(World.Reputation.Of(hero, Bandits), Is.EqualTo(-50), "the kill");
            Assert.That(World.Reputation.Of(hero, Guards), Is.EqualTo(40), "10 from the bandits' enemy, 30 from the bounty");
            Assert.That(changes.Select(c => c.FactionId), Does.Contain(Bandits).And.Contain(Guards));

            Assert.That(World.Dialogue.Start(hero, Captain), Is.True);
            Assert.That(World.Dialogue.Choices(hero).Select(c => c.Id), Is.EqualTo(new[] { "bribe" }));
            World.Dialogue.Advance(hero, "bribe");
            Assert.That(World.Reputation.Of(hero, Guards), Is.EqualTo(45));
            Assert.That(World.Reputation.Change(hero, "nobody", 5), Is.False);
        }

        [Test]
        public void Reputation_IsSaved()
        {
            StartGame();
            int hero = World.PlayerId;
            World.Reputation.Change(hero, Guards, 60);
            var progress = World.CaptureProgress();
            Assert.That(progress.Reputations.Single(r => r.FactionId == Guards).Value, Is.EqualTo(60));

            World.Reputation.Set(hero, Guards, -80);
            World.RestoreProgress(progress);

            Assert.That(World.Reputation.Of(hero, Guards), Is.EqualTo(60));
            Assert.That(World.Reputation.AtLeast(hero, Guards, FactionStanding.Friendly), Is.True);
        }
    }
}
