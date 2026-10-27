using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Dialogue.Tests
{
    /// <summary>
    /// The manager follows what the designer authored: conditions on nodes and choices, Branch nodes,
    /// and the dialogue events. Uses the real EntityManager and EventBus.
    /// </summary>
    [TestFixture]
    public class DialogueFlowTests
    {
        private EntityManager _entities;
        private DialogueRegistry _registry;
        private EventBus _bus;
        private FakeContext _context;
        private DialogueManager _manager;
        private int _player;

        private List<DialogueStartedEvent> _started;
        private List<LineSpokenEvent> _lines;
        private List<ChoiceSelectedEvent> _choices;
        private List<DialogueEndedEvent> _ended;
        private List<string> _order;

        /// <summary>
        /// A context the tests fill in by hand.
        /// </summary>
        private class FakeContext : IDialogueContext
        {
            public int EntityId { get; set; }
            public string CurrentNodeId { get; set; }
            public Dictionary<string, int> Variables { get; } = new Dictionary<string, int>();
            public HashSet<string> Quests = new HashSet<string>();
            public HashSet<string> Items = new HashSet<string>();
            public Dictionary<string, int> Stats = new Dictionary<string, int>();

            public bool HasCompletedQuest(string questId) => Quests.Contains(questId);
            public bool HasItem(string itemId) => Items.Contains(itemId);
            public int GetStatValue(string statId) => Stats.TryGetValue(statId, out var v) ? v : 0;
        }

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _registry = new DialogueRegistry();
            _bus = new EventBus();
            _context = new FakeContext();
            _player = _entities.CreateEntity();

            _started = new List<DialogueStartedEvent>();
            _lines = new List<LineSpokenEvent>();
            _choices = new List<ChoiceSelectedEvent>();
            _ended = new List<DialogueEndedEvent>();
            _order = new List<string>();
            _bus.Subscribe<DialogueStartedEvent>(e => { _started.Add(e); _order.Add("started"); });
            _bus.Subscribe<LineSpokenEvent>(e => { _lines.Add(e); _order.Add("line:" + e.NodeId); });
            _bus.Subscribe<ChoiceSelectedEvent>(e => { _choices.Add(e); _order.Add("choice:" + e.ChoiceId); });
            _bus.Subscribe<DialogueEndedEvent>(e => { _ended.Add(e); _order.Add("ended:" + e.EndState); });

            _manager = new DialogueManager(_entities, _registry, _bus, id => _context);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Dispose();
        }

        // ---- Helpers ----

        private static IDialogueNode Line(string id, params string[] next) =>
            new DialogueNode(id, DialogueNodeType.Line, "npc", "Text of " + id, next.ToList(), null);

        private static IDialogueNode Line(string id, IDialogueCondition condition, params string[] next) =>
            new DialogueNode(id, DialogueNodeType.Line, "npc", "Text of " + id, next.ToList(), null, condition);

        private static IDialogueNode Branch(string id, IDialogueCondition condition, params string[] next) =>
            new DialogueNode(id, DialogueNodeType.Branch, "", "", next.ToList(), null, condition);

        private static IDialogueNode Choice(string id, params IDialogueChoice[] choices) =>
            new DialogueNode(id, DialogueNodeType.Choice, "player", "Text of " + id, new List<string>(), choices.ToList());

        private static IDialogueNode End(string id) =>
            new DialogueNode(id, DialogueNodeType.End, "narrator", "Text of " + id, new List<string>(), null);

        private static IDialogueCondition Quest(string questId) =>
            new DialogueCondition(ConditionType.QuestComplete, questId, 1);

        private static IDialogueCondition Level(int min) =>
            new DialogueCondition(ConditionType.StatCheck, "Level", min);

        private void Register(string rootId, params IDialogueNode[] nodes)
        {
            _registry.RegisterDialogue(new Dialogue("talk", "Talk", "", rootId, nodes.ToList()));
        }

        private string Current => _manager.GetCurrentNode(_player)?.Id;

        // ---- Branch nodes ----

        [Test]
        public void BranchNode_GoesToTheFirstNextNodeWhoseConditionPasses()
        {
            Register("greet",
                Line("greet", "branch"),
                Branch("branch", null, "hero", "veteran", "stranger"),
                Line("hero", Quest("dragon"), "bye"),
                Line("veteran", Level(10), "bye"),
                Line("stranger", "bye"),
                End("bye"));
            _context.Stats["Level"] = 12;

            _manager.StartDialogue(_player, "talk");

            Assert.That(_manager.AdvanceDialogue(_player), Is.True);
            Assert.That(Current, Is.EqualTo("veteran"), "the branch is passed through at once, to the first way that is open");
        }

        [Test]
        public void BranchNode_FallsThroughToTheNodeWithoutACondition()
        {
            Register("greet",
                Line("greet", "branch"),
                Branch("branch", null, "hero", "stranger"),
                Line("hero", Quest("dragon"), "bye"),
                Line("stranger", "bye"),
                End("bye"));

            _manager.StartDialogue(_player, "talk");
            _manager.AdvanceDialogue(_player);

            Assert.That(Current, Is.EqualTo("stranger"));
        }

        [Test]
        public void BranchNode_WithItsOwnCondition_TakesTheFirstWayWhenItPasses()
        {
            Register("branch",
                Branch("branch", Level(10), "high", "low"),
                Line("high", "bye"),
                Line("low", "bye"),
                End("bye"));
            _context.Stats["Level"] = 10;

            Assert.That(_manager.StartDialogue(_player, "talk"), Is.True);
            Assert.That(Current, Is.EqualTo("high"), "a Branch at the start is resolved when the dialogue starts");
        }

        [Test]
        public void BranchNode_WithItsOwnCondition_SkipsTheFirstWayWhenItFails()
        {
            Register("branch",
                Branch("branch", Level(10), "high", "low"),
                Line("high", "bye"),
                Line("low", "bye"),
                End("bye"));
            _context.Stats["Level"] = 3;

            _manager.StartDialogue(_player, "talk");

            Assert.That(Current, Is.EqualTo("low"));
        }

        [Test]
        public void BranchNode_WithNoOpenWay_DoesNotAdvance_AndNothingChanges()
        {
            Register("greet",
                Line("greet", "branch"),
                Branch("branch", null, "hero"),
                Line("hero", Quest("dragon"), "bye"),
                End("bye"));

            _manager.StartDialogue(_player, "talk");

            Assert.That(_manager.AdvanceDialogue(_player), Is.False);
            Assert.That(Current, Is.EqualTo("greet"));
            Assert.That(_manager.GetDialogueProgress(_player).VisitedNodeIds, Is.EqualTo(new[] { "greet" }));
        }

        [Test]
        public void BranchNodes_ThatLoopForever_DoNotHang()
        {
            Register("greet",
                Line("greet", "a"),
                Branch("a", null, "b"),
                Branch("b", null, "a"),
                End("bye"));

            _manager.StartDialogue(_player, "talk");

            Assert.That(_manager.AdvanceDialogue(_player), Is.False);
            Assert.That(Current, Is.EqualTo("greet"));
        }

        [Test]
        public void BranchNodes_AreRecordedAsVisited_ButSpeakNoLine()
        {
            Register("greet",
                Line("greet", "branch"),
                Branch("branch", null, "stranger"),
                Line("stranger", "bye"),
                End("bye"));

            _manager.StartDialogue(_player, "talk");
            _manager.AdvanceDialogue(_player);

            Assert.That(_manager.GetDialogueProgress(_player).VisitedNodeIds, Is.EqualTo(new[] { "greet", "branch", "stranger" }));
            Assert.That(_lines.Select(l => l.NodeId), Is.EqualTo(new[] { "greet", "stranger" }));
        }

        // ---- Line nodes ----

        [Test]
        public void LineNode_SkipsANextNodeWhoseConditionFails()
        {
            Register("greet",
                Line("greet", "secret", "plain"),
                Line("secret", Quest("dragon"), "bye"),
                Line("plain", "bye"),
                End("bye"));

            _manager.StartDialogue(_player, "talk");
            _manager.AdvanceDialogue(_player);

            Assert.That(Current, Is.EqualTo("plain"));
        }

        [Test]
        public void StartNode_WhoseConditionFails_DoesNotStart()
        {
            Register("greet",
                Line("greet", Quest("dragon"), "bye"),
                End("bye"));

            Assert.That(_manager.StartDialogue(_player, "talk"), Is.False);
            Assert.That(_manager.GetDialogueProgress(_player), Is.Null);
            Assert.That(_started, Is.Empty);
        }

        // ---- Choices ----

        [Test]
        public void Choice_WhoseConditionFails_IsNotOffered()
        {
            Register("ask",
                Choice("ask",
                    new DialogueChoice("brag", "I slew the dragon", "bye", Quest("dragon")),
                    new DialogueChoice("leave", "Bye", "bye")),
                End("bye"));

            _manager.StartDialogue(_player, "talk");
            Assert.That(_manager.GetAvailableChoices(_player).Select(c => c.Id), Is.EqualTo(new[] { "leave" }));

            _context.Quests.Add("dragon");
            Assert.That(_manager.GetAvailableChoices(_player).Select(c => c.Id), Is.EqualTo(new[] { "brag", "leave" }));
        }

        [Test]
        public void Choice_WhoseConditionFails_CannotBeSelected()
        {
            Register("ask",
                Choice("ask",
                    new DialogueChoice("brag", "I slew the dragon", "bye", Quest("dragon")),
                    new DialogueChoice("leave", "Bye", "bye")),
                End("bye"));

            _manager.StartDialogue(_player, "talk");

            Assert.That(_manager.AdvanceDialogue(_player, "brag"), Is.False);
            Assert.That(Current, Is.EqualTo("ask"));
            Assert.That(_choices, Is.Empty);
        }

        [Test]
        public void DisabledChoice_CannotBeSelected()
        {
            Register("ask",
                Choice("ask",
                    new DialogueChoice("locked", "Locked", "bye", null, false),
                    new DialogueChoice("leave", "Bye", "bye")),
                End("bye"));

            _manager.StartDialogue(_player, "talk");

            Assert.That(_manager.AdvanceDialogue(_player, "locked"), Is.False);
            Assert.That(Current, Is.EqualTo("ask"));
        }

        [Test]
        public void Choice_LeadingToABranch_PassesThroughIt()
        {
            Register("ask",
                Choice("ask", new DialogueChoice("go", "Go on", "branch")),
                Branch("branch", Level(5), "strong", "weak"),
                Line("strong", "bye"),
                Line("weak", "bye"),
                End("bye"));
            _context.Stats["Level"] = 1;

            _manager.StartDialogue(_player, "talk");

            Assert.That(_manager.AdvanceDialogue(_player, "go"), Is.True);
            Assert.That(Current, Is.EqualTo("weak"));
            Assert.That(_choices.Single().TargetNodeId, Is.EqualTo("branch"));
        }

        [Test]
        public void WithoutAContext_ConditionsDoNotPass()
        {
            var manager = new DialogueManager(_entities, _registry);
            Register("ask",
                Choice("ask",
                    new DialogueChoice("brag", "I slew the dragon", "bye", Quest("dragon")),
                    new DialogueChoice("leave", "Bye", "bye")),
                End("bye"));

            manager.StartDialogue(_player, "talk");

            Assert.That(manager.GetAvailableChoices(_player).Select(c => c.Id), Is.EqualTo(new[] { "leave" }));
        }

        [Test]
        public void TheContextProvider_IsAskedForTheRightEntity()
        {
            var asked = new List<int>();
            var manager = new DialogueManager(_entities, _registry, null, id => { asked.Add(id); return _context; });
            Register("ask",
                Choice("ask", new DialogueChoice("brag", "I slew the dragon", "bye", Quest("dragon"))),
                End("bye"));

            manager.StartDialogue(_player, "talk");
            manager.GetAvailableChoices(_player);

            Assert.That(asked, Is.Not.Empty);
            Assert.That(asked, Is.All.EqualTo(_player));
        }

        // ---- Events ----

        [Test]
        public void Start_PublishesDialogueStarted_ThenTheFirstLine()
        {
            Register("greet", Line("greet", "bye"), End("bye"));

            _manager.StartDialogue(_player, "talk");

            Assert.That(_order, Is.EqualTo(new[] { "started", "line:greet" }));
            Assert.That(_started.Single().EntityId, Is.EqualTo(_player));
            Assert.That(_started.Single().DialogueId, Is.EqualTo("talk"));
            Assert.That(_lines.Single().SpeakerId, Is.EqualTo("npc"));
            Assert.That(_lines.Single().Text, Is.EqualTo("Text of greet"));
        }

        [Test]
        public void Advancing_PublishesTheLineOfTheNodeArrivedAt()
        {
            Register("greet", Line("greet", "next"), Line("next", "bye"), End("bye"));

            _manager.StartDialogue(_player, "talk");
            _manager.AdvanceDialogue(_player);

            var line = _lines.Last();
            Assert.That(line.NodeId, Is.EqualTo("next"));
            Assert.That(line.Text, Is.EqualTo("Text of next"), "the text belongs to the node in NodeId");
            Assert.That(line.DialogueId, Is.EqualTo("talk"));
        }

        [Test]
        public void Choosing_PublishesChoiceSelected_ThenTheNextLine()
        {
            Register("ask",
                Choice("ask", new DialogueChoice("yes", "Yes please", "thanks")),
                Line("thanks", "bye"),
                End("bye"));

            _manager.StartDialogue(_player, "talk");
            _order.Clear();
            _manager.AdvanceDialogue(_player, "yes");

            Assert.That(_order, Is.EqualTo(new[] { "choice:yes", "line:thanks" }));
            var choice = _choices.Single();
            Assert.That(choice.NodeId, Is.EqualTo("ask"));
            Assert.That(choice.ChoiceText, Is.EqualTo("Yes please"));
            Assert.That(choice.TargetNodeId, Is.EqualTo("thanks"));
            Assert.That(choice.EntityId, Is.EqualTo(_player));
        }

        [Test]
        public void ReachingTheEnd_PublishesDialogueEnded_Completed()
        {
            Register("greet", Line("greet", "bye"), End("bye"));

            _manager.StartDialogue(_player, "talk");
            _order.Clear();
            _manager.AdvanceDialogue(_player);

            Assert.That(_order, Is.EqualTo(new[] { "line:bye", "ended:Completed" }));
            Assert.That(_ended.Single().DialogueId, Is.EqualTo("talk"));
            Assert.That(_manager.GetDialogueProgress(_player), Is.Null);
        }

        [Test]
        public void EndDialogue_PublishesDialogueEnded_Interrupted()
        {
            Register("greet", Line("greet", "bye"), End("bye"));

            _manager.StartDialogue(_player, "talk");
            _manager.EndDialogue(_player);

            Assert.That(_ended.Single().EndState, Is.EqualTo(DialogueState.Interrupted));
            Assert.That(_ended.Single().EntityId, Is.EqualTo(_player));
            Assert.That(_manager.GetDialogueProgress(_player), Is.Null);
        }

        [Test]
        public void AFailedAdvance_PublishesNothing()
        {
            Register("ask", Choice("ask", new DialogueChoice("yes", "Yes", "bye")), End("bye"));

            _manager.StartDialogue(_player, "talk");
            _order.Clear();

            Assert.That(_manager.AdvanceDialogue(_player, "no_such_choice"), Is.False);
            Assert.That(_order, Is.Empty);
        }

        [Test]
        public void WithoutAPublisher_TheDialogueStillPlays()
        {
            var manager = new DialogueManager(_entities, _registry);
            Register("greet", Line("greet", "bye"), End("bye"));

            Assert.That(manager.StartDialogue(_player, "talk"), Is.True);
            Assert.That(manager.AdvanceDialogue(_player), Is.True);
            Assert.That(manager.GetDialogueProgress(_player), Is.Null);
            Assert.That(_order, Is.Empty);
        }
    }
}
