using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Dialogue;
using RPGFramework.ECS;
using RPGFramework.Tools.DialogueEditor;
using UnityEngine;

namespace RPGFramework.Tools.DialogueEditor.Tests
{
    /// <summary>
    /// A conversation written as a dialogue asset plays in the real Dialogue module (registry, manager, entity manager)
    /// exactly as written, and each check catches a mistake that would stop it: a missing or duplicate id, a choice
    /// leading nowhere, a dead end, a node no path reaches, a conversation that never ends.
    /// </summary>
    public sealed class DialogueAssetTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _made)
                Object.DestroyImmediate(asset);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _made.Add(asset);
            return asset;
        }

        private static DialogueNodeEntry Node(string id, DialogueNodeType type, params string[] next)
        {
            return new DialogueNodeEntry
            {
                nodeId = id,
                nodeType = type,
                speakerId = "npc_blacksmith",
                text = "Text of " + id,
                nextNodeIds = next.ToList()
            };
        }

        private static DialogueChoiceEntry Choice(string id, string target)
        {
            return new DialogueChoiceEntry { choiceId = id, text = "Say " + id, targetNodeId = target };
        }

        /// <summary>
        /// greet -> offer (accept -> thanks -> bye | decline -> bye), bye is the end.
        /// </summary>
        private DialogueAsset GoodDialogue(string id = "dialogue_blacksmith")
        {
            var offer = Node("offer", DialogueNodeType.Choice);
            offer.choices = new List<DialogueChoiceEntry> { Choice("accept", "thanks"), Choice("decline", "bye") };

            var dialogue = Make<DialogueAsset>();
            dialogue.dialogueId = id;
            dialogue.title = "Blacksmith";
            dialogue.startNodeId = "greet";
            dialogue.nodes = new List<DialogueNodeEntry>
            {
                Node("greet", DialogueNodeType.Line, "offer"),
                offer,
                Node("thanks", DialogueNodeType.Line, "bye"),
                Node("bye", DialogueNodeType.End)
            };
            return dialogue;
        }

        private static DialogueNodeEntry Find(DialogueAsset dialogue, string nodeId)
        {
            return dialogue.nodes.First(n => n.nodeId == nodeId);
        }

        private static void AssertProblem(DialogueAsset dialogue, string fragment)
        {
            var errors = dialogue.Validate();
            Assert.That(errors.Any(e => e.Contains(fragment)), Is.True,
                $"Expected a problem containing \"{fragment}\", got:\n{string.Join("\n", errors)}");
        }

        // ---- A good dialogue ----

        [Test]
        public void AGoodDialogue_HasNoProblems()
        {
            Assert.That(GoodDialogue().Validate(), Is.Empty);
        }

        [Test]
        public void AGoodDialogue_ConvertsToTheModulesData_AsWritten()
        {
            var data = GoodDialogue().ToDialogueData();

            Assert.That(data.Id, Is.EqualTo("dialogue_blacksmith"));
            Assert.That(data.Title, Is.EqualTo("Blacksmith"));
            Assert.That(data.RootNodeId, Is.EqualTo("greet"));
            Assert.That(data.Nodes.Select(n => n.Id), Is.EqualTo(new[] { "greet", "offer", "thanks", "bye" }));

            var offer = data.Nodes[1];
            Assert.That(offer.NodeType, Is.EqualTo(DialogueNodeType.Choice));
            Assert.That(offer.SpeakerId, Is.EqualTo("npc_blacksmith"));
            Assert.That(offer.Text, Is.EqualTo("Text of offer"));
            Assert.That(offer.Choices.Select(c => c.TargetNodeId), Is.EqualTo(new[] { "thanks", "bye" }));
            Assert.That(offer.Condition.HasValue, Is.False);
            Assert.That(data.Nodes[0].NextNodeIds, Is.EqualTo(new[] { "offer" }));
        }

        [Test]
        public void AGoodDialogue_PlaysToTheEnd_InTheRealDialogueManager()
        {
            var registry = new DialogueRegistry();
            registry.RegisterDialogue(GoodDialogue().ToDialogue());
            var entities = new EntityManager();
            var manager = new DialogueManager(entities, registry);
            int player = entities.CreateEntity();

            Assert.That(manager.StartDialogue(player, "dialogue_blacksmith"), Is.True);
            Assert.That(manager.GetCurrentNode(player).Id, Is.EqualTo("greet"));
            Assert.That(manager.GetCurrentNode(player).Text, Is.EqualTo("Text of greet"));

            Assert.That(manager.AdvanceDialogue(player), Is.True);
            Assert.That(manager.GetCurrentNode(player).Id, Is.EqualTo("offer"));
            Assert.That(manager.GetAvailableChoices(player).Select(c => c.Id), Is.EqualTo(new[] { "accept", "decline" }));
            Assert.That(manager.GetAvailableChoices(player)[0].Text, Is.EqualTo("Say accept"));

            Assert.That(manager.AdvanceDialogue(player, "accept"), Is.True);
            Assert.That(manager.GetCurrentNode(player).Id, Is.EqualTo("thanks"));

            Assert.That(manager.AdvanceDialogue(player), Is.True);
            Assert.That(manager.GetDialogueProgress(player), Is.Null, "reaching the End node finishes the conversation");
            Assert.That(entities.TryGetComponent<ActiveDialoguesData>(player, out var record), Is.True);
            Assert.That(record.CompletedDialogueIds, Does.Contain("dialogue_blacksmith"));
        }

        [Test]
        public void TheOtherAnswer_TakesTheOtherPath_InTheRealDialogueManager()
        {
            var registry = new DialogueRegistry();
            registry.RegisterDialogue(GoodDialogue().ToDialogue());
            var entities = new EntityManager();
            var manager = new DialogueManager(entities, registry);
            int player = entities.CreateEntity();

            manager.StartDialogue(player, "dialogue_blacksmith");
            manager.AdvanceDialogue(player);

            Assert.That(manager.AdvanceDialogue(player, "decline"), Is.True);
            Assert.That(manager.GetDialogueProgress(player), Is.Null, "declining goes straight to the end");
            entities.TryGetComponent<ActiveDialoguesData>(player, out var record);
            Assert.That(record.TotalDialoguesCompleted, Is.EqualTo(1));
        }

        [Test]
        public void Conditions_AreCarriedOver_OnlyWhenTicked()
        {
            var dialogue = GoodDialogue();
            var accept = Find(dialogue, "offer").choices[0];
            accept.hasCondition = true;
            accept.condition = new DialogueConditionEntry
            {
                type = ConditionType.StatCheck, parameterId = "Strength", requiredValue = 12
            };
            var decline = Find(dialogue, "offer").choices[1];
            decline.condition.parameterId = "ignored";

            var choices = dialogue.ToDialogue().GetNode("offer").Choices;

            Assert.That(choices[0].Condition, Is.Not.Null);
            Assert.That(choices[0].Condition.Type, Is.EqualTo(ConditionType.StatCheck));
            Assert.That(choices[0].Condition.ParameterId, Is.EqualTo("Strength"));
            Assert.That(choices[0].Condition.RequiredValue, Is.EqualTo(12));
            Assert.That(choices[1].Condition, Is.Null);
            Assert.That(dialogue.Validate(), Is.Empty);
        }

        [Test]
        public void EmptySlots_AreLeftOut_OfTheConvertedDialogue()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "greet").nextNodeIds.Add("");
            Find(dialogue, "offer").choices.Add(null);

            var greet = dialogue.ToDialogue().GetNode("greet");
            var offer = dialogue.ToDialogue().GetNode("offer");

            Assert.That(greet.NextNodeIds, Is.EqualTo(new[] { "offer" }));
            Assert.That(offer.Choices.Count, Is.EqualTo(2));
        }

        // ---- Each check ----

        [Test]
        public void AnEmptyDialogueId_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.dialogueId = " ";
            AssertProblem(dialogue, "Dialogue ID is empty");
        }

        [Test]
        public void ADialogueWithoutNodes_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.nodes.Clear();
            AssertProblem(dialogue, "has no nodes");
        }

        [Test]
        public void AnEmptyNodeSlot_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.nodes.Add(null);
            AssertProblem(dialogue, "Node #5 is empty");
        }

        [Test]
        public void AMissingStartNode_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.startNodeId = "";
            AssertProblem(dialogue, "Start node is not set");
        }

        [Test]
        public void AStartNodeThatDoesNotExist_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.startNodeId = "hello";
            AssertProblem(dialogue, "Start node 'hello' does not exist");
        }

        [Test]
        public void AStartNodeThatIsAnEnd_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.startNodeId = "bye";
            AssertProblem(dialogue, "Start node 'bye' is an End node");
        }

        [Test]
        public void ANodeWithoutId_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.nodes.Add(Node("", DialogueNodeType.Line, "bye"));
            AssertProblem(dialogue, "Node #5 has no ID");
        }

        [Test]
        public void TwoNodesWithTheSameId_AreReported()
        {
            var dialogue = GoodDialogue();
            dialogue.nodes.Add(Node("thanks", DialogueNodeType.End));
            AssertProblem(dialogue, "Two nodes use the ID 'thanks'");
        }

        [Test]
        public void ANextNodeThatDoesNotExist_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "thanks").nextNodeIds[0] = "farewell";
            AssertProblem(dialogue, "Node 'thanks' continues to 'farewell', which does not exist");
        }

        [Test]
        public void AnEmptyNextNodeSlot_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "thanks").nextNodeIds.Add("");
            AssertProblem(dialogue, "Node 'thanks' has an empty next node slot");
        }

        [Test]
        public void AChoiceLeadingToANodeThatDoesNotExist_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "offer").choices[1].targetNodeId = "leave";
            AssertProblem(dialogue, "choice 'decline' leads to 'leave', which does not exist");
        }

        [Test]
        public void AChoiceLeadingNowhere_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "offer").choices[1].targetNodeId = "";
            AssertProblem(dialogue, "choice 'decline' does not lead anywhere");
        }

        [Test]
        public void AChoiceWithoutId_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "offer").choices[1].choiceId = "";
            AssertProblem(dialogue, "choice #2 has no ID");
        }

        [Test]
        public void TwoChoicesWithTheSameId_AreReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "offer").choices[1].choiceId = "accept";
            AssertProblem(dialogue, "two choices use the ID 'accept'");
        }

        [Test]
        public void AChoiceWithoutText_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "offer").choices[0].text = "";
            AssertProblem(dialogue, "choice 'accept' has no text");
        }

        [Test]
        public void AnEmptyChoiceSlot_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "offer").choices.Add(null);
            AssertProblem(dialogue, "choice #3 is empty");
        }

        [Test]
        public void AChoiceNodeWithoutChoices_IsReported()
        {
            var dialogue = GoodDialogue();
            var offer = Find(dialogue, "offer");
            offer.choices.Clear();
            offer.nextNodeIds.Add("thanks");
            AssertProblem(dialogue, "Choice node 'offer' has no choices");
        }

        [Test]
        public void ADeadEndThatIsNotAnEnd_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "thanks").nextNodeIds.Clear();
            AssertProblem(dialogue, "Node 'thanks' is a dead end");
        }

        [Test]
        public void AnEndNodeWithFollowUps_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "bye").nextNodeIds.Add("greet");
            AssertProblem(dialogue, "End node 'bye' has next nodes or choices that are never used");
        }

        [Test]
        public void ANodeNoPathReaches_IsReported()
        {
            var dialogue = GoodDialogue();
            dialogue.nodes.Add(Node("secret", DialogueNodeType.Line, "bye"));
            AssertProblem(dialogue, "Node 'secret' can never be reached");
        }

        [Test]
        public void AConversationThatNeverEnds_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "thanks").nextNodeIds[0] = "greet";
            Find(dialogue, "offer").choices[1].targetNodeId = "greet";
            AssertProblem(dialogue, "No End node can be reached");
        }

        [Test]
        public void AConditionWithNothingToCheck_IsReported()
        {
            var dialogue = GoodDialogue();
            Find(dialogue, "greet").hasCondition = true;
            Find(dialogue, "offer").choices[0].hasCondition = true;

            AssertProblem(dialogue, "Node 'greet' has a condition with nothing to check");
            AssertProblem(dialogue, "choice 'accept' has a condition with nothing to check");
        }

        // ---- Database ----

        [Test]
        public void ADatabase_RegistersItsDialogues_SoTheManagerCanStartThem()
        {
            var database = Make<DialogueDatabase>();
            database.dialogues = new List<DialogueAsset> { GoodDialogue("dialogue_a"), GoodDialogue("dialogue_b") };
            var registry = new DialogueRegistry();
            var entities = new EntityManager();
            var manager = new DialogueManager(entities, registry);

            Assert.That(database.ValidateDatabase(), Is.Empty);
            Assert.That(database.RegisterAll(registry), Is.EqualTo(2));
            Assert.That(manager.StartDialogue(entities.CreateEntity(), "dialogue_b"), Is.True);
            Assert.That(database.GetDialogue("dialogue_a"), Is.SameAs(database.dialogues[0]));
        }

        [Test]
        public void ADatabase_SkipsBrokenDialogues_AndReportsThem()
        {
            var broken = GoodDialogue("dialogue_broken");
            broken.name = "Broken";
            broken.startNodeId = "nowhere";
            var database = Make<DialogueDatabase>();
            database.dialogues = new List<DialogueAsset> { GoodDialogue("dialogue_a"), broken, null };

            var errors = database.ValidateDatabase();

            Assert.That(errors.Any(e => e.Contains("Broken: Start node 'nowhere' does not exist")), Is.True, string.Join("\n", errors));
            Assert.That(errors.Any(e => e.Contains("1 empty dialogue slot")), Is.True, string.Join("\n", errors));
            Assert.That(database.RegisterAll(new DialogueRegistry()), Is.EqualTo(1));
        }

        [Test]
        public void ADatabase_ReportsTwoDialoguesWithTheSameId()
        {
            var database = Make<DialogueDatabase>();
            database.dialogues = new List<DialogueAsset> { GoodDialogue("dialogue_a"), GoodDialogue("dialogue_a") };

            var errors = database.ValidateDatabase();

            Assert.That(errors.Any(e => e.Contains("Two dialogues use the ID 'dialogue_a'")), Is.True, string.Join("\n", errors));
            Assert.That(database.RegisterAll(new DialogueRegistry()), Is.EqualTo(1));
        }
    }
}
