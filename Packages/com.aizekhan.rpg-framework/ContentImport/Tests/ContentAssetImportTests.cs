using System.Linq;
using NUnit.Framework;
using RPGFramework.ContentImport.Editor;
using RPGFramework.Quests;
using RPGFramework.Tools.DialogueEditor;
using RPGFramework.Tools.QuestEditor;
using UnityEditor;

namespace RPGFramework.ContentImport.Tests
{
    public sealed class ContentAssetImportTests
    {
        private const string Folder = "Assets/__ContentImportTest";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void AFileBecomesQuestAndDialogueAssets_AndASecondImportUpdatesThem()
        {
            var result = ContentAssetImport.Import(ContentImporterTests.LostHammer, Folder);

            Assert.That(result.Imported, Is.True, string.Join("\n", result.Problems));
            Assert.That(result.Created.Count, Is.EqualTo(2));
            var quest = AssetDatabase.LoadAssetAtPath<QuestAsset>(result.Created.Single(p => p.Contains("quest_m1x2")));
            Assert.That(quest.stages.Select(s => s.stageId), Is.EqualTo(new[] { "s1", "s2", "s3", "s4" }));
            Assert.That(quest.ToQuestData().Stages[2].Outcome, Is.EqualTo(QuestOutcome.Success));
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueAsset>(result.Created.Single(p => p.Contains("dlg_k9p0")));
            Assert.That(dialogue.Validate(), Is.Empty);
            Assert.That(dialogue.nodes[0].textKey, Is.EqualTo("dlg.lyra-edge.n1"));
            Assert.That(dialogue.questStageId, Is.EqualTo("s1"));
            Assert.That(result.Problems.Select(p => p.Code), Has.Member("effectsNotInAssets"));

            var again = ContentAssetImport.Import(ContentImporterTests.LostHammer.Replace("The Lost Hammer", "The Hammer"), Folder);

            Assert.That(again.Created, Is.Empty);
            Assert.That(again.Updated.Count, Is.EqualTo(2));
            Assert.That(quest.title, Is.EqualTo("The Hammer"));
        }

        [Test]
        public void AFileWithErrors_WritesNothing()
        {
            var result = ContentAssetImport.Import(@"{ ""schema"": ""rpgframework.content/1"", ""quests"": [{ ""title"": ""no id"" }] }", Folder);

            Assert.That(result.Imported, Is.False);
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
        }
    }
}
