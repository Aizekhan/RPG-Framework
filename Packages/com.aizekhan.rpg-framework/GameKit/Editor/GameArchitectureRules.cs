using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using RPGFramework.Events;
using UnityEngine;

namespace RPGFramework.GameKit.Editor
{
    /// <summary>
    /// A broken architecture rule, written for whoever fixes it (a person or an AI): what is wrong, where,
    /// and how to fix it.
    /// </summary>
    public sealed class ArchitectureViolation
    {
        public ArchitectureViolation(string rule, string where, string problem, string fix)
        {
            Rule = rule;
            Where = where;
            Problem = problem;
            Fix = fix;
        }

        /// <summary>
        /// Rule id (GA01…), the heading of the rule in the framework's architecture guide.
        /// </summary>
        public string Rule { get; }

        public string Where { get; }
        public string Problem { get; }
        public string Fix { get; }

        public override string ToString() => $"[{Rule}] {Where}: {Problem}\n    Fix: {Fix}";
    }

    /// <summary>
    /// The architecture rules of a game built on the framework. Each rule returns its violations; an empty result
    /// means the rule holds. A source line can opt out of a source rule with a comment naming it:
    /// <c>// architecture: allow GA05 (reason)</c>.
    /// </summary>
    public static class GameArchitectureRules
    {
        public const string Guide = "Docs/RPGFramework/architecture.md";

        /// <summary>
        /// Where a game records what it needed from the framework and did not find (project root).
        /// </summary>
        public const string GapsFile = "FRAMEWORK-GAPS.md";

        public static readonly IReadOnlyList<(string Id, string Title, Func<GameCodebase, IEnumerable<ArchitectureViolation>> Check)> All =
            new (string, string, Func<GameCodebase, IEnumerable<ArchitectureViolation>>)[]
            {
                ("GA01", "Game code lives in assembly definitions", CodeIsInAssemblyDefinitions),
                ("GA02", "The game has a rules assembly", RulesAssemblyExists),
                ("GA03", "Rules do not use UnityEngine", RulesDoNotUseUnityEngine),
                ("GA04", "Rules do not depend on views, editor code or tests", RulesDoNotDependOnOtherGameCode),
                ("GA05", "No scene searches or messages by name", NoSceneSearches),
                ("GA06", "No singletons", NoSingletons),
                ("GA07", "Events are immutable structs", EventsAreImmutableStructs),
                ("GA08", "UnityEditor only in editor code", UnityEditorOnlyInEditorCode),
                ("GA09", "Unity objects live in files of their own name", UnityObjectsHaveMatchingFiles),
                ("GA10", "Every rules and runtime assembly has tests", AssembliesAreTested),
                ("GA11", "The game does not redo the framework", GameDoesNotRedoTheFramework),
                ("GA12", "Framework gaps are recorded", FrameworkGapsAreRecorded)
            };

        /// <summary>
        /// Runs every rule.
        /// </summary>
        public static List<ArchitectureViolation> CheckAll(GameCodebase codebase) =>
            All.SelectMany(rule => rule.Check(codebase)).ToList();

        public static IEnumerable<ArchitectureViolation> CodeIsInAssemblyDefinitions(GameCodebase codebase)
        {
            foreach (var assembly in codebase.Assemblies.Where(a => !a.HasAssemblyDefinition))
            {
                yield return new ArchitectureViolation("GA01", assembly.Name,
                    $"{assembly.SourceFiles.Count} script(s) under {codebase.Root} compile into {assembly.Name}",
                    "Put every script in a folder with an .asmdef (rules: <Game>.Core, views: <Game>, editor: <Game>.Editor, " +
                    "tests: <Game>.Tests). The RPG Framework > Game Kit > New Game generator creates them.");
            }
        }

        public static IEnumerable<ArchitectureViolation> RulesAssemblyExists(GameCodebase codebase)
        {
            if (codebase.OfKind(GameAssemblyKind.Rules).Any())
                yield break;
            yield return new ArchitectureViolation("GA02", codebase.Root,
                $"no rules assembly (an .asmdef named *{GameCodebase.DefaultRulesSuffix})",
                "Keep game rules (state, decisions, events) in <Game>.Core with \"noEngineReferences\": true; " +
                "views and input stay in the <Game> assembly and call the rules.");
        }

