using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RPGFramework.GameKit.Editor
{
    /// <summary>
    /// Creates the structure of a game, and of features inside it, from the templates in <c>Templates~</c>:
    /// assemblies split by the architecture rules, the composition, the host and scene, and tests (including the
    /// architecture tests). Every file gets its <c>.meta</c>. Existing files are never overwritten.
    /// Batch mode:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewGameFromCommandLine -game MyGame</c>,
    /// <c>... GameScaffold.NewFeatureFromCommandLine -game MyGame -feature Shop</c>; add <c>-aiGuidance</c> to New Game
    /// to also install the AI guidance.
    /// </summary>
    public static class GameScaffold
    {
        public const string GameToken = "__GAME__";
        public const string FeatureToken = "__FEATURE__";
        public const string HostGuidToken = "__HOST_GUID__";

        /// <summary>
        /// Line in the composition above which New Feature adds its module.
        /// </summary>
        public const string ModulesMarker = "// rpg-framework:modules";

        private static readonly Regex Identifier = new Regex("^[A-Z][A-Za-z0-9]{0,47}$");

        private static readonly Dictionary<string, string> Importers = new Dictionary<string, string>
        {
            [".cs"] = "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
            [".asmdef"] = "AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
            [".unity"] = "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
            [".md"] = "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        };

        private const string FolderImporter =
            "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        /// <summary>
        /// Templates shipped with the package.
        /// </summary>
        public static string TemplatesRoot => Path.Combine(PackagePaths.Root, "GameKit", "Editor", "Templates~");

        public static bool IsValidName(string name) => name != null && Identifier.IsMatch(name);

        /// <summary>
        /// Writes a new game into <paramref name="assetsFolder"/>/<paramref name="game"/>.
        /// </summary>
        /// <returns>Paths of the files written (without their .meta files).</returns>
        public static List<string> WriteGame(string templatesRoot, string assetsFolder, string game)
        {
            RequireName(game, "game");
            string gameFolder = Path.Combine(assetsFolder, game);
            if (Directory.Exists(gameFolder))
                throw new InvalidOperationException($"{gameFolder} already exists; pick another name or delete the folder");

            var replacements = new Dictionary<string, string> { [GameToken] = game };
            return WriteTree(Path.Combine(templatesRoot, "Game"), assetsFolder, replacements, $"{game}Host.cs");
        }

        /// <summary>
        /// Writes a new feature (module, rules, events, tests) into an existing game and adds its module to the
        /// game's composition.
        /// </summary>
        /// <returns>Paths of the files written, and whether the module was added to the composition.</returns>
        public static (List<string> Files, bool AddedToComposition) WriteFeature(string templatesRoot, string assetsFolder,
            string game, string feature)
        {
            RequireName(game, "game");
            RequireName(feature, "feature");
            string gameFolder = Path.Combine(assetsFolder, game);
            foreach (var required in new[] { Path.Combine(gameFolder, "Scripts", "Core"), Path.Combine(gameFolder, "Tests") })
            {
                if (!Directory.Exists(required))
                    throw new InvalidOperationException($"{required} not found; create the game with New Game first");
            }

            var replacements = new Dictionary<string, string> { [GameToken] = game, [FeatureToken] = feature };
            var files = WriteTree(Path.Combine(templatesRoot, "Feature"), assetsFolder, replacements, null);
            bool added = AddModuleToComposition(Path.Combine(gameFolder, "Scripts", "Core", $"{game}Composition.cs"), $"{feature}Module");
            return (files, added);
        }

        /// <summary>
        /// Inserts <c>.UseModule(new TModule())</c> above the modules marker.
        /// </summary>
        /// <returns>False when the file or the marker is missing (add the module by hand then).</returns>
        public static bool AddModuleToComposition(string compositionFile, string moduleType)
        {
            if (!File.Exists(compositionFile))
                return false;

            var lines = File.ReadAllLines(compositionFile).ToList();
            int marker = lines.FindIndex(line => line.Contains(ModulesMarker));
            if (marker < 0)
                return false;
            if (lines.Any(line => line.Contains($"new {moduleType}(")))
                return true;

            string indent = lines[marker].Substring(0, lines[marker].Length - lines[marker].TrimStart().Length);
            lines.Insert(marker, $"{indent}.UseModule(new {moduleType}())");
            File.WriteAllText(compositionFile, string.Join("\n", lines) + "\n");
            return true;
        }

        [MenuItem("RPG Framework/Game Kit/New Game or Feature...", priority = 0)]
        public static void OpenWindow() => GameKitWindow.Open();

        /// <summary>
        /// Creates a playable game in Assets: code from the templates, starter content (hero, goblin, items, loot,
        /// quest, Game Setup), the scene linked to it. Optionally installs the AI guidance at the project root.
        /// </summary>
        public static List<string> NewGame(string game, bool installAIGuidance = false)
        {
            var files = WriteGame(TemplatesRoot, Application.dataPath, game);
            AssetDatabase.Refresh();

            var setup = StarterContent.Create(game, out var ground);
            string scene = $"Assets/{game}/Scenes/{game}.unity";
            StarterContent.LinkScene(scene, setup, ground);

            if (installAIGuidance)
                AIGuidance.Install(AIGuidance.DocumentationRoot, PackagePaths.ProjectRoot);
            AssetDatabase.Refresh();

            Debug.Log($"[Game Kit] Created {game} in Assets/{game}: open {scene} and press Play. " +
                      $"Tune the game in Assets/{game}/Content/{game}Setup.asset");
            if (!Application.isBatchMode && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scene);
            return files;
        }

        public static List<string> NewFeature(string game, string feature)
        {
            var (files, added) = WriteFeature(TemplatesRoot, Application.dataPath, game, feature);
            AssetDatabase.Refresh();
            if (added)
                Debug.Log($"[Game Kit] Created feature {feature} in {game} ({files.Count} files) and added {feature}Module to {game}Composition");
            else
                Debug.LogWarning($"[Game Kit] Created feature {feature} in {game}; add `.UseModule(new {feature}Module())` to {game}Composition by hand");
            return files;
        }

        public static void NewGameFromCommandLine() =>
            NewGame(Argument("-game"), Environment.GetCommandLineArgs().Contains("-aiGuidance"));

        public static void NewFeatureFromCommandLine() => NewFeature(Argument("-game"), Argument("-feature"));

        private static List<string> WriteTree(string templateRoot, string targetRoot, Dictionary<string, string> replacements,
            string hostFile)
        {
            if (!Directory.Exists(templateRoot))
                throw new DirectoryNotFoundException($"Templates not found at {templateRoot}");

            string Replace(string text) => replacements.Aggregate(text, (current, pair) => current.Replace(pair.Key, pair.Value));

            var files = Directory.GetFiles(templateRoot, "*", SearchOption.AllDirectories)
                .Select(source => (Source: source, Target: Path.Combine(targetRoot, Replace(Relative(templateRoot, source)))))
                .ToList();
            var existing = files.Where(f => File.Exists(f.Target)).Select(f => f.Target).ToList();
            if (existing.Count > 0)
                throw new InvalidOperationException("Would overwrite existing files:\n" + string.Join("\n", existing));

            var guids = files.ToDictionary(f => f.Target, _ => Guid.NewGuid().ToString("N"));
            var allReplacements = new Dictionary<string, string>(replacements);
            if (hostFile != null)
            {
                var host = files.FirstOrDefault(f => Path.GetFileName(f.Target) == hostFile);
                if (host.Target != null)
                    allReplacements[HostGuidToken] = guids[host.Target];
            }

            foreach (var (source, target) in files)
            {
                EnsureFolder(targetRoot, Path.GetDirectoryName(target));
                string text = File.ReadAllText(source).Replace("\r\n", "\n");
                File.WriteAllText(target, allReplacements.Aggregate(text, (current, pair) => current.Replace(pair.Key, pair.Value)));
                WriteMeta(target, guids[target], ImporterFor(target));
            }
            return files.Select(f => f.Target).ToList();
        }

        /// <summary>
        /// Creates the folder and every missing parent below <paramref name="root"/>, each with a folder .meta.
        /// </summary>
        private static void EnsureFolder(string root, string folder)
        {
            if (Directory.Exists(folder))
                return;
            string parent = Path.GetDirectoryName(folder);
            if (parent != null && !string.Equals(Path.GetFullPath(folder), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                EnsureFolder(root, parent);
            Directory.CreateDirectory(folder);
            WriteMeta(folder, Guid.NewGuid().ToString("N"), FolderImporter);
        }

        private static string ImporterFor(string path)
        {
            if (Importers.TryGetValue(Path.GetExtension(path), out var importer))
                return importer;
            throw new InvalidOperationException($"No importer known for {path}");
        }

        private static void WriteMeta(string path, string guid, string importer)
        {
            string meta = path + ".meta";
            if (!File.Exists(meta))
                File.WriteAllText(meta, $"fileFormatVersion: 2\nguid: {guid}\n{importer}");
        }

        private static string Relative(string root, string path) =>
            path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static void RequireName(string name, string what)
        {
            if (!IsValidName(name))
                throw new ArgumentException(
                    $"'{name}' is not a valid {what} name: use PascalCase letters and digits, starting with a capital (e.g. MyGame)");
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException($"Missing command line argument {name} <value>");
            return args[index + 1];
        }
    }
}
