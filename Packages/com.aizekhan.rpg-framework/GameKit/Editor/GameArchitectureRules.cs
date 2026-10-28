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
    /// <c>// architecture: allow GA05 (reason)</c>; GA13 also accepts <c>// framework-ok: reason</c>. GA14 accepts only a
    /// recorded framework gap (<c>// framework-gap: id</c>).
    /// </summary>
    public static class GameArchitectureRules
    {
        public const string Guide = "Docs/RPGFramework/architecture.md";

        /// <summary>
        /// Where a game records what it needed from the framework and did not find (project root).
        /// </summary>
        public const string GapsFile = "FRAMEWORK-GAPS.md";

        /// <summary>
        /// The recipe that maps each everyday need to the framework service for it (GA13 points here).
        /// </summary>
        public const string CoreServicesRecipe = "Docs/RPGFramework/recipes/14-core-services.md";

        /// <summary>
        /// The recipe that maps each game need (a chest, a shop, a perk, poison) to the gameplay module for it (GA14).
        /// </summary>
        public const string ModulesRecipe = "Docs/RPGFramework/recipes/16-game-needs-to-modules.md";

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
                ("GA12", "Framework gaps are recorded", FrameworkGapsAreRecorded),
                ("GA13", "The game uses the framework's core services", GameUsesCoreServices),
                ("GA14", "The game's rules use the framework's gameplay modules", GameUsesGameplayModules)
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

        // A Unity object call: "Object.Instantiate", "GameObject.Destroy" or the bare inherited one, not "service.Instantiate"
        private const string UnityObjectCall = @"(?:\b(?:UnityEngine\.)?(?:Object|GameObject)\.|(?<![\w.]))";

        private static readonly (Regex Pattern, string Found, string Use)[] CoreServiceBypasses =
        {
            (new Regex(@"\bResources\.Load(All|Async)?\b"), "Resources.Load",
                "IAssetManager.LoadAsync<T>(key) and Dispose the handle when done (Resources, Addressables and test providers behind one API)"),
            (new Regex(@"\bSceneManager\.(LoadScene|LoadSceneAsync|UnloadSceneAsync)\b"), "SceneManager scene loading",
                "ISceneDispatcher.LoadAsync / LoadAdditiveAsync / UnloadAsync (queued, transition hooks, scene events on the bus)"),
            (new Regex(@"\bDebug\.Log\w*\s*\("), "Debug.Log",
                "the game's ILogger (Host.Logger, or an ILogger taken in the constructor): logger.LogInfo(message, category)"),
            (new Regex(@"(?:(?<![\w.])|\bthis\.)(Invoke|InvokeRepeating)\s*\(\s*(nameof\s*\(|"")"), "MonoBehaviour.Invoke / InvokeRepeating",
                "ITimerService.Schedule(seconds, action) / Repeat(interval, action); keep the TimerHandle and Cancel it"),
            (new Regex(@"\bnew\s+WaitForSeconds(Realtime)?\s*\("), "a coroutine waiting with WaitForSeconds",
                "ITimerService.Schedule / Repeat, or await timers.DelayAsync(seconds, token) (game time, pausable, cancellable)"),
            (new Regex(UnityObjectCall + @"Instantiate\s*(<[^>()]*>)?\s*\("), "Instantiate",
                "IPrefabSpawner.SpawnAsync(key, position, rotation) with ConfigurePool(key) for things that come and go; " +
                "IAssetManager.InstantiateAsync for one-off objects"),
            (new Regex(@"\b(Stack|Queue)\s*<\s*(GameObject|Transform)\s*>|\b(Stack|Queue|List|HashSet)\s*<[^>]+>\s+_?\w*[Pp]ool\w*\b"),
                "a hand-made pool", "IPrefabSpawner.ConfigurePool for prefabs, ObjectPool<T> (registered in IPoolManager) for plain objects"),
        };

        private static readonly Regex DestroyCall = new Regex(UnityObjectCall + @"Destroy\s*\(");
        private static readonly Regex DestroyDeclaration = new Regex(@"\bvoid\s+Destroy\s*\(");
        private static readonly Regex FrameworkOk = new Regex(@"framework-ok:\s*\S");

        /// <summary>
        /// Player code that does by hand what a framework service does: loading, scenes, logging, delayed calls,
        /// creating and destroying objects, pools. A line with a real reason to keep the Unity call says so with
        /// <c>// framework-ok: reason</c>.
        /// </summary>
        public static IEnumerable<ArchitectureViolation> GameUsesCoreServices(GameCodebase codebase)
        {
            foreach (var assembly in codebase.Assemblies.Where(a => a.IsPlayerCode))
            {
                foreach (var file in assembly.SourceFiles)
                {
                    foreach (var (code, number) in CodeLines(file, "GA13", skipEditorBlocks: true, allowLine: FrameworkOk))
                    {
                        string line = StripStrings(code);
                        foreach (var (pattern, found, use) in CoreServiceBypasses)
                        {
                            if (pattern.IsMatch(line))
                                yield return CoreServiceViolation(file, number, found, use);
                        }

                        var destroy = DestroyCall.Match(line);
                        // Removing a component (a primitive's collider) is not despawning an object
                        if (destroy.Success && !DestroyDeclaration.IsMatch(line) && !line.Substring(destroy.Index).Contains("GetComponent"))
                        {
                            yield return CoreServiceViolation(file, number, "Destroy",
                                "IPrefabSpawner.Despawn(instance) for what the spawner made (pooled ones go back to their pool)");
                        }
                    }
                }
            }
        }

        // The delta a per-frame update counts down with: "x -= dt;", "_left[id] -= deltaTime)"
        private const string CountDown = @"(\s*\[[^\]]*\])?\s*-=\s*(dt|deltaTime|delta|deltaSeconds|elapsed|elapsedSeconds)\s*[;)]";

        private static readonly Regex UsesRandom = new Regex(@"\.Next(Double|Single|Float)?\s*\(|\bRandom\.(Range|value)\b");

        // Checked in order; a line reports the first that matches. Weighted picks only count in files that roll dice.
        private static readonly (Regex Pattern, bool NeedsRandom, string Found, string Use)[] ModuleBypasses =
        {
            (new Regex(@"\.Next(Double|Single|Float)?\s*\((?:[^;()]|\([^;()]*\))*(gold|coin|money|currenc|cash|gem|silver)|\.Next(Double|Single|Float)?\s*\(\s*\)\s*\*[^;<>=]*(gold|coin|money|currenc|cash|gem|silver)",
                    RegexOptions.IgnoreCase), false,
                "a random amount of currency",
                "a loot table entry for the currency with its quantity range (Loot Table asset or LootTableBuilder; " +
                "ILootGenerator rolls it, the action RPG kit pays it into ICurrencyWallet)"),
            (new Regex(@"\bWeighted\w*\.(Pick|Roll|Choose|Select|Random)\w*\s*\(|(roll|total|sum|cumulative|acc|pick)\w*\s*(\+=|-=)\s*[\w.\[\]()]*weight\b|\.Next(Double|Single|Float)?\s*\((?:[^;()]|\([^;()]*\))*weight|\.Next(Double|Single|Float)?\s*\(\s*\)\s*\*[^;<>=]*weight",
                    RegexOptions.IgnoreCase), true,
                "a hand-made weighted pick",
                "an ILootTable (Loot Table asset or LootTableBuilder) rolled by ILootGenerator: weights, chances, " +
                "quantity ranges, level limits and a seed"),
            (new Regex(@"\w*(gold|coin|money|currenc|cash|gem|silver)\w*\s*-=", RegexOptions.IgnoreCase), false,
                "currency taken by hand",
                "ICurrencyWallet.TrySpend (TradingModule) for any price; a shop is a trader whose offers ITradingManager.Trade " +
                "runs (checks money and items, pays, moves the items)"),
            (new Regex(@"\w*cooldown\w*" + CountDown + @"|\b(Dictionary|SortedDictionary)\s*<[^>]+>\s+_?\w*cooldown", RegexOptions.IgnoreCase), false,
                "a hand-made cooldown",
                "an ability (Ability asset or AbilityMetadata) used through ActorAbilities.Use / IAbilityExecutor: cooldowns, " +
                "charges (MaxCharges), cast times, costs and co-op sync; the game's own action goes in ActorAbilities.SetEffect"),
            (new Regex(@"\w*(poison|burn|bleed|stun|freez|frozen|slow|haste|shield|invuln|buff|debuff|effect|status)\w*" + CountDown,
                    RegexOptions.IgnoreCase), false,
                "a hand-made status effect timer",
                "a status effect (Status Effect asset) applied with ActorEffects.Apply / IStatusEffectManager: duration, " +
                "ticks, stacks, stat modifiers and controls"),
            (new Regex(@"\w+" + CountDown), false,
                "a timer counted down by hand",
                "ITimerService.Schedule(seconds, action) / Repeat(interval, action) (game time, pausable, cancellable); " +
                "a cooldown or an effect duration belongs to Abilities or StatusEffects"),
        };

        /// <summary>
        /// Rules code that does by hand what a gameplay module does: weighted picks and random currency (Loot),
        /// prices taken from a balance (wallet and Trading), cooldowns (Abilities), effect timers (StatusEffects),
        /// countdowns (timers). These are heuristics, so the only way past one is a recorded gap: a
        /// <c>// framework-gap: &lt;id&gt;</c> marker on the line, the line above it, or above the type it is in
        /// (GA12 then requires the section in FRAMEWORK-GAPS.md).
        /// </summary>
        public static IEnumerable<ArchitectureViolation> GameUsesGameplayModules(GameCodebase codebase)
        {
            foreach (var assembly in codebase.OfKind(GameAssemblyKind.Rules))
            {
                foreach (var file in assembly.SourceFiles.Where(File.Exists))
                {
                    var raw = File.ReadAllLines(file);
                    bool MarkedAt(int number) => number >= 1 && number <= raw.Length && GapMarker.IsMatch(raw[number - 1]);
                    bool rollsDice = raw.Any(line => UsesRandom.IsMatch(StripStrings(line)));
                    bool typeMarked = false;
                    foreach (var (code, number) in CodeLines(file, "GA14", allowOptOut: false))
                    {
                        if (Declaration.IsMatch(code))
                        {
                            // The marker sits above the type, possibly above its doc comment and attributes
                            int above = number - 1;
                            while (above >= 1 && AboveDeclaration(raw[above - 1]))
                                above--;
                            typeMarked = MarkedAt(above) || MarkedAt(above - 1);
                        }
                        if (typeMarked || MarkedAt(number) || MarkedAt(number - 1))
                            continue;

                        string line = StripStrings(code);
                        foreach (var (pattern, needsRandom, found, use) in ModuleBypasses)
                        {
                            if ((needsRandom && !rollsDice) || !pattern.IsMatch(line))
                                continue;
                            yield return new ArchitectureViolation("GA14", $"{file}:{number}", $"{found}, which a framework module does",
                                $"Use {use}. {ModulesRecipe} maps each need to its module. If the module cannot do what this game " +
                                $"needs, record the gap in {GapsFile} and mark the stand-in // framework-gap: <id> (no other opt-out).");
                            break;
                        }
                    }
                }
            }
        }

        // Lines between a type and the marker above it: attributes, its doc comment, blank lines
        private static bool AboveDeclaration(string line)
        {
            string trimmed = line.Trim();
            return trimmed.Length == 0 || trimmed.StartsWith("[", StringComparison.Ordinal)
                   || trimmed.StartsWith("///", StringComparison.Ordinal);
        }

        private static ArchitectureViolation CoreServiceViolation(string file, int number, string found, string use) =>
            new ArchitectureViolation("GA13", $"{file}:{number}", $"uses {found}, which a framework service does",
                $"Use {use}. The services come from the host's container ({CoreServicesRecipe}). If this line really needs " +
                "the Unity call, end it with // framework-ok: <reason>.");

        /// <summary>
        /// The line with the text of string and char literals removed (the quotes stay), so words in messages do not match.
        /// </summary>
        private static string StripStrings(string line)
        {
            var result = new System.Text.StringBuilder(line.Length);
            char quote = '\0';
            bool verbatim = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quote == '\0')
                {
                    if (c == '"' || c == '\'')
                    {
                        quote = c;
                        verbatim = c == '"' && i > 0 && (line[i - 1] == '@' || (line[i - 1] == '$' && i > 1 && line[i - 2] == '@'));
                    }
                    result.Append(c);
                    continue;
                }
                if (!verbatim && c == '\\')
                {
                    i++;
                    continue;
                }
                if (c != quote)
                    continue;
                if (verbatim && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                quote = '\0';
                result.Append(c);
            }
            return result.ToString();
        }


        /// <summary>
        /// Code lines of a C# file with their line numbers: comments removed, lines that allow this rule skipped,
        /// and optionally lines inside <c>#if UNITY_EDITOR</c> blocks and lines matching <paramref name="allowLine"/> skipped.
        /// </summary>
        internal static IEnumerable<(string Line, int Number)> CodeLines(string file, string rule, bool skipEditorBlocks = false,
            Regex allowLine = null, bool allowOptOut = true)
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
                if ((allowOptOut && raw.Contains("architecture: allow " + rule)) || (allowLine != null && allowLine.IsMatch(raw)))
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