        public static IEnumerable<ArchitectureViolation> RulesDoNotUseUnityEngine(GameCodebase codebase)
        {
            foreach (var rules in codebase.OfKind(GameAssemblyKind.Rules).Where(a => !a.NoEngineReferences))
            {
                yield return new ArchitectureViolation("GA03", rules.Name,
                    "rules assembly can use UnityEngine (\"noEngineReferences\" is false)",
                    "Set \"noEngineReferences\": true in its .asmdef. Move code that needs Unity (MonoBehaviours, " +
                    "Vector3, Time, Debug) to the runtime assembly; pass values in from there.");
            }
        }

        public static IEnumerable<ArchitectureViolation> RulesDoNotDependOnOtherGameCode(GameCodebase codebase)
        {
            var nonRules = new HashSet<string>(codebase.Assemblies.Where(a => a.Kind != GameAssemblyKind.Rules).Select(a => a.Name));
            foreach (var rules in codebase.OfKind(GameAssemblyKind.Rules))
            {
                foreach (var reference in rules.References.Where(nonRules.Contains))
                {
                    yield return new ArchitectureViolation("GA04", rules.Name, $"references {reference}",
                        "Rules must not know about views, editor tools or tests. Reverse the dependency: publish an " +
                        "event or expose an interface in the rules and let the other assembly react to it.");
                }
            }
        }

        private static readonly (Regex Pattern, string Api)[] SceneSearches =
        {
            (new Regex(@"\bFind(First|Any)?Object(s)?(Of|By)Type\b"), "FindObjectOfType / FindFirstObjectByType"),
            (new Regex(@"\bGameObject\.Find(WithTag|GameObjectsWithTag)?\s*\("), "GameObject.Find"),
            (new Regex(@"\b(SendMessage|BroadcastMessage|SendMessageUpwards)\s*\("), "SendMessage"),
        };

