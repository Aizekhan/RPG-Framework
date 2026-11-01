using System.Linq;
using NUnit.Framework;
using RPGFramework.Dialogue;
using RPGFramework.Quests;

namespace RPGFramework.ContentImport.Tests
{
    public sealed class ContentImporterTests
    {
        // A quest with branches and the conversation that gives it, as a design tool would export them
        internal const string LostHammer = @"{
  ""schema"": ""rpgframework.content/1"",
  ""quests"": [{
    ""id"": ""quest_m1x2"", ""title"": ""The Lost Hammer"", ""type"": ""main"",
    ""description"": ""Lyra asks the player to recover the hammer the Rust Golem guards."",
    ""repeatable"": false,
    ""requirements"": [{ ""kind"": ""flag"", ""id"": ""met_lyra"" }],
    ""objectives"": [{ ""id"": ""reach"", ""type"": ""Explore"", ""target"": ""forge1"" }],
    ""rewards"": [{ ""type"": ""experience"", ""amount"": 200 }, { ""type"": ""item"", ""id"": ""old_hammer"" }],
    ""start"": ""s1"",
    ""stages"": [
      { ""id"": ""s1"", ""title"": ""Into the forest"", ""objectives"": [""reach""], ""next"": [{ ""to"": ""s2"" }] },
      { ""id"": ""s2"", ""title"": ""The guardian"", ""next"": [{ ""to"": ""s3"", ""condition"": ""golem_defeated"" }, { ""to"": ""s4"", ""condition"": ""player_died_3x"" }] },
      { ""id"": ""s3"", ""title"": ""Return"", ""outcome"": ""success"" },
      { ""id"": ""s4"", ""title"": ""Retreat"", ""outcome"": ""failure"" }
    ]
  }],
  ""dialogues"": [{
    ""id"": ""dlg_k9p0"", ""title"": ""Lyra at the forest edge"", ""type"": ""quest"",
    ""quest"": ""quest_m1x2"", ""stage"": ""s1"", ""participants"": [""lyra""], ""start"": ""n1"",
    ""nodes"": [
      { ""id"": ""n1"", ""speaker"": ""lyra"", ""text"": ""You there! Have you seen a hammer?"", ""key"": ""dlg.lyra-edge.n1"",
        ""choices"": [{ ""text"": ""I can find it."", ""to"": ""n2"" }, { ""id"": ""refuse"", ""text"": ""Not my problem."", ""to"": ""n3"",
          ""effects"": [{ ""kind"": ""setVariable"", ""target"": ""lyra_refused"" }] }] },
      { ""id"": ""n2"", ""type"": ""end"", ""speaker"": ""lyra"", ""text"": ""The golem took it into Forge-1."",
        ""effects"": [{ ""kind"": ""acceptQuest"", ""target"": ""quest_m1x2"" }] },
      { ""id"": ""n3"", ""type"": ""end"", ""speaker"": ""player"", ""text"": ""Not my problem."",
        ""condition"": { ""type"": ""variable"", ""id"": ""grumpy"", ""value"": 0 } }
    ]
  }]
}";

        [Test]
        public void AFileBecomesQuestsDialoguesAndEffects()
        {
            var package = ContentImporter.Read(LostHammer);

            Assert.That(package.Problems, Is.Empty, string.Join("\n", package.Problems));
            var quest = package.Quests.Single();
            Assert.That(quest.Type, Is.EqualTo("main"));
            Assert.That(quest.Objectives.Single().Type, Is.EqualTo(ObjectiveType.Explore));
            Assert.That(quest.Objectives.Single().RequiredCount, Is.EqualTo(1), "count defaults to 1");
            Assert.That(quest.Rewards.Select(r => r.Type), Is.EqualTo(new[] { RewardType.Experience, RewardType.Item }));
            Assert.That(quest.Requirements.Single().Value, Is.EqualTo(1));
            Assert.That(quest.Stages.Select(s => s.Outcome), Is.EqualTo(new[] { QuestOutcome.None, QuestOutcome.None, QuestOutcome.Success, QuestOutcome.Failure }));
            Assert.That(quest.Stages[1].Next.Select(l => l.Condition), Is.EqualTo(new[] { "golem_defeated", "player_died_3x" }));

            var dialogue = new global::RPGFramework.Dialogue.Dialogue(package.Dialogues.Single());
            Assert.That(dialogue.QuestStageId, Is.EqualTo("s1"));
            var first = dialogue.GetNode("n1");
            Assert.That(first.NodeType, Is.EqualTo(DialogueNodeType.Choice), "a node with choices is a choice");
            Assert.That(first.Choices.Select(c => c.Id), Is.EqualTo(new[] { "c1", "refuse" }));
            Assert.That(((ILocalizedLine)first).TextKey, Is.EqualTo("dlg.lyra-edge.n1"));
            Assert.That(dialogue.GetNode("n3").Condition.Type, Is.EqualTo(ConditionType.VariableCheck));
            Assert.That(package.Effects.Select(e => $"{e.NodeId ?? e.ChoiceId}:{e.Kind}:{e.Target}"),
                Is.EquivalentTo(new[] { "refuse:setVariable:lyra_refused", "n2:acceptQuest:quest_m1x2" }));
        }

