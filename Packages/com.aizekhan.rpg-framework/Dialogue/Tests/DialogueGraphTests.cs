using System;
using System.Linq;
using NUnit.Framework;

namespace RPGFramework.Dialogue.Tests
{
    public sealed class DialogueGraphTests
    {
        private static DialogueNodeData Line(string id, string text, params string[] next) =>
            new DialogueNodeData { Id = id, NodeType = DialogueNodeType.Line, SpeakerId = "lyra", Text = text, NextNodeIds = next };

        private static DialogueNodeData End(string id) =>
            new DialogueNodeData { Id = id, NodeType = DialogueNodeType.End, SpeakerId = "lyra", Text = "Bye." };

        private static DialogueChoiceData Answer(string id, string to, string text = "Yes.") =>
            new DialogueChoiceData { Id = id, Text = text, TargetNodeId = to };

        private static DialogueData Edge() => new DialogueData
        {
            Id = "edge", Title = "Lyra at the forest edge", RootNodeId = "n1", Type = "quest", QuestId = "hammer",
            QuestStageId = "s1", ParticipantIds = new[] { "lyra" },
            Nodes = new[]
            {
                new DialogueNodeData
                {
                    Id = "n1", NodeType = DialogueNodeType.Choice, SpeakerId = "lyra", Text = "Seen a hammer?",
                    TextKey = "dlg.edge.n1", Choices = new[] { Answer("help", "n2"), Answer("no", "n3", "Not my problem.") }
                },
                Line("n2", "Be careful.", "bye"),
                new DialogueNodeData { Id = "n3", NodeType = DialogueNodeType.Line, SpeakerId = "player", Text = "Not my problem.", NextNodeIds = new[] { "bye" } },
                End("bye"),
            }
        };

        [Test]
        public void AGoodConversation_HasNoIssues_AndKeepsItsDetails()
        {
            var dialogue = new Dialogue(Edge());

            Assert.That(DialogueGraph.Check(dialogue, q => q == "hammer", (q, s) => s == "s1"), Is.Empty);
            Assert.That(dialogue.Type, Is.EqualTo("quest"));
            Assert.That(dialogue.QuestStageId, Is.EqualTo("s1"));
            Assert.That(dialogue.ParticipantIds, Is.EqualTo(new[] { "lyra" }));
        }

        [Test]
        public void TheCheck_NamesDeadEndsLoopsMissingLinksSpeakersAndQuests()
        {
            var data = Edge();
            data.Nodes = new[]
            {
                new DialogueNodeData { Id = "n1", NodeType = DialogueNodeType.Choice, SpeakerId = "lyra", Text = "Well?", Choices = new[] { Answer("a", "loop1"), Answer("b", "ghost") } },
                Line("loop1", "Round...", "loop2"),
                Line("loop2", "...and round.", "loop1"),
                new DialogueNodeData { Id = "stuck", NodeType = DialogueNodeType.Line, SpeakerId = "stranger", Text = "" },
                new DialogueNodeData { Id = "empty", NodeType = DialogueNodeType.Choice, SpeakerId = "lyra", Text = "Hm?" },
            };

            var codes = DialogueGraph.Check(new Dialogue(data), q => false).Select(i => i.Code).ToList();

            Assert.That(codes, Is.EquivalentTo(new[]
            {
                "badLink", "emptyText", "speakerNotIn", "noChoices", "missingQuest", "unreachable", "unreachable",
                "noWayOut", "noWayOut", "noWayOut"
            }));
        }

        [Test]
        public void AStageTheQuestDoesNotHave_AndAnEndAtTheStart_AreErrors()
        {
            var data = Edge();
            Assert.That(DialogueGraph.Check(new Dialogue(data), q => true, (q, s) => false).Single().Code, Is.EqualTo("missingStage"));

            data.RootNodeId = "bye";
            Assert.That(DialogueGraph.Check(new Dialogue(data)).Select(i => i.Code), Has.Member("startIsEnd"));
            data.RootNodeId = "nowhere";
            Assert.That(DialogueGraph.Check(new Dialogue(data)).Select(i => i.Code), Has.Member("badStart"));
        }

        [Test]
        public void ALineWithAKey_IsShownInThePlayersLanguage()
        {
            var dialogue = new Dialogue(Edge());
            var node = dialogue.GetNode("n1");
            Func<string, string, string> ukrainian = (key, fallback) => key == "dlg.edge.n1" ? "Бачив молот?" : fallback;

            Assert.That(DialogueText.Of(node, ukrainian), Is.EqualTo("Бачив молот?"));
            Assert.That(DialogueText.Of(node, null), Is.EqualTo("Seen a hammer?"));
            Assert.That(DialogueText.Of(dialogue.GetNode("n2"), ukrainian), Is.EqualTo("Be careful."), "no key: the text as written");
            Assert.That(DialogueText.Of(node.Choices[0], ukrainian), Is.EqualTo("Yes."));
            Assert.That(DialogueText.KeyFor("edge", "n1", "help"), Is.EqualTo("dlg.edge.n1.help"));
        }

        [Test]
        public void AOneLineConversation_IsFine_ABranchThatLeadsNowhere_IsADeadEnd()
        {
            var oneLine = new DialogueData { Id = "hi", RootNodeId = "a", Nodes = new[] { Line("a", "Morning.") } };
            Assert.That(DialogueGraph.Check(new Dialogue(oneLine)), Is.Empty, "a line with no way on ends the conversation");

            var fork = new DialogueData
            {
                Id = "fork", RootNodeId = "a",
                Nodes = new[] { Line("a", "Hm.", "b"), new DialogueNodeData { Id = "b", NodeType = DialogueNodeType.Branch } }
            };
            Assert.That(DialogueGraph.Check(new Dialogue(fork)).Select(i => i.Code), Has.Member("deadEnd"));
        }

        [Test]
        public void ABark_IsALineWhoseConditionPasses()
        {
            var dialogue = new Dialogue(new DialogueData
            {
                Id = "barks", RootNodeId = "a", Type = "bark",
                Nodes = new[]
                {
                    Line("a", "Rain again."),
                    new DialogueNodeData
                    {
                        Id = "b", NodeType = DialogueNodeType.Line, Text = "Nice coat.",
                        Condition = new DialogueConditionData { Type = ConditionType.VariableCheck, ParameterId = "rich", RequiredValue = 1 }
                    },
                    End("end"),
                }
            });
            var random = new Random(3);

            Assert.That(Enumerable.Range(0, 20).Select(_ => DialogueBarks.Pick(dialogue, null, random).Id).Distinct(), Is.EqualTo(new[] { "a" }));
            var context = new DialogueContext(1);
            context.Variables["rich"] = 1;
            Assert.That(Enumerable.Range(0, 40).Select(_ => DialogueBarks.Pick(dialogue, context, random).Id).Distinct(),
                Is.EquivalentTo(new[] { "a", "b" }));
        }
    }
}
