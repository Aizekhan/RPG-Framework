using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace RPGFramework.Tools.ProjectIndex.Tests
{
    public sealed class ProjectIndexerTests
    {
        private const string HeroGuid = "11111111111111111111111111111111";
        private const string ItemDataGuid = "22222222222222222222222222222222";
        private const string PrefabGuid = "33333333333333333333333333333333";
        private const string SceneGuid = "44444444444444444444444444444444";
        private const string SwordGuid = "55555555555555555555555555555555";
        private const string QuestsAsmGuid = "66666666666666666666666666666666";
        private const string GameAsmGuid = "77777777777777777777777777777777";
        private const string MissingGuid = "99999999999999999999999999999999";

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ProjectIndexTest-" + Guid.NewGuid().ToString("N"));
            Write("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.0.45f1\nm_EditorVersionWithRevision: 6000.0.45f1 (abc)\n");
            Write("ProjectSettings/EditorBuildSettings.asset",
                "%YAML 1.1\nEditorBuildSettings:\n  m_Scenes:\n  - enabled: 1\n    path: Assets/Scenes/Main.unity\n    guid: " + SceneGuid +
                "\n  - enabled: 1\n    path: Assets/Scenes/Gone.unity\n    guid: 88888888888888888888888888888888\n");
            Write("Packages/manifest.json",
                "{\n  \"dependencies\": {\n    \"com.aizekhan.rpg-framework\": \"https://github.com/Aizekhan/RPG-Framework.git?path=Packages/com.aizekhan.rpg-framework#v0.32.0\",\n" +
                "    \"com.unity.render-pipelines.universal\": \"17.0.4\"\n  }\n}\n");
            Write(".git/HEAD", "ref: refs/heads/main\n");
            Write(".git/refs/heads/main", "0123456789abcdef0123456789abcdef01234567\n");
            Write("Library/PackageCache/com.aizekhan.rpg-framework@1a2b/Quests/Runtime/RPGFramework.Quests.Runtime.asmdef",
                "{ \"name\": \"RPGFramework.Quests.Runtime\" }", QuestsAsmGuid);

            Write("Assets/Game/Game.asmdef",
                "{\n  \"name\": \"Dragon.Game\",\n  \"references\": [\n    \"RPGFramework.ActionCombat.Unity\",\n    \"GUID:" + QuestsAsmGuid + "\"\n  ]\n}", GameAsmGuid);
            Write("Assets/Game/Hero.cs", string.Join("\n",
                "using UnityEngine;",
                "using RPGFramework.Inventory;",
                "",
                "namespace Dragon",
                "{",
                "    /// <summary>",
                "    /// The player's hero: moves and swings a <see cref=\"Sword\"/>.",
                "    /// </summary>",
                "    [DisallowMultipleComponent]",
                "    public sealed class Hero : MonoBehaviour, IHittable",
                "    {",
                "        public float Speed = 4f;",
                "        public int Health { get; private set; }",
                "        public void Swing(int power) { }",
                "        private void Update() { }",
                "    }",
                "",
                "    public enum Stance { Idle, Guard }",
                "    internal interface IHittable { }",
                "}"), HeroGuid);
            Write("Assets/Data/ItemData.cs", "namespace Dragon { public class ItemData : UnityEngine.ScriptableObject { } }", ItemDataGuid);
            Write("Assets/Data/Sword.asset",
                "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " + ItemDataGuid + ", type: 3}\n  m_Name: Sword\n", SwordGuid);
            Write("Assets/Prefabs/Hero.prefab", string.Join("\n",
                "%YAML 1.1",
                "--- !u!1 &100",
                "GameObject:",
                "  m_Name: HeroRoot",
                "--- !u!4 &200",
                "Transform:",
                "  m_GameObject: {fileID: 100}",
                "  m_Father: {fileID: 0}",
                "--- !u!114 &300",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 100}",
                "  m_Script: {fileID: 11500000, guid: " + HeroGuid + ", type: 3}",
                "  _weapon: {fileID: 11400000, guid: " + SwordGuid + ", type: 2}",
                "--- !u!114 &400",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 100}",
                "  m_Script: {fileID: 11500000, guid: " + MissingGuid + ", type: 3}",
                "--- !u!95 &500",
                "Animator:",
                "  m_GameObject: {fileID: 100}"), PrefabGuid);
            Write("Assets/Scenes/Main.unity", string.Join("\n",
                "%YAML 1.1",
                "--- !u!104 &2",
                "RenderSettings:",
                "  m_Fog: 0",
                "--- !u!1 &10",
                "GameObject:",
                "  m_Name: Main Camera",
                "--- !u!4 &11",
                "Transform:",
                "  m_GameObject: {fileID: 10}",
                "  m_Father: {fileID: 0}",
                "--- !u!20 &12",
                "Camera:",
                "  m_GameObject: {fileID: 10}",
                "--- !u!1001 &20",
                "PrefabInstance:",
                "  m_Modification:",
                "    m_TransformParent: {fileID: 0}",
                "  m_SourcePrefab: {fileID: 100100000, guid: " + PrefabGuid + ", type: 3}"), SceneGuid);
            Write("Assets/Secrets/store.keystore", "secret", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            Write("Assets/ThirdParty/Pack/Rock.png", "png", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private void Write(string relative, string text, string guid = null)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            if (guid != null)
                File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
        }

        private ProjectIndex Scan() =>
            ProjectIndexer.Scan(_root, new ProjectIndexOptions { Exclude = { "Assets/ThirdParty" } });

        [Test]
        public void ItReadsTheProjectsVersionPackagesBuildScenesAndRepository()
        {
            var index = Scan();

            Assert.That(index.schema, Is.EqualTo(ProjectIndex.Schema));
            Assert.That(index.project.unityVersion, Is.EqualTo("6000.0.45f1"));
            Assert.That(index.project.renderPipeline, Is.EqualTo("URP"));
            Assert.That(index.project.packages.Select(p => p.name), Has.Member("com.unity.render-pipelines.universal"));
            Assert.That(index.project.repo.branch, Is.EqualTo("main"));
            Assert.That(index.project.repo.commit, Is.EqualTo("0123456789abcdef0123456789abcdef01234567"));
            Assert.That(index.project.buildScenes.Count, Is.EqualTo(2));
            Assert.That(index.scenes.Single().buildIndex, Is.EqualTo(0));
            Assert.That(index.problems, Has.Some.Contains("Build scene Assets/Scenes/Gone.unity is not in the project"));
        }

        [Test]
        public void AssetsHaveGuidsTypesAndReferences_ScriptableObjectsAreNamedByTheirClass()
        {
            var index = Scan();

            var sword = index.assets.Single(a => a.path == "Assets/Data/Sword.asset");
            Assert.That(sword.guid, Is.EqualTo(SwordGuid));
            Assert.That(sword.type, Is.EqualTo("ScriptableObject:ItemData"));
            Assert.That(sword.deps, Has.Member(ItemDataGuid));
            var prefab = index.assets.Single(a => a.path == "Assets/Prefabs/Hero.prefab");
            Assert.That(prefab.type, Is.EqualTo("Prefab"));
            Assert.That(prefab.deps, Has.Member(HeroGuid).And.Member(SwordGuid));
        }

        [Test]
        public void ScenesAndPrefabs_ListTheirRootsAndComponents_AndMissingScripts()
        {
            var index = Scan();

            var scene = index.scenes.Single();
            Assert.That(scene.roots, Is.EquivalentTo(new[] { "Main Camera", "Hero (prefab)" }));
            Assert.That(scene.components, Is.EquivalentTo(new[] { "Camera", "Transform" }), "scene settings are not components");
            var prefab = index.prefabs.Single();
            Assert.That(prefab.root, Is.EqualTo("HeroRoot"));
            Assert.That(prefab.components, Is.EquivalentTo(new[] { "Animator", "Hero", "MissingScript", "Transform" }));
            Assert.That(index.problems, Has.Some.Contains("Assets/Prefabs/Hero.prefab has a missing script"));
            Assert.That(index.problems, Has.Some.Contains("references a missing asset (guid " + MissingGuid));
        }

        [Test]
        public void ScriptsHaveTheirAssemblyNamespaceTypesSummaryAndPublicMembers()
        {
            var index = Scan();

            var script = index.scripts.Single(s => s.path == "Assets/Game/Hero.cs");
            Assert.That(script.assembly, Is.EqualTo("Dragon.Game"));
            Assert.That(script.@namespace, Is.EqualTo("Dragon"));
            Assert.That(script.usings, Is.EqualTo(new[] { "UnityEngine", "RPGFramework.Inventory" }));
            var hero = script.types.Single(t => t.name == "Hero");
            Assert.That(hero.kind, Is.EqualTo("class"));
            Assert.That(hero.baseTypes, Is.EqualTo(new[] { "MonoBehaviour", "IHittable" }));
            Assert.That(hero.summary, Is.EqualTo("The player's hero: moves and swings a Sword."));
            Assert.That(hero.publicMembers, Is.EqualTo(new[] { "Speed", "Health", "Swing" }));
            Assert.That(script.types.Select(t => t.name), Is.EquivalentTo(new[] { "Hero", "Stance", "IHittable" }));
            Assert.That(index.scripts.Single(s => s.path == "Assets/Data/ItemData.cs").assembly, Is.EqualTo("Assembly-CSharp"));
        }

        [Test]
        public void TheFrameworkVersionAndTheModulesTheGameUses_AreListed()
        {
            var index = Scan();

            Assert.That(index.framework.name, Is.EqualTo("com.aizekhan.rpg-framework"));
            Assert.That(index.framework.version, Is.EqualTo("0.32.0"));
            Assert.That(index.framework.modules.Select(m => m.name), Is.EquivalentTo(new[] { "ActionCombat", "Inventory", "Quests" }));
            Assert.That(index.framework.modules.Single(m => m.name == "Quests").usedBy, Is.EqualTo(new[] { "Dragon.Game" }),
                "a GUID reference is resolved through the package cache");
            Assert.That(index.assemblies.Single().references, Has.Member("RPGFramework.Quests.Runtime"));
        }

        [Test]
        public void SecretsAndExcludedFoldersAreLeftOut_AndNothingIsWritten()
        {
            var before = Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
                .ToDictionary(f => f, f => File.GetLastWriteTimeUtc(f));

            var index = Scan();

            Assert.That(index.assets.Select(a => a.path), Has.None.Contains("store.keystore"));
            Assert.That(index.assets.Select(a => a.path), Has.None.Contains("ThirdParty"));
            Assert.That(index.assets.Select(a => a.path), Has.None.StartsWith("Library"));
            var after = Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
                .ToDictionary(f => f, f => File.GetLastWriteTimeUtc(f));
            Assert.That(after, Is.EquivalentTo(before));
            Assert.That(index.coverage.Single(c => c.kind == "Script").indexed, Is.EqualTo(2));
        }

        [Test]
        public void ExportWritesJsonThatReadsBack()
        {
            var output = Path.Combine(_root, "Library/RPGFramework/ProjectIndex.json");

            ProjectIndexer.Export(_root, output);
            var read = UnityEngine.JsonUtility.FromJson<ProjectIndex>(File.ReadAllText(output));

            Assert.That(read.schema, Is.EqualTo(ProjectIndex.Schema));
            Assert.That(read.scripts.Count, Is.EqualTo(2));
            Assert.That(File.ReadAllText(output), Does.Contain("\"namespace\": \"Dragon\""));
        }
    }
}