        [Test]
        public void WhatIsWrong_IsNamed_AndBreaksTheImport()
        {
            var package = ContentImporter.Read(@"{ ""schema"": ""rpgframework.content/1"",
              ""quests"": [{ ""title"": ""No id"" }, { ""id"": ""q"", ""objectives"": [{ ""id"": ""o"", ""type"": ""dance"" }],
                ""stages"": [{ ""id"": ""a"", ""next"": [{ ""to"": ""b"" }] }] }],
              ""dialogues"": [{ ""id"": ""d"", ""quest"": ""q"", ""stage"": ""zz"", ""nodes"": [{ ""id"": ""x"", ""text"": ""Hi"" }, { ""id"": ""x"" }] },
                              { ""id"": ""e"", ""start"": ""a"", ""nodes"": [{ ""id"": ""a"", ""text"": ""Hm"", ""condition"": { ""type"": ""weather"" } }] }] }");

            Assert.That(package.HasErrors, Is.True);
            var codes = package.Problems.Select(p => p.Code).ToList();
            Assert.That(codes, Has.Member("noId").And.Member("unknownValue").And.Member("badLink").And.Member("noEnd").And.Member("dupNode"));
            Assert.That(package.Problems.Where(p => p.Code == "unknownValue").Select(p => p.Where), Has.Some.Contains("objective o"));
            Assert.That(package.Problems.Where(p => p.Code == "unknownValue").Select(p => p.Where), Has.Some.Contains("dialogue e/a"));
            Assert.That(package.Dialogues.Select(d => d.Id), Is.EqualTo(new[] { "e" }), "a dialogue with two nodes of one id is left out");
        }

        [Test]
        public void BrokenJsonOrAnotherSchema_IsAnError()
        {
            Assert.That(ContentImporter.Read("{ not json").Problems.Single().Code, Is.EqualTo("badJson"));
            Assert.That(ContentImporter.Read(@"{ ""schema"": ""other/2"" }").Problems.Single().Code, Is.EqualTo("badSchema"));
            Assert.That(ContentImporter.Read(@"{ ""quests"": [] }").Problems.Single().Level, Is.EqualTo(ContentProblemLevel.Warning),
                "no schema: read as the current one");
        }

        [Test]
        public void ADialoguesQuestOutsideTheFile_IsAWarning_OrKnownToTheGame()
        {
            const string json = @"{ ""schema"": ""rpgframework.content/1"", ""quests"": [{ ""id"": ""q"", ""title"": ""Q"", ""prerequisites"": [""old""] }],
              ""dialogues"": [{ ""id"": ""d"", ""quest"": ""elsewhere"", ""nodes"": [{ ""id"": ""a"", ""text"": ""Hi"", ""next"": [""b""] }, { ""id"": ""b"", ""type"": ""end"", ""text"": ""Bye"" }] }] }";

            var alone = ContentImporter.Read(json);
            Assert.That(alone.HasErrors, Is.False, string.Join("\n", alone.Problems));
            Assert.That(alone.Problems.Select(p => p.Code), Has.Member("missingQuest").And.Member("missingPrerequisite"));

            var inGame = ContentImporter.Read(json, id => id == "old");
            Assert.That(inGame.Problems.Select(p => p.Code), Has.No.Member("missingPrerequisite"));
            Assert.That(inGame.Problems.Where(p => p.Code == "missingQuest").Single().Level, Is.EqualTo(ContentProblemLevel.Error));
        }

        [Test]
        public void WrittenContent_ReadsBackTheSame()
        {
            var first = ContentImporter.Read(LostHammer);

            var again = ContentImporter.Read(ContentImporter.Write(first.Quests, first.Dialogues, first.Effects));

            Assert.That(again.Problems, Is.Empty, string.Join("\n", again.Problems));
            Assert.That(again.Quests.Single().Stages.Select(s => s.Id), Is.EqualTo(first.Quests.Single().Stages.Select(s => s.Id)));
            Assert.That(again.Quests.Single().Rewards.Length, Is.EqualTo(2));
            Assert.That(again.Dialogues.Single().Nodes.Select(n => n.NodeType), Is.EqualTo(first.Dialogues.Single().Nodes.Select(n => n.NodeType)));
            Assert.That(again.Effects.Count, Is.EqualTo(2));
            Assert.That(again.Dialogues.Single().Nodes[2].Condition.Value.RequiredValue, Is.EqualTo(0));
        }
    }
}
