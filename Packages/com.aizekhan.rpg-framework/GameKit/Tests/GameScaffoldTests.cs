using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RPGFramework.GameKit.Editor;

namespace RPGFramework.GameKit.Tests
{
    /// <summary>
    /// The generators write complete, named, meta-carrying file trees into a temporary folder (compiling the result
    /// is checked by generating a game in a project; see the package README).
    /// </summary>
    public sealed class GameScaffoldTests
    {
        private string _assets;
        private string _project;

        [SetUp]
        public void SetUp()
        {
            _project = Path.Combine(Path.GetTempPath(), "GameKitScaffold_" + Guid.NewGuid().ToString("N"));
            _assets = Path.Combine(_project, "Assets");
            Directory.CreateDirectory(_assets);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_project, true);

        private string Read(string relative) => File.ReadAllText(Path.Combine(_assets, relative));

        [Test]
        public void NewGame_WritesTheLayeredStructureWithMetas()
        {
            var files = GameScaffold.WriteGame(GameScaffold.TemplatesRoot, _assets, "DragonPeak");

            var relative = files.Select(f => f.Substring(_assets.Length + 1).Replace('\\', '/')).ToList();
            Assert.That(relative, Is.SupersetOf(new[]
            {
                "DragonPeak/Scripts/Core/DragonPeak.Core.asmdef",
                "DragonPeak/Scripts/Core/DragonPeakComposition.cs",
                "DragonPeak/Scripts/Core/DragonPeakModule.cs",
                "DragonPeak/Scripts/DragonPeak.asmdef",
                "DragonPeak/Scripts/DragonPeakHost.cs",
                "DragonPeak/Scenes/DragonPeak.unity",
                "DragonPeak/Tests/DragonPeak.Tests.asmdef",
                "DragonPeak/Tests/DragonPeakArchitectureTests.cs"
            }));

            // Every file and folder has a .meta with a valid GUID, and no template token is left
            var everything = Directory.GetFileSystemEntries(Path.Combine(_assets, "DragonPeak"), "*", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".meta")).Append(Path.Combine(_assets, "DragonPeak")).ToList();
            foreach (var path in everything)
            {
                Assert.That(File.Exists(path + ".meta"), Is.True, path);
                Assert.That(Regex.IsMatch(File.ReadAllText(path + ".meta"), @"guid: [0-9a-f]{32}\n"), Is.True, path);
            }
            foreach (var file in files)
                Assert.That(File.ReadAllText(file), Does.Not.Contain("__"), file);

            Assert.That(Read("DragonPeak/Scripts/Core/DragonPeak.Core.asmdef"), Does.Contain("\"noEngineReferences\": true"));
        }

        [Test]
        public void NewGame_SceneHostPointsAtTheGeneratedScript()
        {
            GameScaffold.WriteGame(GameScaffold.TemplatesRoot, _assets, "DragonPeak");

            string meta = Read("DragonPeak/Scripts/DragonPeakHost.cs.meta");
            string guid = Regex.Match(meta, "guid: ([0-9a-f]{32})").Groups[1].Value;
            Assert.That(Read("DragonPeak/Scenes/DragonPeak.unity"), Does.Contain($"guid: {guid}, type: 3"));
        }

        [Test]
        public void NewGame_RefusesToOverwriteAndBadNames()
        {
            GameScaffold.WriteGame(GameScaffold.TemplatesRoot, _assets, "DragonPeak");

            Assert.Throws<InvalidOperationException>(() => GameScaffold.WriteGame(GameScaffold.TemplatesRoot, _assets, "DragonPeak"));
            Assert.Throws<ArgumentException>(() => GameScaffold.WriteGame(GameScaffold.TemplatesRoot, _assets, "dragon peak"));
            Assert.Throws<ArgumentException>(() => GameScaffold.WriteGame(GameScaffold.TemplatesRoot, _assets, "1Up"));
        }

        [Test]
        public void NewFeature_WritesRulesAndTestsAndJoinsTheComposition()
        {
            GameScaffold.WriteGame(GameScaffold.TemplatesRoot, _assets, "DragonPeak");

            var (files, added) = GameScaffold.WriteFeature(GameScaffold.TemplatesRoot, _assets, "DragonPeak", "Shop");

            Assert.That(added, Is.True);
            Assert.That(files.Select(Path.GetFileName), Is.EquivalentTo(new[]
            {
                "ShopModule.cs", "ShopRules.cs", "ShopEvents.cs", "ShopTests.cs"
            }));
            Assert.That(File.Exists(Path.Combine(_assets, "DragonPeak/Scripts/Core/Shop.meta")), Is.True);

            var composition = Read("DragonPeak/Scripts/Core/DragonPeakComposition.cs");
            int shop = composition.IndexOf(".UseModule(new ShopModule())", StringComparison.Ordinal);
            int marker = composition.IndexOf(GameScaffold.ModulesMarker, StringComparison.Ordinal);
            int game = composition.IndexOf(".UseModule(new DragonPeakModule())", StringComparison.Ordinal);
            Assert.That(shop, Is.GreaterThan(0).And.LessThan(marker));
            Assert.That(marker, Is.LessThan(game));

            // Twice does not add the module twice
            Assert.That(GameScaffold.AddModuleToComposition(
                Path.Combine(_assets, "DragonPeak/Scripts/Core/DragonPeakComposition.cs"), "ShopModule"), Is.True);
            Assert.That(Regex.Matches(Read("DragonPeak/Scripts/Core/DragonPeakComposition.cs"), "new ShopModule").Count, Is.EqualTo(1));
        }

        [Test]
        public void NewFeature_NeedsAnExistingGame()
        {
            Assert.Throws<InvalidOperationException>(() =>
                GameScaffold.WriteFeature(GameScaffold.TemplatesRoot, _assets, "Nowhere", "Shop"));
        }

