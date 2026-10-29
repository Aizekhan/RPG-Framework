using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Dialogue;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The kit's conversations: conditions see the player's gold and variables, effects give items, take gold,
    /// hand out quests and set variables, talking counts for Interact objectives, and the variables are saved.
    /// </summary>
    public sealed class ActorDialogueTests
    {
        private const string Elder = "elder", Hunt = "hunt", Talk = "talk", Medal = "medal";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private ActionRpgModule _module;

        private ActorWorld World => _module.World;
        private ActorDialogue Talks => World.Dialogue;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7
            };
            _settings.Enemies.Add(new CharacterData { Id = "goblin", DisplayName = "Goblin" });
            _settings.StartingQuests.Add(Talk);

            _content = new ActionRpgContent();
            _content.Items.Add(new Item(Medal, "Medal", "", ItemType.Quest));
            _content.Quests.Add(new Quest(Hunt, "The Hunt", "", new List<IQuestObjective>
                { new QuestObjective("slay", ObjectiveType.Kill, "goblin", 1, "") }, new List<IQuestReward>(), new List<string>(), 1, false));
            _content.Quests.Add(new Quest(Talk, "Talk to the Elder", "", new List<IQuestObjective>
                { new QuestObjective("talk", ObjectiveType.Interact, Elder, 1, "") }, new List<IQuestReward>(), new List<string>(), 1, false));

            var nodes = new List<IDialogueNode>
            {
                new DialogueNode("greet", DialogueNodeType.Line, Elder, "Welcome.", new[] { "ask" }, new IDialogueChoice[0]),
                new DialogueNode("ask", DialogueNodeType.Choice, Elder, "Will you help?", new string[0], new IDialogueChoice[]
                {
                    new DialogueChoice("take", "I will.", "given"),
                    new DialogueChoice("rich", "Here, 50 gold.", "bye", new DialogueCondition(ConditionType.StatCheck, "Gold", 50)),
                    new DialogueChoice("again", "Hello again.", "bye", new DialogueCondition(ConditionType.VariableCheck, "met", 1))
                }),
                new DialogueNode("given", DialogueNodeType.Line, Elder, "Take this.", new[] { "bye" }, new IDialogueChoice[0]),
                new DialogueNode("bye", DialogueNodeType.End, Elder, "Farewell.", new string[0], new IDialogueChoice[0])
            };
            _content.Dialogues.Add(new RPGFramework.Dialogue.Dialogue(Elder, "The Elder", "", "greet", nodes));
            _content.DialogueEffects.Add(DialogueEffect.OnChoice(Elder, "take", DialogueEffectKind.AcceptQuest, Hunt));
            _content.DialogueEffects.Add(DialogueEffect.OnChoice(Elder, "rich", DialogueEffectKind.TakeCurrency, "", 50));
            _content.DialogueEffects.Add(DialogueEffect.OnNode(Elder, "given", DialogueEffectKind.GiveItem, Medal));
            _content.DialogueEffects.Add(DialogueEffect.OnNode(Elder, "given", DialogueEffectKind.SetVariable, "met", 1));
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
        public void ATalk_FollowsTheConditions_AndItsEffectsHappen()
        {
            StartGame();
            int hero = World.PlayerId;

            Assert.That(Talks.Start(hero, Elder), Is.True);
            Assert.That(World.CompletedQuests, Does.Contain(Talk), "talking is the Interact objective");
            Assert.That(Talks.Current(hero).Id, Is.EqualTo("greet"));
            Assert.That(Talks.Advance(hero), Is.True);
            Assert.That(Talks.Choices(hero).Select(c => c.Id), Is.EqualTo(new[] { "take" }), "no gold, never met");

            Assert.That(Talks.Advance(hero, "take"), Is.True);
            Assert.That(World.ActiveQuests.Select(q => q.Quest.Id), Does.Contain(Hunt));
            Assert.That(World.InventoryOf(hero).GetAllItems().Count(i => i?.ItemId == Medal), Is.EqualTo(1));
            Assert.That(Talks.VariableOf(hero, "met"), Is.EqualTo(1));
            Talks.Advance(hero);
            Assert.That(Talks.IsTalking(hero), Is.False);

            World.GiveCurrency(hero, 60);
            Assert.That(Talks.Start(hero, Elder), Is.True);
            Talks.Advance(hero);
            Assert.That(Talks.Choices(hero).Select(c => c.Id), Is.EquivalentTo(new[] { "take", "rich", "again" }));
            Assert.That(Talks.Advance(hero, "rich"), Is.True);
            Assert.That(World.CurrencyOf(hero), Is.EqualTo(10));
        }

        [Test]
        public void Variables_AreSaved()
        {
            StartGame();
            int hero = World.PlayerId;
            Talks.SetVariable(hero, "met", 1);
            Talks.SetVariable(hero, "trust", 3);
            var progress = World.CaptureProgress();
            Assert.That(progress.DialogueVariables.Select(v => v.Name), Is.EqualTo(new[] { "met", "trust" }));

            Talks.SetVariable(hero, "trust", 0);
            World.RestoreProgress(progress);

            Assert.That(Talks.VariableOf(hero, "trust"), Is.EqualTo(3));
            Assert.That(World.TakeItem(hero, Medal), Is.False, "none to take");
        }
    }
}
