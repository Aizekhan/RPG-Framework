using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Events;
using RPGFramework.GameKit.Editor;
using UnityEngine;

namespace RPGFramework.GameKit.Tests
{
    /// <summary>
    /// Every rule catches its violation and accepts the right way, on small made-up games.
    /// </summary>
    public sealed class GameArchitectureRulesTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "GameKitRules_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        private string Source(string name, string code)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, code);
            return path;
        }

        private static GameAssembly Assembly(string name, GameAssemblyKind kind, bool noEngine = false,
            IEnumerable<string> references = null, IEnumerable<string> sources = null, IEnumerable<System.Type> types = null,
            bool asmdef = true) =>
            new GameAssembly(name, kind, noEngine, references, sources, types, asmdef);

        private static GameCodebase Game(params GameAssembly[] assemblies) => new GameCodebase("Assets/Game", assemblies);

        private static List<string> Rules(IEnumerable<ArchitectureViolation> violations) => violations.Select(v => v.Rule).ToList();

        // A well-built game: every rule holds
        private GameCodebase GoodGame() => Game(
            Assembly("Game.Core", GameAssemblyKind.Rules, noEngine: true, references: new[] { "RPGFramework.Events.Runtime" },
                types: new[] { typeof(GoodEvent), typeof(Service) }),
            Assembly("Game", GameAssemblyKind.Runtime, references: new[] { "Game.Core" },
                sources: new[] { Source("GoodView.cs", "class GoodView : MonoBehaviour { [SerializeField] Camera _camera; }") },
                types: new[] { typeof(GoodView) }),
            Assembly("Game.Editor", GameAssemblyKind.Editor, references: new[] { "Game", "Game.Core" },
                sources: new[] { Source("Tool.cs", "using UnityEditor; class Tool { void F() => Object.FindFirstObjectByType<Camera>(); }") }),
            Assembly("Game.Tests", GameAssemblyKind.Tests, references: new[] { "Game", "Game.Core" }));

        [Test]
        public void GoodGame_BreaksNoRule()
        {
            Assert.That(GameArchitectureRules.CheckAll(GoodGame()), Is.Empty);
        }

        [Test]
        public void GA01_ScriptsWithoutAsmdef()
        {
            var game = Game(Assembly("Assembly-CSharp", GameAssemblyKind.Runtime, asmdef: false, sources: new[] { "a.cs" }));
            Assert.That(Rules(GameArchitectureRules.CodeIsInAssemblyDefinitions(game)), Is.EqualTo(new[] { "GA01" }));
        }

        [Test]
        public void GA02_NoRulesAssembly()
        {
            var game = Game(Assembly("Game", GameAssemblyKind.Runtime));
            Assert.That(Rules(GameArchitectureRules.RulesAssemblyExists(game)), Is.EqualTo(new[] { "GA02" }));
        }

        [Test]
        public void GA03_RulesWithUnityEngine()
        {
            var game = Game(Assembly("Game.Core", GameAssemblyKind.Rules, noEngine: false));
            Assert.That(Rules(GameArchitectureRules.RulesDoNotUseUnityEngine(game)), Is.EqualTo(new[] { "GA03" }));
        }

        [Test]
        public void GA04_RulesReferencingViews()
        {
            var game = Game(
                Assembly("Game.Core", GameAssemblyKind.Rules, noEngine: true, references: new[] { "Game", "RPGFramework.Events.Runtime" }),
                Assembly("Game", GameAssemblyKind.Runtime, references: new[] { "Game.Core" }));
            var violations = GameArchitectureRules.RulesDoNotDependOnOtherGameCode(game).ToList();
            Assert.That(Rules(violations), Is.EqualTo(new[] { "GA04" }));
            Assert.That(violations[0].Problem, Does.Contain("Game"));
        }

        [Test]
        public void GA05_SceneSearchesInPlayerCode()
        {
            var file = Source("Bad.cs", string.Join("\n",
                "class Bad : MonoBehaviour {",
                "  void A() => FindObjectOfType<Camera>();",
                "  void B() => Object.FindFirstObjectByType<Camera>();",
                "  void C() => GameObject.Find(\"Player\");",
                "  void D() => SendMessage(\"Hit\");",
                "  // FindObjectOfType in a comment is fine",
                "  void E() => FindAnyObjectByType<Camera>(); // architecture: allow GA05 (bootstrap)",
                "}"));
            var game = Game(Assembly("Game", GameAssemblyKind.Runtime, sources: new[] { file }));

            var lines = GameArchitectureRules.NoSceneSearches(game).Select(v => v.Where.Split(':').Last()).ToList();

            Assert.That(lines, Is.EqualTo(new[] { "2", "3", "4", "5" }));
        }

        [Test]
        public void GA06_Singletons()
        {
            var game = Game(Assembly("Game", GameAssemblyKind.Runtime, types: new[] { typeof(SingletonManager), typeof(Service) }));
            var violations = GameArchitectureRules.NoSingletons(game).ToList();
            Assert.That(violations.Select(v => v.Where), Is.EqualTo(new[] { $"Game: {typeof(SingletonManager).FullName}.Instance" }));
        }

        [Test]
        public void GA07_MutableOrClassEvents()
        {
            var game = Game(Assembly("Game.Core", GameAssemblyKind.Rules, noEngine: true,
                types: new[] { typeof(GoodEvent), typeof(ClassEvent), typeof(MutableEvent) }));
            var where = GameArchitectureRules.EventsAreImmutableStructs(game).Select(v => v.Where).ToList();
            Assert.That(where, Is.EquivalentTo(new[] { $"Game.Core: {typeof(ClassEvent).FullName}", $"Game.Core: {typeof(MutableEvent).FullName}" }));
        }

        [Test]
        public void GA08_UnityEditorInPlayerCode()
        {
            var file = Source("Mixed.cs", string.Join("\n",
                "using UnityEditor;",
                "#if UNITY_EDITOR",
                "using UnityEditor.SceneManagement;",
                "#else",
                "using UnityEditor.Oops;",
                "#endif",
                "class Mixed { }"));
            var game = Game(Assembly("Game", GameAssemblyKind.Runtime, sources: new[] { file }));

            var lines = GameArchitectureRules.UnityEditorOnlyInEditorCode(game).Select(v => v.Where.Split(':').Last()).ToList();

            Assert.That(lines, Is.EqualTo(new[] { "1", "5" }));
        }

        [Test]
        public void GA09_UnityObjectInAFileOfAnotherName()
        {
            var game = Game(Assembly("Game", GameAssemblyKind.Runtime, sources: new[] { Source("Views.cs", "") },
                types: new[] { typeof(GoodView) }));
            Assert.That(Rules(GameArchitectureRules.UnityObjectsHaveMatchingFiles(game)), Is.EqualTo(new[] { "GA09" }));
        }

        [Test]
        public void GA10_UntestedAssembly()
        {
            var game = Game(
                Assembly("Game.Core", GameAssemblyKind.Rules, noEngine: true),
                Assembly("Game", GameAssemblyKind.Runtime),
                Assembly("Game.Tests", GameAssemblyKind.Tests, references: new[] { "Game.Core" }));
            var violations = GameArchitectureRules.AssembliesAreTested(game).ToList();
            Assert.That(violations.Select(v => v.Where), Is.EqualTo(new[] { "Game" }));
        }

        [Test]
        public void Violation_TellsWhatAndHowToFix()
        {
            var violation = GameArchitectureRules.RulesAssemblyExists(Game()).Single();
            Assert.That(violation.ToString(), Does.StartWith("[GA02] Assets/Game:").And.Contains("Fix:"));
        }

        [Test]
        public void EveryRule_HasAnIdAndTitle()
        {
            Assert.That(GameArchitectureRules.All.Select(r => r.Id), Is.Unique);
            Assert.That(GameArchitectureRules.All.All(r => r.Title.Length > 0 && r.Check != null), Is.True);
        }

        // Made-up game types
        private readonly struct GoodEvent : IEvent
        {
            public GoodEvent(int value) => Value = value;
            public int Value { get; }
        }

        private sealed class ClassEvent : IEvent
        {
        }

        private struct MutableEvent : IEvent
        {
            public int Value { get; set; }
        }

        private sealed class Service
        {
            public static readonly Service Empty = new Service();
        }

        private sealed class SingletonManager
        {
            public static SingletonManager Instance { get; private set; }
        }
    }

    // Top-level so GA09 sees it as a scene component
    internal sealed class GoodView : MonoBehaviour
    {
    }
}
