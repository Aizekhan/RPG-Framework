using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.GameKit.Editor
{
    /// <summary>
    /// Installs the guidance an AI assistant (or a new developer) needs to build a game the framework's way:
    /// a block in the project's <c>CLAUDE.md</c>, the architecture guide and recipes under <c>Docs/RPGFramework</c>,
    /// the game's own documents (<c>GAME.md</c>, <c>STATUS.md</c>, <c>FRAMEWORK-GAPS.md</c>, <c>DEVLOG.md</c>,
    /// <c>THIRD-PARTY-ASSETS.md</c>, <c>CREDITS.md</c>, <c>Docs/ARCHITECTURE.md</c>) and Claude Code files (<c>.claude/settings.json</c> with a new game only, the
    /// <c>/start</c> and <c>/handoff</c> commands), created once and then kept, the framework update check (a
    /// SessionStart hook, replaced on every install and added to an existing <c>.claude/settings.json</c>),
    /// a CI workflow that runs the tests when the project has none, and a <c>.gitattributes</c> that puts binary
    /// assets in Git LFS when the project has none. Reinstalling updates the framework's files and its
    /// <c>CLAUDE.md</c> block; the rest of <c>CLAUDE.md</c> is kept.
    /// </summary>
    public static class AIGuidance
    {
        public const string BlockBegin = "<!-- rpg-framework:begin";
        public const string BlockEnd = "<!-- rpg-framework:end -->";
        public const string DocsFolder = "Docs/RPGFramework";
        public const string WorkflowPath = ".github/workflows/unity-tests.yml";
        public const string WorkflowHeader = "# Installed by RPG Framework > Game Kit > Install AI Guidance";
        public const string GitAttributesPath = ".gitattributes";

        /// <summary>
        /// Claude Code's permissions for the project: installed only with a new game, never by a reinstall, because in an
        /// existing project they are the developer's decision.
        /// </summary>
        public const string ClaudeSettingsPath = ".claude/settings.json";

        /// <summary>
        /// The SessionStart hook that tells the game's agent about newer framework versions (from the release feed,
        /// <c>releases.json</c>). The framework's file: every install replaces it.
        /// </summary>
        public const string UpdateCheckPath = ".claude/hooks/framework-update-check.sh";

        /// <summary>The hook's command; carriage returns are dropped so a Windows checkout still runs.</summary>
        public const string UpdateCheckCommand =
            "tr -d '\\r' < \"$CLAUDE_PROJECT_DIR/" + UpdateCheckPath + "\" | bash";

        private const string UpdateCheckEntry =
            "{ \"matcher\": \"startup\", \"hooks\": [ { \"type\": \"command\", \"command\": \"" +
            "tr -d '\\\\r' < \\\"$CLAUDE_PROJECT_DIR/" + UpdateCheckPath + "\\\" | bash\", \"timeout\": 30 } ] }";

        public static string DocumentationRoot => Path.Combine(PackagePaths.Root, "Documentation~", "AI");

        [MenuItem("RPG Framework/Game Kit/Install AI Guidance", priority = 20)]
        public static void InstallFromMenu()
        {
            var warnings = new List<string>();
            var written = Install(DocumentationRoot, PackagePaths.ProjectRoot, warnings: warnings);
            Debug.Log("[Game Kit] AI guidance installed:\n" + string.Join("\n", written));
            foreach (var warning in warnings)
                Debug.LogWarning("[Game Kit] " + warning);
            var outdated = CommandsThatDiffer(DocumentationRoot, PackagePaths.ProjectRoot);
            if (outdated.Count > 0)
            {
                Debug.LogWarning("[Game Kit] These Claude Code commands differ from this framework version's templates " +
                                 $"(kept as they are; compare with {Path.Combine(DocumentationRoot, ProjectTemplatesFolder)} " +
                                 "and bring over what is new):\n" + string.Join("\n", outdated));
            }
        }

        public static void InstallFromCommandLine() => InstallFromMenu();

        private const string ProjectTemplatesFolder = "project";
        private const string CommandsFolder = ".claude/commands/";

        /// <summary>
        /// The game's Claude Code commands (<c>.claude/commands/*</c>) whose text differs from this framework version's
        /// templates. A reinstall never replaces them (the game may have adapted them), so it reports them instead.
        /// </summary>
        /// <returns>Project-relative paths.</returns>
        public static List<string> CommandsThatDiffer(string documentationRoot, string projectRoot)
        {
            var differ = new List<string>();
            string templates = Path.Combine(documentationRoot, ProjectTemplatesFolder, ".claude", "commands");
            if (!Directory.Exists(templates))
                return differ;
            foreach (var template in Directory.GetFiles(templates).OrderBy(f => f))
            {
                string relative = CommandsFolder + Path.GetFileName(template);
                string target = Path.Combine(projectRoot, relative);
                if (File.Exists(target) &&
                    Normalize(File.ReadAllText(target)).Trim() != Normalize(File.ReadAllText(template)).Trim())
                    differ.Add(relative);
            }
            return differ;
        }

        /// <param name="claudeSettings">Also create <see cref="ClaudeSettingsPath"/> (New Game does; a reinstall does not).</param>
        /// <param name="warnings">Receives what the developer has to do by hand (a settings file the hook could not be added to).</param>
        /// <returns>Project-relative paths written.</returns>
        public static List<string> Install(string documentationRoot, string projectRoot, bool claudeSettings = false,
            List<string> warnings = null)
        {
            if (!Directory.Exists(documentationRoot))
                throw new DirectoryNotFoundException($"Framework AI documentation not found at {documentationRoot}");

            var written = new List<string>();

            string claude = Path.Combine(projectRoot, "CLAUDE.md");
            string block = Normalize(File.ReadAllText(Path.Combine(documentationRoot, "CLAUDE.template.md"))).Trim();
            File.WriteAllText(claude, MergeBlock(File.Exists(claude) ? Normalize(File.ReadAllText(claude)) : null, block));
            written.Add("CLAUDE.md");

            string docs = Path.Combine(projectRoot, DocsFolder);
            Copy(Path.Combine(documentationRoot, "architecture.md"), Path.Combine(docs, "architecture.md"));
            written.Add($"{DocsFolder}/architecture.md");
            string recipes = Path.Combine(documentationRoot, "recipes");
            if (Directory.Exists(recipes))
            {
                foreach (var recipe in Directory.GetFiles(recipes, "*.md"))
                {
                    Copy(recipe, Path.Combine(docs, "recipes", Path.GetFileName(recipe)));
                    written.Add($"{DocsFolder}/recipes/{Path.GetFileName(recipe)}");
                }
            }

            // The game's own documents and Claude Code files, at the same paths as in the template folder: created once,
            // never overwritten (they hold the game's records and settings)
            string projectDocs = Path.Combine(documentationRoot, ProjectTemplatesFolder);
            if (Directory.Exists(projectDocs))
            {
                foreach (var template in Directory.GetFiles(projectDocs, "*", SearchOption.AllDirectories))
                {
                    string relative = template.Substring(projectDocs.Length + 1).Replace('\\', '/');
                    string target = Path.Combine(projectRoot, relative);
                    if (File.Exists(target) || (!claudeSettings && relative == ClaudeSettingsPath))
                        continue;
                    Copy(template, target);
                    written.Add(relative);
                }
            }

            // The update check: the script is the framework's, the hook line goes into the game's settings once
            string updateCheck = Path.Combine(documentationRoot, "hooks", Path.GetFileName(UpdateCheckPath));
            if (File.Exists(updateCheck))
            {
                Copy(updateCheck, Path.Combine(projectRoot, UpdateCheckPath));
                written.Add(UpdateCheckPath);
                string settings = Path.Combine(projectRoot, ClaudeSettingsPath);
                string current = File.Exists(settings) ? Normalize(File.ReadAllText(settings)) : "{\n}\n";
                string merged = WithUpdateCheckHook(current);
                if (merged == null)
                {
                    warnings?.Add($"{ClaudeSettingsPath} could not be read as a JSON object; add the framework update " +
                                  $"check by hand: a SessionStart hook running {UpdateCheckCommand}");
                }
                else if (merged != current)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(settings));
                    File.WriteAllText(settings, merged);
                    written.Add(ClaudeSettingsPath + " (framework update check hook)");
                }
            }

            string workflow = Path.Combine(projectRoot, WorkflowPath);
            string workflowTemplate = Path.Combine(documentationRoot, "ci", "unity-tests.yml");
            if (File.Exists(workflowTemplate) && (!File.Exists(workflow) || IsGreenWhenSkippedWorkflow(File.ReadAllText(workflow))))
            {
                Copy(workflowTemplate, workflow);
                written.Add(WorkflowPath);
            }

            string attributes = Path.Combine(projectRoot, GitAttributesPath);
            string attributesTemplate = Path.Combine(documentationRoot, "git", "gitattributes");
            if (!File.Exists(attributes) && File.Exists(attributesTemplate))
            {
                Copy(attributesTemplate, attributes);
                written.Add(GitAttributesPath);
            }
            return written;
        }

        /// <summary>
        /// The workflow installed before 0.5.2 passed when the licence secrets were missing, without running a test.
        /// It is replaced on reinstall; a workflow the game wrote or changed is kept.
        /// </summary>
        public static bool IsGreenWhenSkippedWorkflow(string workflow) =>
            workflow.StartsWith(WorkflowHeader, StringComparison.Ordinal) &&
            workflow.Contains("echo \"available=false\" >> \"$GITHUB_OUTPUT\"");

        /// <summary>
        /// Claude Code settings with the framework's SessionStart update check added: unchanged when it is already
        /// there, null when the text is not a JSON object whose <c>hooks</c> and <c>SessionStart</c> have the expected
        /// shape. Everything else in the file is kept as written.
        /// </summary>
        public static string WithUpdateCheckHook(string settings)
        {
            if (settings.Contains(Path.GetFileName(UpdateCheckPath)))
                return settings;
            int root = SkipSpace(settings, 0);
            if (root >= settings.Length || settings[root] != '{' || !IsBalanced(settings, root))
                return null;
            int hooks = ValueOf(settings, root, "hooks");
            if (hooks < 0)
                return InsertMember(settings, root, "\"hooks\": { \"SessionStart\": [ " + UpdateCheckEntry + " ] }");
            if (settings[hooks] != '{')
                return null;
            int sessionStart = ValueOf(settings, hooks, "SessionStart");
            if (sessionStart < 0)
                return InsertMember(settings, hooks, "\"SessionStart\": [ " + UpdateCheckEntry + " ]");
            return settings[sessionStart] == '[' ? InsertMember(settings, sessionStart, UpdateCheckEntry) : null;
        }

        // Where the value of `key` starts in the object opening at `open`, or -1
        private static int ValueOf(string json, int open, string key)
        {
            int depth = 0;
            for (int i = open; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int end = StringEnd(json, i);
                    if (end < 0)
                        return -1;
                    int colon = SkipSpace(json, end + 1);
                    if (depth == 1 && colon < json.Length && json[colon] == ':' && end - i - 1 == key.Length &&
                        string.CompareOrdinal(json, i + 1, key, 0, key.Length) == 0)
                        return SkipSpace(json, colon + 1);
                    i = end;
                }
                else if (c == '{' || c == '[')
                    depth++;
                else if ((c == '}' || c == ']') && --depth == 0)
                    return -1;
            }
            return -1;
        }

        // The member goes first, after the bracket at `open`, with a comma when others follow
        private static string InsertMember(string json, int open, string member)
        {
            int next = SkipSpace(json, open + 1);
            bool empty = next < json.Length && (json[next] == '}' || json[next] == ']');
            return json.Substring(0, open + 1) + "\n  " + member +
                   (empty ? "\n" + json.Substring(next) : "," + json.Substring(open + 1));
        }

        private static bool IsBalanced(string json, int open)
        {
            int depth = 0;
            for (int i = open; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    i = StringEnd(json, i);
                    if (i < 0)
                        return false;
                }
                else if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    if (--depth < 0)
                        return false;
                    if (depth == 0)
                        return SkipSpace(json, i + 1) == json.Length;
                }
            }
            return false;
        }

        private static int StringEnd(string json, int quote)
        {
            for (int i = quote + 1; i < json.Length; i++)
            {
                if (json[i] == '\\')
                    i++;
                else if (json[i] == '"')
                    return i;
            }
            return -1;
        }

        private static int SkipSpace(string text, int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            return i;
        }

        /// <summary>
        /// The framework block replaces the previous one, or is appended when there is none.
        /// </summary>
        public static string MergeBlock(string existing, string block)
        {
            if (string.IsNullOrEmpty(existing))
                return block + "\n";

            int begin = existing.IndexOf(BlockBegin, StringComparison.Ordinal);
            int end = existing.IndexOf(BlockEnd, StringComparison.Ordinal);
            if (begin >= 0 && end > begin)
                return existing.Substring(0, begin) + block + existing.Substring(end + BlockEnd.Length);

            return existing.TrimEnd() + "\n\n" + block + "\n";
        }

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, Normalize(File.ReadAllText(source)));
        }

        private static string Normalize(string text) => text.Replace("\r\n", "\n");
    }
}
