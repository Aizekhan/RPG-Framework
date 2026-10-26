using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace RPGFramework.GameKit.Editor
{
    public enum GameAssemblyKind
    {
        /// <summary>
        /// Game rules: plain C#, no UnityEngine (asmdef named <c>*.Core</c> by convention).
        /// </summary>
        Rules,

        /// <summary>
        /// Runtime code that may use Unity: views, input, scene entry points.
        /// </summary>
        Runtime,

        Editor,
        Tests
    }

    /// <summary>
    /// One compiled assembly of a game, as the architecture rules see it.
    /// </summary>
    public sealed class GameAssembly
    {
        public GameAssembly(string name, GameAssemblyKind kind, bool noEngineReferences, IEnumerable<string> references,
            IEnumerable<string> sourceFiles, IEnumerable<Type> types, bool hasAssemblyDefinition = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            NoEngineReferences = noEngineReferences;
            References = (references ?? Enumerable.Empty<string>()).ToList();
            SourceFiles = (sourceFiles ?? Enumerable.Empty<string>()).ToList();
            Types = (types ?? Enumerable.Empty<Type>()).ToList();
            HasAssemblyDefinition = hasAssemblyDefinition;
        }

        public string Name { get; }
        public GameAssemblyKind Kind { get; }
        public bool NoEngineReferences { get; }

        /// <summary>
        /// Names of the assemblies this one references (asmdef references).
        /// </summary>
        public IReadOnlyList<string> References { get; }

        public IReadOnlyList<string> SourceFiles { get; }

        /// <summary>
        /// Compiled types (empty when the assembly is not loaded).
        /// </summary>
        public IReadOnlyList<Type> Types { get; }

        public bool HasAssemblyDefinition { get; }

        /// <summary>
        /// Code that ships in the player: rules and runtime assemblies.
        /// </summary>
        public bool IsPlayerCode => Kind == GameAssemblyKind.Rules || Kind == GameAssemblyKind.Runtime;

        public override string ToString() => $"{Name} ({Kind})";
    }

    /// <summary>
    /// The assemblies of one game: every compiled assembly with source files under the game's root folder.
    /// </summary>
    public sealed class GameCodebase
    {
        public const string DefaultRulesSuffix = ".Core";

        public GameCodebase(string root, IEnumerable<GameAssembly> assemblies)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Assemblies = (assemblies ?? Enumerable.Empty<GameAssembly>()).ToList();
        }

        /// <summary>
        /// Project-relative folder of the game, e.g. <c>Assets/MyGame</c>.
        /// </summary>
        public string Root { get; }

        public IReadOnlyList<GameAssembly> Assemblies { get; }

        public IEnumerable<GameAssembly> OfKind(GameAssemblyKind kind) => Assemblies.Where(a => a.Kind == kind);

        public GameAssembly Find(string name) => Assemblies.FirstOrDefault(a => a.Name == name);

        /// <summary>
        /// Reads the game's assemblies from Unity's compilation pipeline.
        /// </summary>
        /// <param name="root">Project-relative game folder, e.g. <c>Assets/MyGame</c>.</param>
        /// <param name="rulesSuffix">Assemblies whose name ends with this are the game rules.</param>
        public static GameCodebase Load(string root, string rulesSuffix = DefaultRulesSuffix)
        {
            if (string.IsNullOrEmpty(root))
                throw new ArgumentException("Give the game folder, e.g. Assets/MyGame", nameof(root));

            string prefix = Normalize(root).TrimEnd('/') + "/";
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .GroupBy(a => a.GetName().Name)
                .ToDictionary(g => g.Key, g => g.First());

            var assemblies = new List<GameAssembly>();
            foreach (var compiled in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                var sources = compiled.sourceFiles.Where(f => Normalize(f).StartsWith(prefix, StringComparison.Ordinal)).ToList();
                if (sources.Count == 0)
                    continue;

                string asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(compiled.name);
                var asmdef = ReadAsmdef(asmdefPath);
                var references = compiled.assemblyReferences.Select(r => r.name).ToList();
                var kind = Classify(compiled.name, asmdef, references, rulesSuffix);
                loaded.TryGetValue(compiled.name, out var assembly);

                assemblies.Add(new GameAssembly(compiled.name, kind, asmdef?.noEngineReferences ?? false, references, sources,
                    TypesOf(assembly), hasAssemblyDefinition: asmdef != null));
            }
            return new GameCodebase(Normalize(root).TrimEnd('/'), assemblies);
        }

        private static GameAssemblyKind Classify(string name, AsmdefData asmdef, List<string> references, string rulesSuffix)
        {
            bool tests = (asmdef?.defineConstraints?.Contains("UNITY_INCLUDE_TESTS") ?? false) ||
                         references.Contains("UnityEngine.TestRunner") || references.Contains("UnityEditor.TestRunner");
            if (tests)
                return GameAssemblyKind.Tests;
            if (asmdef?.includePlatforms != null && asmdef.includePlatforms.Length == 1 && asmdef.includePlatforms[0] == "Editor")
                return GameAssemblyKind.Editor;
            if (name.EndsWith("-Editor", StringComparison.Ordinal)) // Assembly-CSharp-Editor
                return GameAssemblyKind.Editor;
            if (!string.IsNullOrEmpty(rulesSuffix) && name.EndsWith(rulesSuffix, StringComparison.Ordinal))
                return GameAssemblyKind.Rules;
            return GameAssemblyKind.Runtime;
        }

        private static AsmdefData ReadAsmdef(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                return JsonUtility.FromJson<AsmdefData>(File.ReadAllText(path));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static IEnumerable<Type> TypesOf(Assembly assembly)
        {
            if (assembly == null)
                return Enumerable.Empty<Type>();
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        internal static string Normalize(string path) => path.Replace('\\', '/');

        [Serializable]
        private sealed class AsmdefData
        {
            public string name;
            public string[] includePlatforms;
            public string[] defineConstraints;
            public bool noEngineReferences;
        }
    }
}
