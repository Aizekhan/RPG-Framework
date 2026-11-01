using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RPGFramework.Tools.ProjectIndex
{
    /// <summary>What <see cref="ProjectIndexer"/> scans.</summary>
    public sealed class ProjectIndexOptions
    {
        /// <summary>Also index the packages embedded in <c>Packages/</c> (their own folders), not only <c>Assets/</c>.</summary>
        public bool IncludeEmbeddedPackages;

        /// <summary>Project-relative folders or files to leave out (forward slashes), e.g. <c>Assets/ThirdParty</c>.</summary>
        public List<string> Exclude = new List<string>();

        /// <summary>Most public members listed per type.</summary>
        public int MembersPerType = 40;
    }

    /// <summary>
    /// Builds a <see cref="ProjectIndex"/> by reading the project's files: <c>.meta</c> GUIDs, the YAML of scenes,
    /// prefabs and assets, <c>.cs</c> and <c>.asmdef</c> files, <c>Packages/manifest.json</c> and
    /// <c>ProjectSettings</c>. It opens nothing for writing and does not use the AssetDatabase, so it works the same
    /// in the editor, in batch mode and on a project that does not compile. Skipped: Library, Temp, Logs, obj,
    /// UserSettings, hidden and <c>~</c> folders, and key or secret files (.env*, .pem, .key, .keystore, .p12,
    /// .pfx). Library/PackageCache is read only to name the packages' GUIDs.
    /// </summary>
    public static class ProjectIndexer
    {
        private static readonly Regex GuidRef = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);
        private static readonly Regex DocHeader = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex FileIdField = new Regex(@"\{fileID: (-?\d+)", RegexOptions.Compiled);
        private static readonly Regex NamespaceDecl = new Regex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Compiled);
        private static readonly Regex UsingDecl = new Regex(@"^\s*using\s+(?:static\s+)?([\w.]+)\s*;", RegexOptions.Compiled);
        private static readonly Regex TypeDecl = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*((?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|new|ref)\s+)*)(class|struct|interface|enum|record)\s+(\w+)(?:<[^>{]*>)?\s*(?:\(([^)]*)\))?\s*(?::\s*([^{]+?))?\s*(?:where\b[^{]*)?(?:\{|$)",
            RegexOptions.Compiled);
        private static readonly Regex MemberDecl = new Regex(
            @"^\s*public\s+(?:(?:static|virtual|override|abstract|sealed|readonly|async|new|extern|unsafe|const|event|partial|required)\s+)*[\w<>\[\],.?() ]+?\s+(\w+)\s*(?:<[^>]*>)?\s*(\(|\{|=>|;|=)",
            RegexOptions.Compiled);
        private static readonly Regex JsonString = new Regex("\"name\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);

        private static readonly HashSet<string> SkippedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Library", "Temp", "Logs", "obj", "UserSettings", "MemoryCaptures", "Recordings" };

        private static readonly HashSet<string> SecretExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pem", ".key", ".keystore", ".jks", ".p12", ".pfx" };

        private static readonly HashSet<string> YamlExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController", ".mixer", ".playable",
            ".physicMaterial", ".physicsMaterial2D", ".lighting", ".spriteatlas", ".renderTexture", ".flare", ".mask",
            ".guiskin", ".fontsettings", ".cubemap", ".brush", ".terrainlayer", ".signal",
        };

        private static readonly Dictionary<string, string> TypeByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".unity"] = "Scene", [".prefab"] = "Prefab", [".cs"] = "Script", [".asmdef"] = "AssemblyDefinition",
            [".asmref"] = "AssemblyReference", [".mat"] = "Material", [".png"] = "Texture", [".jpg"] = "Texture",
            [".jpeg"] = "Texture", [".tga"] = "Texture", [".psd"] = "Texture", [".tif"] = "Texture", [".tiff"] = "Texture",
            [".exr"] = "Texture", [".hdr"] = "Texture", [".bmp"] = "Texture", [".gif"] = "Texture", [".fbx"] = "Model",
            [".obj"] = "Model", [".blend"] = "Model", [".dae"] = "Model", [".3ds"] = "Model", [".max"] = "Model",
            [".anim"] = "AnimationClip", [".controller"] = "AnimatorController",
            [".overrideController"] = "AnimatorOverrideController", [".mask"] = "AvatarMask", [".wav"] = "AudioClip",
            [".mp3"] = "AudioClip", [".ogg"] = "AudioClip", [".aif"] = "AudioClip", [".aiff"] = "AudioClip",
            [".flac"] = "AudioClip", [".mixer"] = "AudioMixer", [".shader"] = "Shader", [".shadergraph"] = "Shader",
            [".shadersubgraph"] = "Shader", [".hlsl"] = "Shader", [".cginc"] = "Shader", [".compute"] = "ComputeShader",
            [".ttf"] = "Font", [".otf"] = "Font", [".fontsettings"] = "Font", [".physicMaterial"] = "PhysicsMaterial",
            [".physicsMaterial2D"] = "PhysicsMaterial2D", [".renderTexture"] = "RenderTexture",
            [".inputactions"] = "InputActions", [".json"] = "TextAsset", [".txt"] = "TextAsset", [".csv"] = "TextAsset",
            [".xml"] = "TextAsset", [".bytes"] = "TextAsset", [".md"] = "TextAsset", [".uxml"] = "UIDocument",
            [".uss"] = "StyleSheet", [".vfx"] = "VisualEffect", [".playable"] = "Timeline", [".lighting"] = "LightingSettings",
            [".spriteatlas"] = "SpriteAtlas", [".mp4"] = "VideoClip", [".mov"] = "VideoClip", [".webm"] = "VideoClip",
            [".terrainlayer"] = "TerrainLayer", [".signal"] = "Signal", [".preset"] = "Preset", [".dll"] = "Plugin",
        };

        // Unity class ids of the components worth naming; others are written as ClassID<n>
        private static readonly Dictionary<int, string> ClassNames = new Dictionary<int, string>
        {
            [4] = "Transform", [20] = "Camera", [23] = "MeshRenderer", [33] = "MeshFilter", [50] = "Rigidbody2D",
            [54] = "Rigidbody", [56] = "TerrainCollider", [58] = "CircleCollider2D", [60] = "PolygonCollider2D",
            [61] = "BoxCollider2D", [64] = "MeshCollider", [65] = "BoxCollider", [66] = "CompositeCollider2D",
            [68] = "EdgeCollider2D", [70] = "CapsuleCollider2D", [81] = "AudioListener", [82] = "AudioSource",
            [95] = "Animator", [96] = "TrailRenderer", [102] = "TextMesh", [108] = "Light", [111] = "Animation",
            [120] = "LineRenderer", [135] = "SphereCollider", [136] = "CapsuleCollider", [137] = "SkinnedMeshRenderer",
            [143] = "CharacterController", [146] = "WheelCollider", [154] = "TerrainCollider", [195] = "NavMeshAgent",
            [198] = "ParticleSystem", [199] = "ParticleSystemRenderer", [205] = "LODGroup", [208] = "NavMeshObstacle",
            [210] = "SortingGroup", [212] = "SpriteRenderer", [215] = "ReflectionProbe", [218] = "Terrain",
            [220] = "LightProbeGroup", [222] = "CanvasRenderer", [223] = "Canvas", [224] = "RectTransform",
            [225] = "CanvasGroup", [320] = "PlayableDirector", [328] = "VideoPlayer", [331] = "SpriteMask",
            [483693784] = "TilemapRenderer", [1839735485] = "Tilemap", [156049354] = "Grid",
        };

        // Scene-wide settings objects, not components
        private static readonly HashSet<int> SceneSettings = new HashSet<int> { 29, 104, 157, 196 };

        /// <summary>Scans the project at <paramref name="projectRoot"/> (the folder holding Assets/).</summary>
        public static ProjectIndex Scan(string projectRoot, ProjectIndexOptions options = null)
        {
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(Path.Combine(projectRoot, "Assets")))
                throw new DirectoryNotFoundException($"No Unity project (Assets folder) at '{projectRoot}'");
            options = options ?? new ProjectIndexOptions();
            return new Scanner(Path.GetFullPath(projectRoot), options).Run();
        }

        /// <summary>Scans and writes the index as indented JSON to <paramref name="outputPath"/>.</summary>
        public static ProjectIndex Export(string projectRoot, string outputPath, ProjectIndexOptions options = null)
        {
            var index = Scan(projectRoot, options);
            var folder = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(outputPath, UnityEngine.JsonUtility.ToJson(index, true), new UTF8Encoding(false));
            return index;
        }

        private sealed class Scanner
        {
            private readonly string _root;
            private readonly ProjectIndexOptions _options;
            private readonly ProjectIndex _index = new ProjectIndex();
            private readonly Dictionary<string, string> _pathByGuid = new Dictionary<string, string>();
            private readonly Dictionary<string, string> _guidByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, string> _classByScriptGuid = new Dictionary<string, string>();
            private readonly Dictionary<string, string> _asmdefNameByGuid = new Dictionary<string, string>();
            private readonly List<string> _files = new List<string>();
            private readonly Dictionary<string, (int Found, int Indexed)> _coverage = new Dictionary<string, (int, int)>();
            private readonly Dictionary<string, SortedSet<string>> _moduleUsers = new Dictionary<string, SortedSet<string>>();
            private readonly List<(string Folder, string Name)> _asmdefFolders = new List<(string, string)>();

            public Scanner(string root, ProjectIndexOptions options)
            {
                _root = root;
                _options = options;
            }

            public ProjectIndex Run()
            {
                _index.scannedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                ReadProject();
                Collect("Assets");
                if (_options.IncludeEmbeddedPackages)
                {
                    foreach (var package in EmbeddedPackages())
                        Collect(package);
                }
                ReadGuids();
                ReadAssemblies();
                ReadScripts();
                ReadAssets();
                ReadBuildScenes();
                ReadFramework();
                foreach (var pair in _coverage.OrderBy(p => p.Key, StringComparer.Ordinal))
                    _index.coverage.Add(new CoverageEntry { kind = pair.Key, found = pair.Value.Found, indexed = pair.Value.Indexed });
                return _index;
            }

            // --- project settings, packages, repository

            private void ReadProject()
            {
                var project = _index.project;
                project.name = Path.GetFileName(_root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var version = ReadText("ProjectSettings/ProjectVersion.txt");
                if (version != null)
                {
                    var match = Regex.Match(version, @"m_EditorVersion:\s*(\S+)");
                    if (match.Success)
                        project.unityVersion = match.Groups[1].Value;
                }

                var manifest = ReadText("Packages/manifest.json");
                if (manifest != null)
                {
                    foreach (var (name, value) in JsonObjectEntries(manifest, "dependencies"))
                        project.packages.Add(new PackageRef { name = name, version = value });
                }
                project.packages.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                project.renderPipeline = project.packages.Any(p => p.name == "com.unity.render-pipelines.universal") ? "URP"
                    : project.packages.Any(p => p.name == "com.unity.render-pipelines.high-definition") ? "HDRP"
                    : "Built-in";

                var head = ReadText(".git/HEAD")?.Trim();
                if (head == null)
                    return;
                if (head.StartsWith("ref: ", StringComparison.Ordinal))
                {
                    var reference = head.Substring(5).Trim();
                    project.repo.branch = reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference.Substring(11) : reference;
                    project.repo.commit = ReadText(".git/" + reference)?.Trim();
                    if (project.repo.commit == null)
                    {
                        var packed = ReadText(".git/packed-refs");
                        var line = packed?.Split('\n').FirstOrDefault(l => l.TrimEnd().EndsWith(" " + reference, StringComparison.Ordinal));
                        project.repo.commit = line?.Split(' ')[0];
                    }
                }
                else
                {
                    project.repo.commit = head;
                }
            }

            private IEnumerable<string> EmbeddedPackages()
            {
                var packages = Path.Combine(_root, "Packages");
                if (!Directory.Exists(packages))
                    yield break;
                foreach (var folder in Directory.GetDirectories(packages).OrderBy(f => f, StringComparer.Ordinal))
                {
                    if (File.Exists(Path.Combine(folder, "package.json")))
                        yield return "Packages/" + Path.GetFileName(folder);
                }
            }

            // --- files and guids

            private void Collect(string relativeFolder)
            {
                _index.pathsScanned.Add(relativeFolder);
                var stack = new Stack<string>();
                stack.Push(Path.Combine(_root, relativeFolder));
                while (stack.Count > 0)
                {
                    var folder = stack.Pop();
                    foreach (var sub in Directory.GetDirectories(folder))
                    {
                        var name = Path.GetFileName(sub);
                        if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith("~", StringComparison.Ordinal) ||
                            SkippedFolders.Contains(name) || Excluded(Relative(sub)))
                            continue;
                        stack.Push(sub);
                    }
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        var name = Path.GetFileName(file);
                        if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".", StringComparison.Ordinal) ||
                            SecretExtensions.Contains(Path.GetExtension(name)) || Excluded(Relative(file)))
                            continue;
                        _files.Add(Relative(file));
                    }
                }
                _files.Sort(StringComparer.Ordinal);
            }

            private bool Excluded(string relative) =>
                _options.Exclude.Any(e => relative.Equals(e.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                                          relative.StartsWith(e.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));

            private void ReadGuids()
            {
                foreach (var file in _files)
                {
                    var guid = MetaGuid(Path.Combine(_root, file + ".meta"));
                    if (guid == null)
                        continue;
                    _pathByGuid[guid] = file;
                    _guidByPath[file] = guid;
                }

                // Packages that are not indexed still name the GUIDs the game's assets point at
                foreach (var folder in PackageFolders())
                {
                    foreach (var meta in Directory.EnumerateFiles(folder.Full, "*.meta", SearchOption.AllDirectories))
                    {
                        var guid = MetaGuid(meta);
                        if (guid == null || _pathByGuid.ContainsKey(guid))
                            continue;
                        var path = folder.Name + "/" + meta.Substring(folder.Full.Length + 1, meta.Length - folder.Full.Length - 6).Replace('\\', '/');
                        _pathByGuid[guid] = path;
                        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                            _classByScriptGuid[guid] = Path.GetFileNameWithoutExtension(path);
                        else if (path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                            _asmdefNameByGuid[guid] = AsmdefName(meta.Substring(0, meta.Length - 5));
                    }
                }
            }

            private IEnumerable<(string Name, string Full)> PackageFolders()
            {
                var packages = Path.Combine(_root, "Packages");
                if (Directory.Exists(packages))
                {
                    foreach (var folder in Directory.GetDirectories(packages))
                    {
                        var name = "Packages/" + Path.GetFileName(folder);
                        if (!_index.pathsScanned.Contains(name))
                            yield return (name, folder);
                    }
                }
                var cache = Path.Combine(_root, "Library", "PackageCache");
                if (Directory.Exists(cache))
                {
                    foreach (var folder in Directory.GetDirectories(cache))
                    {
                        var name = Path.GetFileName(folder);
                        int at = name.IndexOf('@');
                        yield return ("Packages/" + (at > 0 ? name.Substring(0, at) : name), folder);
                    }
                }
            }

            private static string MetaGuid(string metaPath)
            {
                if (!File.Exists(metaPath))
                    return null;
                using (var reader = new StreamReader(metaPath))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                            break;
                        if (line.StartsWith("guid: ", StringComparison.Ordinal))
                            return line.Substring(6).Trim();
                    }
                }
                return null;
            }

            // --- assemblies and scripts

            private void ReadAssemblies()
            {
                foreach (var file in _files.Where(f => f.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)))
                {
                    var name = AsmdefName(Path.Combine(_root, file));
                    _guidByPath.TryGetValue(file, out var guid);
                    if (guid != null)
                        _asmdefNameByGuid[guid] = name;
                    _asmdefFolders.Add((file.Substring(0, file.LastIndexOf('/')), name));
                }
                _asmdefFolders.Sort((a, b) => b.Folder.Length.CompareTo(a.Folder.Length));

                foreach (var file in _files.Where(f => f.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)))
                {
                    var text = File.ReadAllText(Path.Combine(_root, file));
                    _guidByPath.TryGetValue(file, out var guid);
                    var record = new AssemblyRecord { name = AsmdefName(Path.Combine(_root, file)), path = file, guid = guid };
                    foreach (var reference in JsonArray(text, "references"))
                    {
                        var resolved = reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase)
                            ? (_asmdefNameByGuid.TryGetValue(reference.Substring(5).ToLowerInvariant(), out var named) ? named : reference)
                            : reference;
                        record.references.Add(resolved);
                        if (!IsFrameworkPath(file))
                            UseModule(resolved, record.name);
                    }
                    _index.assemblies.Add(record);
                }
            }

            private static string AsmdefName(string path)
            {
                try
                {
                    var match = JsonString.Match(File.ReadAllText(path));
                    return match.Success ? match.Groups[1].Value : Path.GetFileNameWithoutExtension(path);
                }
                catch (IOException)
                {
                    return Path.GetFileNameWithoutExtension(path);
                }
            }

            private string AssemblyOf(string file)
            {
                foreach (var (folder, name) in _asmdefFolders)
                {
                    if (file.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                        return name;
                }
                bool editor = file.Split('/').Any(part => part.Equals("Editor", StringComparison.OrdinalIgnoreCase));
                bool firstPass = file.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase) ||
                                 file.StartsWith("Assets/Standard Assets/", StringComparison.OrdinalIgnoreCase);
                return "Assembly-CSharp" + (firstPass ? "-firstpass" : "") + (editor ? "-Editor" : "");
            }

            private void ReadScripts()
            {
                foreach (var file in _files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                {
                    _guidByPath.TryGetValue(file, out var guid);
                    var record = new ScriptRecord { guid = guid, path = file, assembly = AssemblyOf(file) };
                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(Path.Combine(_root, file));
                    }
                    catch (IOException)
                    {
                        _index.problems.Add($"{file} could not be read");
                        continue;
                    }
                    ParseScript(lines, record, _options.MembersPerType);
                    if (guid != null)
                        _classByScriptGuid[guid] = record.types.FirstOrDefault(t => t.name == Path.GetFileNameWithoutExtension(file))?.name
                                                   ?? Path.GetFileNameWithoutExtension(file);
                    if (!IsFrameworkPath(file))
                    {
                        foreach (var name in record.usings.Where(u => u.StartsWith("RPGFramework.", StringComparison.Ordinal)))
                            UseModule(name, record.assembly);
                    }
                    _index.scripts.Add(record);
                }
            }

            internal static void ParseScript(string[] lines, ScriptRecord record, int membersPerType)
            {
                var summary = new StringBuilder();
                bool inSummary = false;
                TypeRecord current = null;
                bool inBlockComment = false;
                foreach (var raw in lines)
                {
                    var line = raw;
                    if (inBlockComment)
                    {
                        if (line.Contains("*/"))
                            inBlockComment = false;
                        continue;
                    }
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("/*", StringComparison.Ordinal) && !trimmed.Contains("*/"))
                    {
                        inBlockComment = true;
                        continue;
                    }
                    if (trimmed.StartsWith("///", StringComparison.Ordinal))
                    {
                        var doc = trimmed.Substring(3).Trim();
                        if (doc.StartsWith("<summary>", StringComparison.Ordinal))
                        {
                            summary.Clear();
                            inSummary = true;
                            doc = doc.Substring(9);
                        }
                        if (inSummary)
                        {
                            int end = doc.IndexOf("</summary>", StringComparison.Ordinal);
                            if (end >= 0)
                            {
                                doc = doc.Substring(0, end);
                                inSummary = false;
                            }
                            if (doc.Length > 0)
                                summary.Append(summary.Length > 0 ? " " : "").Append(doc);
                        }
                        continue;
                    }
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.Length == 0)
                        continue;
                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                        continue;

                    var usingMatch = UsingDecl.Match(line);
                    if (usingMatch.Success && current == null)
                    {
                        if (!record.usings.Contains(usingMatch.Groups[1].Value))
                            record.usings.Add(usingMatch.Groups[1].Value);
                        continue;
                    }
                    var namespaceMatch = NamespaceDecl.Match(line);
                    if (namespaceMatch.Success)
                    {
                        record.@namespace = record.@namespace ?? namespaceMatch.Groups[1].Value;
                        summary.Clear();
                        continue;
                    }
                    var typeMatch = TypeDecl.Match(line);
                    if (typeMatch.Success)
                    {
                        var modifiers = typeMatch.Groups[1].Value;
                        if (modifiers.Contains("private") || (modifiers.Contains("protected") && !modifiers.Contains("internal")))
                        {
                            summary.Clear();
                            continue;
                        }
                        current = new TypeRecord
                        {
                            name = typeMatch.Groups[3].Value,
                            kind = typeMatch.Groups[2].Value,
                            summary = summary.Length > 0 ? Clean(summary.ToString()) : null,
                        };
                        if (typeMatch.Groups[5].Success)
                        {
                            foreach (var part in SplitBases(typeMatch.Groups[5].Value))
                                current.baseTypes.Add(part);
                        }
                        record.types.Add(current);
                        summary.Clear();
                        continue;
                    }
                    var memberMatch = MemberDecl.Match(line);
                    if (memberMatch.Success && current != null && current.publicMembers.Count < membersPerType)
                    {
                        var member = memberMatch.Groups[1].Value;
                        if (!current.publicMembers.Contains(member))
                            current.publicMembers.Add(member);
                    }
                    summary.Clear();
                    inSummary = false;
                }
            }

            private static IEnumerable<string> SplitBases(string bases)
            {
                int depth = 0;
                var part = new StringBuilder();
                foreach (char c in bases)
                {
                    if (c == '<')
                        depth++;
                    else if (c == '>')
                        depth--;
                    if (c == ',' && depth == 0)
                    {
                        if (part.ToString().Trim().Length > 0)
                            yield return part.ToString().Trim();
                        part.Clear();
                        continue;
                    }
                    part.Append(c);
                }
                if (part.ToString().Trim().Length > 0)
                    yield return part.ToString().Trim();
            }

            private static string Clean(string summary) =>
                Regex.Replace(Regex.Replace(summary, @"<(?:see|seealso|paramref|typeparamref)\s+\w+=""([^""]*)""\s*/>", "$1"), @"<[^>]+>", "").Trim();

            // --- assets, scenes, prefabs

            private void ReadAssets()
            {
                foreach (var file in _files)
                {
                    var extension = Path.GetExtension(file);
                    _guidByPath.TryGetValue(file, out var guid);
                    string type = TypeByExtension.TryGetValue(extension, out var known) ? known : extension.TrimStart('.').ToUpperInvariant();
                    var record = new AssetRecord { guid = guid, path = file, name = Path.GetFileNameWithoutExtension(file) };
                    if (guid == null)
                        _index.problems.Add($"{file} has no .meta file (no GUID)");

                    List<string> yaml = null;
                    if (YamlExtensions.Contains(extension))
                    {
                        yaml = ReadYaml(file);
                        if (yaml == null)
                            _index.problems.Add($"{file} is not saved as text (Force Text serialization), its references are not indexed");
                        else
                            record.deps = Deps(file, guid, yaml);
                    }
                    if (extension.Equals(".asset", StringComparison.OrdinalIgnoreCase) && yaml != null)
                        type = ScriptableObjectType(yaml) ?? "Asset";
                    record.type = type;
                    _index.assets.Add(record);
                    Count(type.StartsWith("ScriptableObject", StringComparison.Ordinal) ? "ScriptableObject" : type, true);

                    if (yaml != null && extension.Equals(".unity", StringComparison.OrdinalIgnoreCase))
                        _index.scenes.Add(ReadScene(file, guid, yaml, record.deps));
                    else if (yaml != null && extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                        _index.prefabs.Add(ReadPrefab(file, guid, yaml, record.deps));
                }
            }

            private List<string> ReadYaml(string file)
            {
                try
                {
                    var lines = File.ReadAllLines(Path.Combine(_root, file));
                    return lines.Length > 0 && lines[0].StartsWith("%YAML", StringComparison.Ordinal) ? lines.ToList() : null;
                }
                catch (IOException)
                {
                    return null;
                }
            }

            private List<string> Deps(string file, string ownGuid, List<string> yaml)
            {
                var deps = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var line in yaml)
                {
                    foreach (Match match in GuidRef.Matches(line))
                    {
                        var guid = match.Groups[1].Value;
                        if (guid == ownGuid || guid.StartsWith("0000000000000000", StringComparison.Ordinal))
                            continue;
                        deps.Add(guid);
                    }
                }
                foreach (var guid in deps)
                {
                    if (!_pathByGuid.ContainsKey(guid))
                        _index.problems.Add($"{file} references a missing asset (guid {guid})");
                }
                return deps.ToList();
            }

            private string ScriptableObjectType(List<string> yaml)
            {
                foreach (var line in yaml)
                {
                    var trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("m_Script:", StringComparison.Ordinal))
                        continue;
                    var match = GuidRef.Match(trimmed);
                    if (match.Success)
                        return "ScriptableObject:" + (_classByScriptGuid.TryGetValue(match.Groups[1].Value, out var name) ? name : "Missing");
                }
                return null;
            }

            private sealed class YamlObject
            {
                public int ClassId;
                public string FileId;
                public string Name;
                public string GameObject;
                public string Father;
                public string ScriptGuid;
                public string SourcePrefab;
                public string TransformParent;
                public bool Stripped;
            }

            private List<YamlObject> Objects(List<string> yaml)
            {
                var objects = new List<YamlObject>();
                YamlObject current = null;
                foreach (var line in yaml)
                {
                    var header = DocHeader.Match(line);
                    if (header.Success)
                    {
                        current = new YamlObject
                        {
                            ClassId = int.Parse(header.Groups[1].Value, CultureInfo.InvariantCulture),
                            FileId = header.Groups[2].Value,
                            Stripped = line.EndsWith("stripped", StringComparison.Ordinal),
                        };
                        objects.Add(current);
                        continue;
                    }
                    if (current == null || !line.StartsWith("  ", StringComparison.Ordinal))
                        continue;
                    if (line.StartsWith("   ", StringComparison.Ordinal))
                    {
                        // A prefab instance's parent sits inside m_Modification
                        var nested = line.TrimStart();
                        if (nested.StartsWith("m_TransformParent: ", StringComparison.Ordinal))
                            current.TransformParent = FileIdOf(nested);
                        continue;
                    }
                    var trimmed = line.Substring(2);
                    if (trimmed.StartsWith("m_Name: ", StringComparison.Ordinal))
                        current.Name = current.Name ?? trimmed.Substring(8).Trim();
                    else if (trimmed.StartsWith("m_GameObject: ", StringComparison.Ordinal))
                        current.GameObject = FileIdOf(trimmed);
                    else if (trimmed.StartsWith("m_Father: ", StringComparison.Ordinal))
                        current.Father = FileIdOf(trimmed);
                    else if (trimmed.StartsWith("m_TransformParent: ", StringComparison.Ordinal))
                        current.TransformParent = FileIdOf(trimmed);
                    else if (trimmed.StartsWith("m_Script: ", StringComparison.Ordinal))
                        current.ScriptGuid = GuidOf(trimmed);
                    else if (trimmed.StartsWith("m_SourcePrefab: ", StringComparison.Ordinal))
                        current.SourcePrefab = GuidOf(trimmed);
                }
                return objects;
            }

            private static string GuidOf(string line)
            {
                var match = GuidRef.Match(line);
                return match.Success ? match.Groups[1].Value : null;
            }

            private static string FileIdOf(string line)
            {
                var match = FileIdField.Match(line);
                return match.Success ? match.Groups[1].Value : null;
            }

            private List<string> Components(string file, List<YamlObject> objects)
            {
                var components = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var item in objects)
                {
                    if (item.Stripped || item.ClassId == 1 || item.ClassId == 1001 || SceneSettings.Contains(item.ClassId))
                        continue;
                    if (item.ClassId == 114)
                    {
                        if (item.ScriptGuid == null)
                            continue;
                        if (_classByScriptGuid.TryGetValue(item.ScriptGuid, out var name))
                        {
                            components.Add(name);
                        }
                        else
                        {
                            components.Add("MissingScript");
                            _index.problems.Add($"{file} has a missing script (guid {item.ScriptGuid})");
                        }
                        continue;
                    }
                    components.Add(ClassNames.TryGetValue(item.ClassId, out var known) ? known : "ClassID" + item.ClassId);
                }
                return components.ToList();
            }

            private List<string> Roots(List<YamlObject> objects)
            {
                var names = objects.Where(o => o.ClassId == 1 && !o.Stripped).ToDictionary(o => o.FileId, o => o.Name);
                var roots = new List<string>();
                foreach (var item in objects)
                {
                    if ((item.ClassId == 4 || item.ClassId == 224) && !item.Stripped && item.Father == "0" &&
                        item.GameObject != null && names.TryGetValue(item.GameObject, out var name))
                        roots.Add(name);
                    else if (item.ClassId == 1001 && item.TransformParent == "0" && item.SourcePrefab != null)
                        roots.Add(_pathByGuid.TryGetValue(item.SourcePrefab, out var path)
                            ? Path.GetFileNameWithoutExtension(path) + " (prefab)"
                            : "Missing prefab (prefab)");
                }
                return roots;
            }

            private SceneRecord ReadScene(string file, string guid, List<string> yaml, List<string> deps)
            {
                var objects = Objects(yaml);
                return new SceneRecord
                {
                    guid = guid, path = file, name = Path.GetFileNameWithoutExtension(file), roots = Roots(objects),
                    components = Components(file, objects), deps = deps,
                };
            }

            private PrefabRecord ReadPrefab(string file, string guid, List<string> yaml, List<string> deps)
            {
                var objects = Objects(yaml);
                var roots = Roots(objects);
                return new PrefabRecord
                {
                    guid = guid, path = file, name = Path.GetFileNameWithoutExtension(file), root = roots.FirstOrDefault(),
                    components = Components(file, objects), deps = deps,
                };
            }

            private void ReadBuildScenes()
            {
                var text = ReadText("ProjectSettings/EditorBuildSettings.asset");
                if (text == null)
                    return;
                BuildScene current = null;
                int index = 0;
                foreach (var raw in text.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("- enabled:", StringComparison.Ordinal))
                    {
                        current = new BuildScene { enabled = line.EndsWith("1", StringComparison.Ordinal) };
                        _index.project.buildScenes.Add(current);
                    }
                    else if (current != null && line.StartsWith("path:", StringComparison.Ordinal))
                    {
                        current.path = line.Substring(5).Trim();
                    }
                    else if (current != null && line.StartsWith("guid:", StringComparison.Ordinal))
                    {
                        current.guid = line.Substring(5).Trim();
                    }
                }
                foreach (var scene in _index.project.buildScenes)
                {
                    scene.index = scene.enabled ? index++ : -1;
                    var record = _index.scenes.FirstOrDefault(s => s.guid == scene.guid || s.path == scene.path);
                    if (record != null)
                        record.buildIndex = scene.index;
                    else if (!File.Exists(Path.Combine(_root, scene.path ?? "")))
                        _index.problems.Add($"Build scene {scene.path} is not in the project");
                }
            }

            // --- the framework

            private void ReadFramework()
            {
                const string package = "com.aizekhan.rpg-framework";
                var framework = _index.framework;
                var dependency = _index.project.packages.FirstOrDefault(p => p.name == package);
                string version = null;
                var embedded = ReadText($"Packages/{package}/package.json");
                if (embedded != null)
                    version = Regex.Match(embedded, "\"version\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(version) && dependency != null)
                {
                    var tag = Regex.Match(dependency.version, @"#v?(\d+\.\d+\.\d+[\w.\-]*)");
                    version = tag.Success ? tag.Groups[1].Value : dependency.version;
                }
                if (dependency == null && embedded == null)
                    return;
                framework.name = package;
                framework.version = version;
                foreach (var pair in _moduleUsers.OrderBy(p => p.Key, StringComparer.Ordinal))
                    framework.modules.Add(new ModuleUse { name = pair.Key, usedBy = pair.Value.ToList() });
            }

            private static bool IsFrameworkPath(string file) =>
                file.StartsWith("Packages/com.aizekhan.rpg-framework/", StringComparison.OrdinalIgnoreCase);

            // "RPGFramework.ActionCombat.Unity" and "using RPGFramework.ActionCombat" are both the ActionCombat module
            private void UseModule(string name, string user)
            {
                if (!name.StartsWith("RPGFramework.", StringComparison.Ordinal))
                    return;
                var parts = name.Substring(13).Split('.').ToList();
                if (parts.Count > 1 && (parts[parts.Count - 1] == "Runtime" || parts[parts.Count - 1] == "Unity" ||
                                        parts[parts.Count - 1] == "Editor" || parts[parts.Count - 1] == "Tests" ||
                                        parts[parts.Count - 1] == "PlayTests"))
                    parts.RemoveAt(parts.Count - 1);
                var module = parts[0] == "Tools" && parts.Count > 1 ? "Tools." + parts[1] : parts[0];
                if (!_moduleUsers.TryGetValue(module, out var users))
                    _moduleUsers[module] = users = new SortedSet<string>(StringComparer.Ordinal);
                users.Add(user);
            }

            // --- helpers

            private string Relative(string full) => full.Substring(_root.Length + 1).Replace('\\', '/');

            private string ReadText(string relative)
            {
                var path = Path.Combine(_root, relative);
                try
                {
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
                catch (IOException)
                {
                    return null;
                }
            }

            private void Count(string kind, bool indexed)
            {
                _coverage.TryGetValue(kind, out var entry);
                _coverage[kind] = (entry.Found + 1, entry.Indexed + (indexed ? 1 : 0));
            }

            // The "key": "value" pairs of a flat JSON object property (manifest dependencies)
            private static IEnumerable<(string, string)> JsonObjectEntries(string json, string property)
            {
                var block = Regex.Match(json, "\"" + property + "\"\\s*:\\s*\\{([^}]*)\\}");
                if (!block.Success)
                    yield break;
                foreach (Match pair in Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\""))
                    yield return (pair.Groups[1].Value, pair.Groups[2].Value);
            }

            private static IEnumerable<string> JsonArray(string json, string property)
            {
                var block = Regex.Match(json, "\"" + property + "\"\\s*:\\s*\\[([^\\]]*)\\]");
                if (!block.Success)
                    yield break;
                foreach (Match item in Regex.Matches(block.Groups[1].Value, "\"([^\"]*)\""))
                    yield return item.Groups[1].Value;
            }
        }
    }
}
