using System;
using System.Collections.Generic;

namespace RPGFramework.Tools.ProjectIndex
{
    /// <summary>
    /// A read-only map of a Unity project: its packages and build scenes, every asset with its GUID and references,
    /// scenes and prefabs with their components, scripts with their types and public members, assemblies and the
    /// framework modules the game uses. Written as JSON by <see cref="ProjectIndexer"/> for agents and tools that need
    /// to know what a project holds without opening it.
    /// </summary>
    [Serializable]
    public sealed class ProjectIndex
    {
        public const string Schema = "rpgframework.project-index/1";

        public string schema = Schema;
        public string scannedAt;
        public List<string> pathsScanned = new List<string>();
        public ProjectInfo project = new ProjectInfo();
        /// <summary>The RPG Framework package and the modules the game uses; empty name when it is not installed.</summary>
        public FrameworkInfo framework = new FrameworkInfo();
        public List<CoverageEntry> coverage = new List<CoverageEntry>();
        public List<AssetRecord> assets = new List<AssetRecord>();
        public List<SceneRecord> scenes = new List<SceneRecord>();
        public List<PrefabRecord> prefabs = new List<PrefabRecord>();
        public List<ScriptRecord> scripts = new List<ScriptRecord>();
        public List<AssemblyRecord> assemblies = new List<AssemblyRecord>();

        /// <summary>Broken references, missing scripts, build scenes that are not in the project.</summary>
        public List<string> problems = new List<string>();
    }

    [Serializable]
    public sealed class ProjectInfo
    {
        public string name;
        public string unityVersion;
        public string renderPipeline;
        public List<PackageRef> packages = new List<PackageRef>();
        public List<BuildScene> buildScenes = new List<BuildScene>();
        public RepoInfo repo = new RepoInfo();
    }

    [Serializable]
    public sealed class RepoInfo
    {
        public string branch;
        public string commit;
    }

    [Serializable]
    public sealed class PackageRef
    {
        public string name;
        public string version;
    }

    [Serializable]
    public sealed class BuildScene
    {
        public string path;
        public string guid;
        public bool enabled;
        public int index;
    }

    [Serializable]
    public sealed class FrameworkInfo
    {
        public string name;
        public string version;
        public List<ModuleUse> modules = new List<ModuleUse>();
    }

    [Serializable]
    public sealed class ModuleUse
    {
        public string name;

        /// <summary>The game's assemblies that reference the module or use its namespace.</summary>
        public List<string> usedBy = new List<string>();
    }

    [Serializable]
    public sealed class CoverageEntry
    {
        public string kind;
        public int found;
        public int indexed;
    }

    [Serializable]
    public sealed class AssetRecord
    {
        public string guid;
        public string path;
        public string name;

        /// <summary>Scene, Prefab, Script, Material, Texture, Model, AudioClip, ScriptableObject:&lt;Class&gt;...</summary>
        public string type;

        /// <summary>GUIDs this asset references (text-serialized assets only).</summary>
        public List<string> deps = new List<string>();
    }

    [Serializable]
    public sealed class SceneRecord
    {
        public string guid;
        public string path;
        public string name;

        /// <summary>Index in the build settings, -1 when not in the build.</summary>
        public int buildIndex = -1;

        public List<string> roots = new List<string>();
        public List<string> components = new List<string>();
        public List<string> deps = new List<string>();
    }

    [Serializable]
    public sealed class PrefabRecord
    {
        public string guid;
        public string path;
        public string name;
        public string root;
        public List<string> components = new List<string>();
        public List<string> deps = new List<string>();
    }

    [Serializable]
    public sealed class ScriptRecord
    {
        public string guid;
        public string path;
        public string assembly;
        public string @namespace;
        public List<TypeRecord> types = new List<TypeRecord>();
        public List<string> usings = new List<string>();
    }

    [Serializable]
    public sealed class TypeRecord
    {
        public string name;

        /// <summary>class, struct, interface, enum or record.</summary>
        public string kind;

        public List<string> baseTypes = new List<string>();

        /// <summary>The type's XML summary, if it has one.</summary>
        public string summary;

        public List<string> publicMembers = new List<string>();
    }

    [Serializable]
    public sealed class AssemblyRecord
    {
        public string name;
        public string path;
        public string guid;
        public List<string> references = new List<string>();
    }
}
