using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.Tools.ProjectIndex
{
    /// <summary>
    /// Writes the project's <see cref="ProjectIndex"/> from the menu (RPG Framework > Tools > Export Project Index) or
    /// from the command line:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.Tools.ProjectIndex.ProjectIndexCommand.ExportFromCommandLine [-indexOutput &lt;file&gt;] [-includePackages] [-indexExclude Assets/ThirdParty;Assets/Art]</c>.
    /// The default file is <c>Library/RPGFramework/ProjectIndex.json</c>. Exit code 0 when written, 1 on a failure.
    /// </summary>
    public static class ProjectIndexCommand
    {
        public const string DefaultOutput = "Library/RPGFramework/ProjectIndex.json";

        [MenuItem("RPG Framework/Tools/Export Project Index", false, 40)]
        public static void ExportFromMenu()
        {
            var index = Export(DefaultOutput, new ProjectIndexOptions());
            EditorUtility.RevealInFinder(Path.Combine(ProjectRoot, DefaultOutput));
            Debug.Log(Summary(index, DefaultOutput));
        }

        public static void ExportFromCommandLine()
        {
            try
            {
                var options = new ProjectIndexOptions { IncludeEmbeddedPackages = Flag("-includePackages") };
                var exclude = Argument("-indexExclude");
                if (!string.IsNullOrEmpty(exclude))
                    options.Exclude.AddRange(exclude.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                var output = Argument("-indexOutput") ?? DefaultOutput;
                var index = Export(output, options);
                Debug.Log(Summary(index, output));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Project Index] Failed: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        private static ProjectIndex Export(string output, ProjectIndexOptions options)
        {
            var path = Path.IsPathRooted(output) ? output : Path.Combine(ProjectRoot, output);
            return ProjectIndexer.Export(ProjectRoot, path, options);
        }

        private static string Summary(ProjectIndex index, string output) =>
            $"[Project Index] {output}: {index.assets.Count} assets, {index.scenes.Count} scenes, {index.prefabs.Count} prefabs, " +
            $"{index.scripts.Count} scripts, {index.assemblies.Count} assemblies, {index.problems.Count} problem(s)";

        private static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
