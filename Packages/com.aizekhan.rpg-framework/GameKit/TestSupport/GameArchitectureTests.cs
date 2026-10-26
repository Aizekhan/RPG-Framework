using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.GameKit.Editor;

namespace RPGFramework.GameKit.TestSupport
{
    /// <summary>
    /// The framework's architecture rules as tests for a game. Derive once in the game's test assembly and name the
    /// game folder; every rule becomes a test, so a change that breaks the architecture fails the build:
    /// <code>
    /// public sealed class MyGameArchitectureTests : GameArchitectureTests
    /// {
    ///     protected override string GameRoot => "Assets/MyGame";
    /// }
    /// </code>
    /// The test asmdef references <c>RPGFramework.GameKit.TestSupport</c> and <c>RPGFramework.GameKit.Editor</c>.
    /// </summary>
    [Category("Architecture")]
    public abstract class GameArchitectureTests
    {
        private GameCodebase _codebase;

        /// <summary>
        /// Project-relative folder holding all of the game's code, e.g. <c>Assets/MyGame</c>.
        /// </summary>
        protected abstract string GameRoot { get; }

        /// <summary>
        /// Assemblies whose name ends with this hold the game rules.
        /// </summary>
        protected virtual string RulesAssemblySuffix => GameCodebase.DefaultRulesSuffix;

        [OneTimeSetUp]
        public void LoadCodebase() => _codebase = GameCodebase.Load(GameRoot, RulesAssemblySuffix);

        [Test]
        public void GA00_GameCodeIsFound() =>
            Assert.That(_codebase.Assemblies, Is.Not.Empty, $"No compiled scripts under {GameRoot}; is GameRoot right?");

        [Test]
        public void GA01_CodeIsInAssemblyDefinitions() => Check(GameArchitectureRules.CodeIsInAssemblyDefinitions);

        [Test]
        public void GA02_RulesAssemblyExists() => Check(GameArchitectureRules.RulesAssemblyExists);

        [Test]
        public void GA03_RulesDoNotUseUnityEngine() => Check(GameArchitectureRules.RulesDoNotUseUnityEngine);

        [Test]
        public void GA04_RulesDoNotDependOnOtherGameCode() => Check(GameArchitectureRules.RulesDoNotDependOnOtherGameCode);

        [Test]
        public void GA05_NoSceneSearches() => Check(GameArchitectureRules.NoSceneSearches);

        [Test]
        public void GA06_NoSingletons() => Check(GameArchitectureRules.NoSingletons);

        [Test]
        public void GA07_EventsAreImmutableStructs() => Check(GameArchitectureRules.EventsAreImmutableStructs);

        [Test]
        public void GA08_UnityEditorOnlyInEditorCode() => Check(GameArchitectureRules.UnityEditorOnlyInEditorCode);

        [Test]
        public void GA09_UnityObjectsHaveMatchingFiles() => Check(GameArchitectureRules.UnityObjectsHaveMatchingFiles);

        [Test]
        public void GA10_AssembliesAreTested() => Check(GameArchitectureRules.AssembliesAreTested);

        [Test]
        public void GA11_GameDoesNotRedoTheFramework() => Check(GameArchitectureRules.GameDoesNotRedoTheFramework);

        [Test]
        public void GA12_FrameworkGapsAreRecorded() => Check(GameArchitectureRules.FrameworkGapsAreRecorded);

        private void Check(Func<GameCodebase, IEnumerable<ArchitectureViolation>> rule)
        {
            var violations = rule(_codebase).ToList();
            Assert.That(violations, Is.Empty,
                $"{violations.Count} architecture violation(s); the rules are explained in {GameArchitectureRules.Guide}\n" +
                string.Join("\n", violations));
        }
    }
}