        [Test]
        public void AIGuidance_InstallsDocsAndKeepsTheRestOfClaudeMd()
        {
            File.WriteAllText(Path.Combine(_project, "CLAUDE.md"), "# My notes\nKeep this.\n");

            var written = AIGuidance.Install(AIGuidance.DocumentationRoot, _project);
            AIGuidance.Install(AIGuidance.DocumentationRoot, _project); // reinstall replaces, never duplicates

            string claude = File.ReadAllText(Path.Combine(_project, "CLAUDE.md"));
            Assert.That(claude, Does.StartWith("# My notes\nKeep this."));
            Assert.That(Regex.Matches(claude, Regex.Escape(AIGuidance.BlockBegin)).Count, Is.EqualTo(1));
            Assert.That(written, Has.Member("Docs/RPGFramework/architecture.md"));
            Assert.That(written.Count(w => w.StartsWith("Docs/RPGFramework/recipes/")), Is.GreaterThanOrEqualTo(5));
            Assert.That(File.Exists(Path.Combine(_project, AIGuidance.WorkflowPath)), Is.True);
        }

        [Test]
        public void AIGuidance_DoesNotReplaceAnExistingWorkflow()
        {
            string workflow = Path.Combine(_project, AIGuidance.WorkflowPath);
            Directory.CreateDirectory(Path.GetDirectoryName(workflow));
            File.WriteAllText(workflow, "mine");

            var written = AIGuidance.Install(AIGuidance.DocumentationRoot, _project);

            Assert.That(File.ReadAllText(workflow), Is.EqualTo("mine"));
            Assert.That(written, Has.No.Member(AIGuidance.WorkflowPath));
        }

        [Test]
        public void AIGuidance_Workflow_FailsWithoutLicenceSecrets_AndReplacesTheOneThatPassedSkipped()
        {
            string template = File.ReadAllText(Path.Combine(AIGuidance.DocumentationRoot, "ci", "unity-tests.yml"));
            Assert.That(template, Does.StartWith(AIGuidance.WorkflowHeader));
            Assert.That(template, Does.Contain("exit 1"));
            Assert.That(AIGuidance.IsGreenWhenSkippedWorkflow(template), Is.False);

            // The 0.5.1 workflow: a run without secrets skipped every step and was green
            string workflow = Path.Combine(_project, AIGuidance.WorkflowPath);
            Directory.CreateDirectory(Path.GetDirectoryName(workflow));
            File.WriteAllText(workflow, AIGuidance.WorkflowHeader + " (only when this file did not exist).\n" +
                                        "          echo \"available=false\" >> \"$GITHUB_OUTPUT\"\n");

            var written = AIGuidance.Install(AIGuidance.DocumentationRoot, _project);

            Assert.That(written, Has.Member(AIGuidance.WorkflowPath));
            Assert.That(File.ReadAllText(workflow), Is.EqualTo(template.Replace("\r\n", "\n")));
        }

        [Test]
        public void AIGuidance_PutsBinaryAssetsInGitLfs_OnlyWhenTheProjectHasNoGitAttributes()
        {
            var written = AIGuidance.Install(AIGuidance.DocumentationRoot, _project);
            string attributes = Path.Combine(_project, AIGuidance.GitAttributesPath);

            Assert.That(written, Has.Member(AIGuidance.GitAttributesPath));
            foreach (var extension in new[] { "fbx", "png", "wav" })
                Assert.That(File.ReadAllText(attributes), Does.Contain($"*.{extension} filter=lfs diff=lfs merge=lfs -text"));

            File.WriteAllText(attributes, "mine");
            var again = AIGuidance.Install(AIGuidance.DocumentationRoot, _project);
            Assert.That(File.ReadAllText(attributes), Is.EqualTo("mine"));
            Assert.That(again, Has.No.Member(AIGuidance.GitAttributesPath));
        }

        [Test]
        public void AIGuidance_CreatesTheGamesOwnDocumentsOnce_AndNeverOverwritesThem()
        {
            var written = AIGuidance.Install(AIGuidance.DocumentationRoot, _project);
            Assert.That(written, Is.SupersetOf(new[] { "GAME.md", GameArchitectureRules.GapsFile, "DEVLOG.md" }));
            Assert.That(File.ReadAllText(Path.Combine(_project, GameArchitectureRules.GapsFile)), Does.Contain("## Open"));

            string game = Path.Combine(_project, "GAME.md");
            File.WriteAllText(game, "# Dragon Peak\nOur decisions.");
            var again = AIGuidance.Install(AIGuidance.DocumentationRoot, _project);

            Assert.That(File.ReadAllText(game), Is.EqualTo("# Dragon Peak\nOur decisions."));
            Assert.That(again, Has.No.Member("GAME.md"));
            string claude = File.ReadAllText(Path.Combine(_project, "CLAUDE.md"));
            Assert.That(claude, Does.Contain("framework-gap").And.Contain("GAME.md"));
        }

        [Test]
        public void EveryRule_IsATestInEveryGame()
        {
            var tests = typeof(RPGFramework.GameKit.TestSupport.GameArchitectureTests).GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0).Select(m => m.Name).ToList();
            foreach (var rule in GameArchitectureRules.All)
                Assert.That(tests.Any(t => t.StartsWith(rule.Id + "_")), Is.True, $"GameArchitectureTests has no test for {rule.Id}");
        }

        [Test]
        public void Guide_ExplainsEveryRule()
        {
            string guide = File.ReadAllText(Path.Combine(AIGuidance.DocumentationRoot, "architecture.md"));
            foreach (var rule in GameArchitectureRules.All)
                Assert.That(guide, Does.Contain($"### {rule.Id} "), rule.Id);
        }
    }
}
