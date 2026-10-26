using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using Assembly = System.Reflection.Assembly;
using UnityEngine;

namespace RPGFramework.Tests.Editor
{
    /// <summary>
    /// Rules that hold for every framework assembly, discovered by name (RPGFramework.*), so new modules are
    /// checked without editing this file.
    /// </summary>
    [Category("Architecture")]
    public sealed class FrameworkArchitectureTests
    {
        // Assemblies a framework runtime assembly may reference: the framework itself, the engine, the BCL and
        // the third-party packages the framework depends on. Anything else (a game assembly) is a violation.
        private static readonly string[] AllowedReferencePrefixes =
        {
            "RPGFramework.", "UnityEngine", "Unity.", "System", "mscorlib", "netstandard", "Microsoft.", "UniTask",
            // Editor builds of runtime assemblies see #if UNITY_EDITOR code; PlayerAssemblies_* checks it stays there
            "UnityEditor"
        };

        private static List<Assembly> FrameworkAssemblies() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("RPGFramework.", StringComparison.Ordinal))
                .ToList();

        private static bool IsRuntime(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            return !name.EndsWith(".Tests", StringComparison.Ordinal) &&
                   !name.EndsWith(".TestSupport", StringComparison.Ordinal) &&
                   !name.EndsWith(".Editor", StringComparison.Ordinal) &&
                   name != "RPGFramework.Editor";
        }

        private static List<Assembly> RuntimeAssemblies() => FrameworkAssemblies().Where(IsRuntime).ToList();

        private static IEnumerable<Type> TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        [Test]
        public void RuntimeAssemblies_AreDiscovered()
        {
            Assert.That(RuntimeAssemblies().Count, Is.GreaterThan(40));
        }

        [Test]
        public void RuntimeAssemblies_ReferenceOnlyFrameworkEngineAndBcl()
        {
            var violations = (from assembly in RuntimeAssemblies()
                              from reference in assembly.GetReferencedAssemblies()
                              where !AllowedReferencePrefixes.Any(p => reference.Name.StartsWith(p, StringComparison.Ordinal))
                              select $"{assembly.GetName().Name} -> {reference.Name}").ToList();

            Assert.That(violations, Is.Empty, "Framework runtime must not depend on game code:\n" + string.Join("\n", violations));
        }

        /// <summary>
        /// Editor-built assemblies always may reference UnityEditor, so this reads the sources of the player build
        /// and flags UnityEditor uses that are not inside <c>#if UNITY_EDITOR</c>: those break player builds.
        /// </summary>
        [Test]
        public void PlayerAssemblies_UseUnityEditorOnlyInsideEditorBlocks()
        {
            var violations = new List<string>();
            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (!assembly.name.StartsWith("RPGFramework.", StringComparison.Ordinal))
                    continue;
                foreach (var file in assembly.sourceFiles)
                {
                    var lines = File.ReadAllLines(file);
                    var editorBlocks = new Stack<bool>();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (line.StartsWith("#if", StringComparison.Ordinal))
                            editorBlocks.Push(line.Contains("UNITY_EDITOR") && !line.Contains("!UNITY_EDITOR"));
                        else if (line.StartsWith("#el", StringComparison.Ordinal) && editorBlocks.Count > 0)
                            editorBlocks.Push(editorBlocks.Pop() ? false : line.Contains("UNITY_EDITOR") && !line.Contains("!UNITY_EDITOR"));
                        else if (line.StartsWith("#endif", StringComparison.Ordinal) && editorBlocks.Count > 0)
                            editorBlocks.Pop();
                        else if (!line.StartsWith("//", StringComparison.Ordinal) && line.Contains("UnityEditor") &&
                                 !editorBlocks.Contains(true))
                            violations.Add($"{file}:{i + 1}: {line}");
                    }
                }
            }

            Assert.That(violations, Is.Empty, "Wrap editor-only code in #if UNITY_EDITOR:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, violations));
        }

        [Test]
        public void FrameworkAssemblies_HaveNoReferenceCycles()
        {
            var graph = FrameworkAssemblies().ToDictionary(
                a => a.GetName().Name,
                a => a.GetReferencedAssemblies().Select(r => r.Name).Where(n => n.StartsWith("RPGFramework.", StringComparison.Ordinal)).ToList());

            var state = new Dictionary<string, int>(); // 1 = on stack, 2 = done
            var cycles = new List<string>();

            void Visit(string node, Stack<string> path)
            {
                state[node] = 1;
                path.Push(node);
                if (graph.TryGetValue(node, out var edges))
                {
                    foreach (var next in edges)
                    {
                        state.TryGetValue(next, out var s);
                        if (s == 1)
                            cycles.Add(string.Join(" -> ", path.Reverse()) + " -> " + next);
                        else if (s == 0)
                            Visit(next, path);
                    }
                }
                path.Pop();
                state[node] = 2;
            }

            foreach (var node in graph.Keys)
            {
                if (!state.ContainsKey(node))
                    Visit(node, new Stack<string>());
            }

            Assert.That(cycles, Is.Empty, string.Join("\n", cycles));
        }

        [Test]
        public void PublicTypes_LiveInFrameworkNamespaces()
        {
            var violations = (from assembly in RuntimeAssemblies()
                              from type in TypesOf(assembly)
                              where type.IsPublic && (type.Namespace == null || !type.Namespace.StartsWith("RPGFramework", StringComparison.Ordinal))
                              select $"{assembly.GetName().Name}: {type.FullName}").ToList();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        /// <summary>
        /// Unity attaches a MonoBehaviour / ScriptableObject only through a MonoScript, which exists only when the
        /// class name matches its file name. A mismatch compiles but cannot be added to a prefab or asset.
        /// </summary>
        [Test]
        public void UnityObjects_HaveAMatchingScriptFile()
        {
            var scripted = new HashSet<Type>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Framework" }))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                var type = script != null ? script.GetClass() : null;
                if (type != null)
                    scripted.Add(type);
            }

            var violations = (from assembly in RuntimeAssemblies()
                              from type in TypesOf(assembly)
                              where !type.IsAbstract && !type.IsGenericTypeDefinition && !type.IsNested &&
                                    (typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(ScriptableObject).IsAssignableFrom(type))
                              where !scripted.Contains(type)
                              select type.FullName).ToList();

            Assert.That(violations, Is.Empty, "Move each class into a file named after it:\n" + string.Join("\n", violations));
        }
    }
}
