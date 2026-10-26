using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.GameKit.Editor
{
    /// <summary>
    /// Installs the guidance an AI assistant (or a new developer) needs to build a game the framework's way:
    /// a block in the project's <c>CLAUDE.md</c>, the architecture guide and recipes under <c>Docs/RPGFramework</c>,
    /// and a CI workflow that runs the tests when the project has none. Reinstalling updates the framework's
    /// files and its <c>CLAUDE.md</c> block; the rest of <c>CLAUDE.md</c> is kept.
    /// </summary>
    public static class AIGuidance
    {
        public const string BlockBegin = "<!-- rpg-framework:begin";
        public const string BlockEnd = "<!-- rpg-framework:end -->";
        public const string DocsFolder = "Docs/RPGFramework";
        public const string WorkflowPath = ".github/workflows/unity-tests.yml";

        public static string DocumentationRoot => Path.Combine(PackagePaths.Root, "Documentation~", "AI");

        [MenuItem("RPG Framework/Game Kit/Install AI Guidance", priority = 20)]
        public static void InstallFromMenu()
        {
            var written = Install(DocumentationRoot, PackagePaths.ProjectRoot);
            Debug.Log("[Game Kit] AI guidance installed:\n" + string.Join("\n", written));
        }

        public static void InstallFromCommandLine() => InstallFromMenu();

        /// <returns>Project-relative paths written.</returns>
        public static List<string> Install(string documentationRoot, string projectRoot)
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

            string workflow = Path.Combine(projectRoot, WorkflowPath);
            string workflowTemplate = Path.Combine(documentationRoot, "ci", "unity-tests.yml");
            if (!File.Exists(workflow) && File.Exists(workflowTemplate))
            {
                Copy(workflowTemplate, workflow);
                written.Add(WorkflowPath);
            }
            return written;
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