        public static IEnumerable<ArchitectureViolation> NoSceneSearches(GameCodebase codebase)
        {
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode))
            {
                foreach (var file in assembly.SourceFiles)
                {
                    foreach (var (line, number) in CodeLines(file, "GA05"))
                    {
                        foreach (var (pattern, api) in SceneSearches)
                        {
                            if (!pattern.IsMatch(line))
                                continue;
                            yield return new ArchitectureViolation("GA05", $"{file}:{number}", $"uses {api}",
                                "Get what you need from the composition instead: services through constructor " +
                                "injection, views through IEntityViewRegistry, scene objects through [SerializeField] " +
                                "on the host. Talk to other code through the event bus.");
                        }
                    }
                }
            }
        }

        public static IEnumerable<ArchitectureViolation> NoSingletons(GameCodebase codebase)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode))
            {
                foreach (var type in assembly.Types.Where(t => !IsCompilerGenerated(t)))
                {
                    var settable = type.GetFields(flags)
                        .Where(f => !f.IsInitOnly && !f.IsLiteral && !IsCompilerGenerated(f) && type.IsAssignableFrom(f.FieldType))
                        .Select(f => f.Name)
                        .Concat(type.GetProperties(flags)
                            .Where(p => p.SetMethod != null && type.IsAssignableFrom(p.PropertyType))
                            .Select(p => p.Name));
                    foreach (var member in settable)
                    {
                        yield return new ArchitectureViolation("GA06", $"{assembly.Name}: {type.FullName}.{member}",
                            "static instance of its own type (singleton)",
                            "Register the service in a GameFeatureModule (Container.RegisterFactory / AddSystem) and " +
                            "take it as a constructor parameter. Views get services from the host or the presenter that binds them.");
                    }
                }
            }
        }

        public static IEnumerable<ArchitectureViolation> EventsAreImmutableStructs(GameCodebase codebase)
        {
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode))
            {
                foreach (var type in assembly.Types.Where(t => typeof(IEvent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
                {
                    if (!type.IsValueType)
                    {
                        yield return new ArchitectureViolation("GA07", $"{assembly.Name}: {type.FullName}",
                            "event is a class",
                            "Declare events as `public readonly struct XxxEvent : IEvent` with get-only properties " +
                            "set in the constructor (no allocation per publish, no handler can change it for the next).");
                        continue;
                    }

                    var writable = type.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => !f.IsInitOnly).Select(f => f.Name)
                        .Concat(type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                            .Where(p => p.SetMethod != null && p.SetMethod.IsPublic && !IsInitOnly(p))
                            .Select(p => p.Name))
                        .ToList();
                    if (writable.Count > 0)
                    {
                        yield return new ArchitectureViolation("GA07", $"{assembly.Name}: {type.FullName}",
                            $"event can be changed after it is published ({string.Join(", ", writable)})",
                            "Make it a `readonly struct` with get-only properties set in the constructor.");
                    }
                }
            }
        }

        public static IEnumerable<ArchitectureViolation> UnityEditorOnlyInEditorCode(GameCodebase codebase)
        {
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode))
            {
                foreach (var file in assembly.SourceFiles)
                {
                    foreach (var (line, number) in CodeLines(file, "GA08", skipEditorBlocks: true))
                    {
                        if (line.Contains("UnityEditor"))
                        {
                            yield return new ArchitectureViolation("GA08", $"{file}:{number}",
                                "uses UnityEditor in code that ships in the player (the build will fail)",
                                "Move editor tools to the <Game>.Editor assembly (an .asmdef with includePlatforms: " +
                                "[\"Editor\"]), or wrap the lines in #if UNITY_EDITOR.");
                        }
                    }
                }
            }
        }

        public static IEnumerable<ArchitectureViolation> UnityObjectsHaveMatchingFiles(GameCodebase codebase)
        {
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode))
            {
                var files = new HashSet<string>(assembly.SourceFiles.Select(Path.GetFileNameWithoutExtension));
                foreach (var type in assembly.Types)
                {
                    if (type.IsAbstract || type.IsNested || type.IsGenericTypeDefinition ||
                        !(typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(ScriptableObject).IsAssignableFrom(type)))
                        continue;
                    if (files.Contains(type.Name))
                        continue;
                    yield return new ArchitectureViolation("GA09", $"{assembly.Name}: {type.FullName}",
                        $"no file named {type.Name}.cs, so Unity cannot add it to a GameObject or create the asset",
                        $"Move {type.Name} into its own file {type.Name}.cs.");
                }
            }
        }

        public static IEnumerable<ArchitectureViolation> AssembliesAreTested(GameCodebase codebase)
        {
            var tested = new HashSet<string>(codebase.OfKind(GameAssemblyKind.Tests).SelectMany(t => t.References));
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode && a.HasAssemblyDefinition))
            {
                if (tested.Contains(assembly.Name))
                    continue;
                yield return new ArchitectureViolation("GA10", assembly.Name, "no test assembly references it",
                    $"Add {assembly.Name} to the references of the game's test .asmdef and write tests for it " +
                    "(rules: boot GameHost with the game's composition; views: PlayMode tests).");
            }
        }

        private static readonly Regex Declaration = new Regex(@"\b(class|struct|interface|enum|record)\s+([A-Z]\w*)");
        private static readonly Regex FrameworkNamespace = new Regex(@"^namespace\s+RPGFramework\b");
        private static HashSet<string> _frameworkTypeNames;

        public static IEnumerable<ArchitectureViolation> GameDoesNotRedoTheFramework(GameCodebase codebase) =>
            GameDoesNotRedoTheFramework(codebase, FrameworkTypeNames());

        /// <param name="frameworkTypes">Names of the framework's public types.</param>
        public static IEnumerable<ArchitectureViolation> GameDoesNotRedoTheFramework(GameCodebase codebase,
            ICollection<string> frameworkTypes)
        {
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode))
            {
                foreach (var file in assembly.SourceFiles)
                {
                    foreach (var (line, number) in CodeLines(file, "GA11"))
                    {
                        if (FrameworkNamespace.IsMatch(line))
                        {
                            yield return new ArchitectureViolation("GA11", $"{file}:{number}", "game code in the RPGFramework namespace",
                                "Use the game's own namespace. A change the framework needs belongs in the framework package, " +
                                $"not in a copy inside the game: describe it in {GapsFile} (GA12).");
                        }
                        foreach (Match declaration in Declaration.Matches(line))
                        {
                            string name = declaration.Groups[2].Value;
                            if (!frameworkTypes.Contains(name))
                                continue;
                            yield return new ArchitectureViolation("GA11", $"{file}:{number}",
                                $"declares {name}, which the framework already has",
                                $"Use the framework's {name} (README lists the modules; recipe 07). If it lacks something this game " +
                                $"needs, do not rebuild it: record the gap in {GapsFile} and keep a stand-in marked " +
                                "// framework-gap: <id> (GA12). If it is a different, game-specific thing, give it a name that says so.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Public top-level type names of the framework's runtime assemblies (not its tests or editor tools).
        /// </summary>
        public static HashSet<string> FrameworkTypeNames()
        {
            if (_frameworkTypeNames != null)
                return _frameworkTypeNames;
            var names = new HashSet<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (!name.StartsWith("RPGFramework.", StringComparison.Ordinal) || name.Contains(".Tests") ||
                    name.Contains("TestSupport") || name.Contains(".Editor") || name.Contains(".GameKit") || assembly.IsDynamic)
                    continue;
                Type[] types;
                try
                {
                    types = assembly.GetExportedTypes();
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (var type in types.Where(t => !t.IsNested))
                {
                    int tick = type.Name.IndexOf('`');
                    names.Add(tick < 0 ? type.Name : type.Name.Substring(0, tick));
                }
            }
            return _frameworkTypeNames = names;
        }

        private static readonly Regex GapMarker = new Regex(@"framework-gap:\s*([A-Za-z0-9][\w-]*)");
        private static readonly Regex GapHeading = new Regex(@"^#+\s*([A-Za-z0-9][\w-]*)");

        public static IEnumerable<ArchitectureViolation> FrameworkGapsAreRecorded(GameCodebase codebase) =>
            FrameworkGapsAreRecorded(codebase, GapsFile);

        /// <param name="gapsFile">The gaps file to check against.</param>
        public static IEnumerable<ArchitectureViolation> FrameworkGapsAreRecorded(GameCodebase codebase, string gapsFile)
        {
            var recorded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(gapsFile))
            {
                foreach (var line in File.ReadAllLines(gapsFile))
                {
                    var heading = GapHeading.Match(line.Trim());
                    if (heading.Success)
                        recorded.Add(heading.Groups[1].Value);
                }
            }
            foreach (var assembly in codebase.Assemblies)
            {
                foreach (var file in assembly.SourceFiles.Where(File.Exists))
                {
                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var marker = GapMarker.Match(lines[i]);
                        if (!marker.Success || recorded.Contains(marker.Groups[1].Value))
                            continue;
                        string id = marker.Groups[1].Value;
                        yield return new ArchitectureViolation("GA12", $"{file}:{i + 1}",
                            $"stand-in for framework gap '{id}', which {GapsFile} does not describe",
                            $"Add a section \"## {id}\" to {GapsFile} (project root): what the game needed, what the framework " +
                            "lacks, the API you would want there, and where the stand-in lives. The list is how the framework " +
                            "learns what games need.");
                    }
                }
            }
        }

        /// <summary>
        /// Code lines of a C# file with their line numbers: comments removed, lines that allow this rule skipped,
        /// and optionally lines inside <c>#if UNITY_EDITOR</c> blocks skipped.
        /// </summary>
        internal static IEnumerable<(string Line, int Number)> CodeLines(string file, string rule, bool skipEditorBlocks = false)
        {
            if (!File.Exists(file))
                yield break;

            var lines = File.ReadAllLines(file);
            var editorBlocks = new Stack<bool>();
            bool inBlockComment = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();

                if (line.StartsWith("#if", StringComparison.Ordinal))
                {
                    editorBlocks.Push(IsEditorCondition(line));
                    continue;
                }
                if (line.StartsWith("#elif", StringComparison.Ordinal) || line.StartsWith("#else", StringComparison.Ordinal))
                {
                    if (editorBlocks.Count > 0)
                    {
                        bool wasEditor = editorBlocks.Pop();
                        editorBlocks.Push(!wasEditor && line.StartsWith("#elif", StringComparison.Ordinal) && IsEditorCondition(line));
                    }
                    continue;
                }
                if (line.StartsWith("#endif", StringComparison.Ordinal))
                {
                    if (editorBlocks.Count > 0)
                        editorBlocks.Pop();
                    continue;
                }
                if (skipEditorBlocks && editorBlocks.Contains(true))
                    continue;
                if (raw.Contains("architecture: allow " + rule))
                    continue;

                line = StripComments(line, ref inBlockComment);
                if (line.Length > 0)
                    yield return (line, i + 1);
            }
        }

        private static bool IsEditorCondition(string directive) =>
            directive.Contains("UNITY_EDITOR") && !directive.Contains("!UNITY_EDITOR");

        private static string StripComments(string line, ref bool inBlockComment)
        {
            var result = new System.Text.StringBuilder(line.Length);
            for (int i = 0; i < line.Length; i++)
            {
                if (inBlockComment)
                {
                    if (i + 1 < line.Length && line[i] == '*' && line[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }
                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
                    break;
                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }
                result.Append(line[i]);
            }
            return result.ToString().Trim();
        }

        private static bool IsCompilerGenerated(MemberInfo member) =>
            member.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) || member.Name.Contains("<");

        private static bool IsInitOnly(PropertyInfo property) =>
            property.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
                .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }
}
