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
    /// <c>/start</c> and <c>/handoff</c> commands), created once and then kept,
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

        public static string DocumentationRoot => Path.Combine(PackagePaths.Root, "Documentation~", "AI");

        [MenuItem("RPG Framework/Game Kit/Install AI Guidance", priority = 20)]
        public static void InstallFromMenu()
        {
            var written = Install(DocumentationRoot, PackagePaths.ProjectRoot);
            Debug.Log("[Game Kit] AI guidance installed:\n" + string.Join("\n", written));
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
        /// <returns>Project-relative paths written.</returns>
        public static List<string> Install(string documentationRoot, string projectRoot, bool claudeSettings = false)
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
