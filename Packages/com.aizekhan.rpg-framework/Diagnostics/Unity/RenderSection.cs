using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGFramework.Diagnostics.Unity
{
    /// <summary>
    /// The kind of device a <see cref="RenderBudget"/> is for. Each preset is a starting point for 30-60 FPS on that
    /// class of device in URP (<see cref="RenderBudget.For"/>); measure the game on its weakest target device and
    /// tune from there.
    /// </summary>
    public enum RenderPlatform
    {
        /// <summary>
        /// The budget's own numbers.
        /// </summary>
        Custom = 0,

        /// <summary>
        /// The preset of the platform the game runs on (<see cref="RenderBudget.PlatformOf"/>): a phone gets
        /// <see cref="Mobile"/>, WebGL <see cref="WebGL"/>, a console its class, the rest <see cref="Desktop"/>.
        /// </summary>
        Auto = 1,

        /// <summary>
        /// Weak Android phones (Mali-G52, Adreno 610, 2-3 GB): 150k triangles, 100 draw calls, 60 set-pass.
        /// </summary>
        LowEndMobile = 2,

        /// <summary>
        /// Mid-range phones (Snapdragon 7xx, iPhone 11-12): 300k triangles, 150 draw calls, 100 set-pass.
        /// </summary>
        Mobile = 3,

        /// <summary>
        /// Flagship phones and tablets (Snapdragon 8 Gen 2+, iPhone 14+, iPad Pro): 800k, 300, 150.
        /// </summary>
        HighEndMobile = 4,

        /// <summary>
        /// Standalone VR (Quest 2/3), both eyes with single-pass instanced: 500k, 150, 80.
        /// </summary>
        StandaloneVR = 5,

        /// <summary>
        /// A browser (WebGL 2 / WebGPU on an average laptop): 500k, 300, 120.
        /// </summary>
        WebGL = 6,

        /// <summary>
        /// Handheld consoles and PCs (Switch, Steam Deck): 600k, 500, 150.
        /// </summary>
        Handheld = 7,

        /// <summary>
        /// A mid-range PC (GTX 1060 / RX 580 class): 1.5M, 1500, 250 (the default numbers).
        /// </summary>
        Desktop = 8,

        /// <summary>
        /// Current consoles and gaming PCs (PS5, Xbox Series X, RTX 3060+): 5M, 3000, 500.
        /// </summary>
        HighEndDesktop = 9
    }

    /// <summary>
    /// How much a frame may draw. Values above it are marked OVER in the "Render" section; a game's tests can assert
    /// a scene against it with <see cref="RenderSection.Measure"/>. 0 turns a limit off. <see cref="Platform"/> picks a
    /// preset instead of the numbers (<see cref="For"/>, <see cref="Resolved"/>).
    /// </summary>
    [Serializable]
    public sealed class RenderBudget
    {
        [Tooltip("A device class preset (Auto: the platform the game runs on); Custom: the numbers below")]
        public RenderPlatform Platform = RenderPlatform.Custom;

        [Tooltip("Triangles of the meshes in the camera's view (shadows not counted; 0 = no limit)")]
        [Min(0)] public long Triangles = 1500000;

        [Tooltip("Draw calls of a frame (read in the Editor and development builds; 0 = no limit)")]
        [Min(0)] public long DrawCalls = 1500;

        [Tooltip("Set-pass calls (material switches) of a frame (read in the Editor and development builds; 0 = no limit)")]
        [Min(0)] public long SetPassCalls = 250;

        [Tooltip("Triangles of one model's mesh (one instance; 0 = no limit)")]
        [Min(0)] public long ModelTriangles;

        /// <summary>
        /// The value is over a limit (a limit of 0 is no limit).
        /// </summary>
        public static bool IsOver(long value, long limit) => limit > 0 && value > limit;

        /// <summary>
        /// The budget a frame is measured against: this one for <see cref="RenderPlatform.Custom"/>, otherwise the
        /// platform's preset (<see cref="For"/>).
        /// </summary>
        public RenderBudget Resolved() => Platform == RenderPlatform.Custom ? this : For(Platform);

        /// <summary>
        /// The preset of a device class (<see cref="RenderPlatform.Auto"/>: of the platform the game runs on). A preset
        /// limits one model's mesh to a tenth of the frame (<see cref="ModelTriangles"/>).
        /// </summary>
        public static RenderBudget For(RenderPlatform platform)
        {
            if (platform == RenderPlatform.Auto)
                platform = PlatformOf(Application.platform);
            switch (platform)
            {
                case RenderPlatform.LowEndMobile: return Preset(platform, 150000, 100, 60);
                case RenderPlatform.Mobile: return Preset(platform, 300000, 150, 100);
                case RenderPlatform.HighEndMobile: return Preset(platform, 800000, 300, 150);
                case RenderPlatform.StandaloneVR: return Preset(platform, 500000, 150, 80);
                case RenderPlatform.WebGL: return Preset(platform, 500000, 300, 120);
                case RenderPlatform.Handheld: return Preset(platform, 600000, 500, 150);
                case RenderPlatform.HighEndDesktop: return Preset(platform, 5000000, 3000, 500);
                case RenderPlatform.Custom: return new RenderBudget();
                default: return Preset(RenderPlatform.Desktop, 1500000, 1500, 250);
            }
        }

        /// <summary>
        /// The device class of a Unity platform: phones <see cref="RenderPlatform.Mobile"/> (a game for weak phones
        /// picks <see cref="RenderPlatform.LowEndMobile"/> itself), WebGL, Switch <see cref="RenderPlatform.Handheld"/>,
        /// PS5 and Xbox <see cref="RenderPlatform.HighEndDesktop"/>, the rest <see cref="RenderPlatform.Desktop"/>.
        /// </summary>
        public static RenderPlatform PlatformOf(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    return RenderPlatform.Mobile;
                case RuntimePlatform.WebGLPlayer:
                    return RenderPlatform.WebGL;
                case RuntimePlatform.Switch:
                    return RenderPlatform.Handheld;
                case RuntimePlatform.PS5:
                case RuntimePlatform.GameCoreXboxSeries:
                    return RenderPlatform.HighEndDesktop;
                default:
                    return RenderPlatform.Desktop;
            }
        }

        private static RenderBudget Preset(RenderPlatform platform, long triangles, long drawCalls, long setPassCalls) =>
            new RenderBudget
            {
                Platform = platform,
                Triangles = triangles,
                DrawCalls = drawCalls,
                SetPassCalls = setPassCalls,
                ModelTriangles = triangles / 10
            };
    }

    /// <summary>
    /// One measure of a frame: what is in the camera's view (counted on the CPU, works without graphics) and, where
    /// the profiler's render counters run, what the GPU drew last frame.
    /// </summary>
    public readonly struct RenderSample
    {
        public RenderSample(long triangles, int meshes, bool hasGpu, long gpuTriangles, long drawCalls, long setPassCalls, long batches)
        {
            Triangles = triangles;
            Meshes = meshes;
            HasGpu = hasGpu;
            GpuTriangles = gpuTriangles;
            DrawCalls = drawCalls;
            SetPassCalls = setPassCalls;
            Batches = batches;
        }

        /// <summary>Triangles of the meshes inside the camera's view.</summary>
        public long Triangles { get; }

        /// <summary>Mesh renderers inside the camera's view.</summary>
        public int Meshes { get; }

        /// <summary>The GPU numbers were read (the Editor or a development build, with graphics).</summary>
        public bool HasGpu { get; }

        /// <summary>Triangles the GPU drew last frame, shadows and every camera included.</summary>
        public long GpuTriangles { get; }

        public long DrawCalls { get; }
        public long SetPassCalls { get; }
        public long Batches { get; }

        public override string ToString() =>
            $"{Triangles} triangles in {Meshes} meshes" + (HasGpu ? $", GPU {GpuTriangles} triangles, {DrawCalls} draw calls, {SetPassCalls} set-pass" : "");
    }

    /// <summary>
    /// What one model adds to the view: triangles in all, instances, and the triangles of its heaviest mesh.
    /// </summary>
    public sealed class RenderModel
    {
        public string Name { get; internal set; }
        public long Triangles { get; internal set; }
        public int Count { get; internal set; }

        /// <summary>Triangles of its heaviest single mesh (what <see cref="RenderBudget.ModelTriangles"/> limits).</summary>
        public long MeshTriangles { get; internal set; }

        public override string ToString() => $"{Name} x{Count}: {Triangles}";
    }

    /// <summary>
    /// The "Render" diagnostics section: the triangles and meshes in the camera's view, by model (heaviest first),
    /// and, where the profiler's render counters run, the GPU's triangles, draw calls, set-pass calls and batches;
    /// values over the <see cref="Budget"/> are marked OVER. Counts mesh renderers and skinned mesh renderers of every
    /// loaded scene and of the anchor's scene (DontDestroyOnLoad). Dispose it to stop the profiler recorders.
    /// A statically batched renderer counts only its own part of the combined mesh (<see cref="ModelOfBatched"/> names it).
    /// </summary>
    public sealed class RenderSection : IDiagnosticsSection, IDisposable
    {
        private readonly Func<Camera> _camera;
        private readonly GameObject _anchor;
        private readonly Plane[] _planes = new Plane[6];
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly Dictionary<Mesh, int> _meshTriangles = new Dictionary<Mesh, int>();
        private readonly Dictionary<string, RenderModel> _models = new Dictionary<string, RenderModel>();
        private readonly List<RenderModel> _heaviest = new List<RenderModel>();
        private ProfilerRecorder _gpuTriangles, _drawCalls, _setPassCalls, _batches;
        private Func<Mesh, string> _modelOf;
        private Func<Renderer, string> _modelOfBatched;

        /// <param name="camera">The camera whose view is counted (null or returning null: <c>Camera.main</c>).</param>
        /// <param name="budget">The limits marked OVER (null: the defaults).</param>
        /// <param name="anchor">An object of the game whose scene is counted too, e.g. a host kept with DontDestroyOnLoad.</param>
        /// <param name="modelOf">The model a mesh belongs to (null: the mesh's name).</param>
        public RenderSection(Func<Camera> camera = null, RenderBudget budget = null, GameObject anchor = null, Func<Mesh, string> modelOf = null)
        {
            _camera = camera;
            Budget = budget ?? new RenderBudget();
            _anchor = anchor;
            ModelOf = modelOf;
            ModelOfBatched = null;
            _gpuTriangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        }

        public string Title => "Render";

        public RenderBudget Budget { get; set; }

        /// <summary>
        /// The model a mesh belongs to, for the breakdown (null: the mesh's name). An editor test can map a mesh to
        /// its asset with AssetDatabase.
        /// </summary>
        public Func<Mesh, string> ModelOf
        {
            get => _modelOf;
            set => _modelOf = value ?? (mesh => mesh.name);
        }

        /// <summary>
        /// The model a statically batched renderer belongs to. Its mesh at run time is the scene's "Combined Mesh", so
        /// <see cref="ModelOf"/> cannot name it (null: the renderer's object name without Unity's " (N)" copy suffix).
        /// </summary>
        public Func<Renderer, string> ModelOfBatched
        {
            get => _modelOfBatched;
            set => _modelOfBatched = value ?? ObjectName;
        }

        /// <summary>
        /// Models listed in the section (heaviest first).
        /// </summary>
        public int TopModels { get; set; } = 6;

        /// <summary>
        /// The models of the last <see cref="Measure"/>, heaviest first.
        /// </summary>
        public IReadOnlyList<RenderModel> Heaviest => _heaviest;

        /// <summary>
        /// Counts the frame as it is now.
        /// </summary>
        public RenderSample Measure()
        {
            _models.Clear();
            _heaviest.Clear();
            long triangles = 0;
            int meshes = 0;
            var camera = _camera?.Invoke() ?? Camera.main;
            if (camera != null)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _planes);
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    Count(SceneManager.GetSceneAt(i), ref triangles, ref meshes);
                if (_anchor != null && _anchor.scene.IsValid() && !IsLoaded(_anchor.scene))
                    Count(_anchor.scene, ref triangles, ref meshes);
            }
            _heaviest.AddRange(_models.Values);
            _heaviest.Sort((a, b) => b.Triangles.CompareTo(a.Triangles));

            bool gpu = _gpuTriangles.Valid && _gpuTriangles.LastValue > 0;
            return new RenderSample(triangles, meshes, gpu, gpu ? _gpuTriangles.LastValue : 0, gpu ? _drawCalls.LastValue : 0,
                gpu ? _setPassCalls.LastValue : 0, gpu ? _batches.LastValue : 0);
        }

        public void Write(StringBuilder output)
        {
            var sample = Measure();
            var budget = (Budget ?? new RenderBudget()).Resolved();
            if (budget.Platform != RenderPlatform.Custom)
                output.AppendLine($"Budget: {budget.Platform}");
            output.AppendLine($"In view {sample.Triangles:N0} triangles, {sample.Meshes} meshes{Over(sample.Triangles, budget.Triangles)}");
            if (sample.HasGpu)
                output.AppendLine($"GPU {sample.GpuTriangles:N0} triangles with shadows, {sample.DrawCalls} draw calls" +
                                  $"{Over(sample.DrawCalls, budget.DrawCalls)}, {sample.SetPassCalls} set-pass" +
                                  $"{Over(sample.SetPassCalls, budget.SetPassCalls)}, {sample.Batches} batches");
            else
                output.AppendLine("GPU counters: not here (they run in the Editor and development builds)");
            for (int i = 0; i < _heaviest.Count && i < TopModels; i++)
            {
                var model = _heaviest[i];
                output.AppendLine($"  {model.Name} x{model.Count}: {model.Triangles:N0}" +
                                  (RenderBudget.IsOver(model.MeshTriangles, budget.ModelTriangles)
                                      ? $"  OVER ({model.MeshTriangles:N0} a mesh, budget {budget.ModelTriangles:N0})" : ""));
            }
        }

        /// <summary>
        /// Triangles of a mesh: its sub-meshes of triangle topology.
        /// </summary>
        public static int TrianglesOf(Mesh mesh)
        {
            if (mesh == null)
                return 0;
            long count = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) == MeshTopology.Triangles)
                    count += mesh.GetIndexCount(sub) / 3;
            }
            return (int)Math.Min(count, int.MaxValue);
        }

        /// <summary>
        /// Triangles a renderer draws of its mesh. A statically batched renderer draws only its own sub-meshes of the
        /// scene's combined mesh (from <c>MeshRenderer.subMeshStartIndex</c>, one per material), not the whole of it.
        /// </summary>
        public static int TrianglesOf(Renderer renderer, Mesh mesh)
        {
            if (mesh == null)
                return 0;
            if (!(renderer is MeshRenderer batched) || !batched.isPartOfStaticBatch)
                return TrianglesOf(mesh);
            int first = batched.subMeshStartIndex;
            int end = Math.Min(mesh.subMeshCount, first + Math.Max(1, batched.sharedMaterials.Length));
            long count = 0;
            for (int sub = Math.Max(0, first); sub < end; sub++)
            {
                if (mesh.GetTopology(sub) == MeshTopology.Triangles)
                    count += mesh.GetIndexCount(sub) / 3;
            }
            return (int)Math.Min(count, int.MaxValue);
        }

        public void Dispose()
        {
            _gpuTriangles.Dispose();
            _drawCalls.Dispose();
            _setPassCalls.Dispose();
            _batches.Dispose();
        }

        private static string Over(long value, long limit) => RenderBudget.IsOver(value, limit) ? $"  OVER (budget {limit:N0})" : "";

        private static bool IsLoaded(Scene scene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i) == scene)
                    return true;
            }
            return false;
        }

        private void Count(Scene scene, ref long triangles, ref int meshes)
        {
            if (!scene.isLoaded)
                return;
            scene.GetRootGameObjects(_roots);
            foreach (var root in _roots)
            {
                root.GetComponentsInChildren(false, _renderers);
                foreach (var renderer in _renderers)
                {
                    if (!renderer.enabled || renderer.forceRenderingOff)
                        continue;
                    var mesh = MeshOf(renderer);
                    if (mesh == null || !GeometryUtility.TestPlanesAABB(_planes, renderer.bounds))
                        continue;
                    bool batched = renderer.isPartOfStaticBatch;
                    int count;
                    if (batched)
                        count = TrianglesOf(renderer, mesh);
                    else if (!_meshTriangles.TryGetValue(mesh, out count))
                        _meshTriangles[mesh] = count = TrianglesOf(mesh);
                    triangles += count;
                    meshes++;
                    string name = (batched ? _modelOfBatched(renderer) : _modelOf(mesh)) ?? mesh.name;
                    if (!_models.TryGetValue(name, out var model))
                        _models[name] = model = new RenderModel { Name = name };
                    model.Triangles += count;
                    model.Count++;
                    model.MeshTriangles = Math.Max(model.MeshTriangles, count);
                }
            }
        }

        private static string ObjectName(Renderer renderer)
        {
            string name = renderer.gameObject.name;
            int copy = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (copy > 0 && name.EndsWith(")", StringComparison.Ordinal))
            {
                for (int i = copy + 2; i < name.Length - 1; i++)
                {
                    if (!char.IsDigit(name[i]))
                        return name;
                }
                if (copy + 2 < name.Length - 1)
                    return name.Substring(0, copy);
            }
            return name;
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh;
            if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
                return filter.sharedMesh;
            return null;
        }
    }
}
