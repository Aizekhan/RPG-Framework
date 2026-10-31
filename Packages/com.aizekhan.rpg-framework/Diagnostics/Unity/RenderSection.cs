using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGFramework.Diagnostics.Unity
{
    /// <summary>
    /// How much a frame may draw. Values above it are marked OVER in the "Render" section; a game's tests can assert
    /// a scene against it with <see cref="RenderSection.Measure"/>. 0 turns a limit off.
    /// </summary>
    [Serializable]
    public sealed class RenderBudget
    {
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
            var budget = Budget ?? new RenderBudget();
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
                    if (!_meshTriangles.TryGetValue(mesh, out int count))
                        _meshTriangles[mesh] = count = TrianglesOf(mesh);
                    triangles += count;
                    meshes++;
                    string name = _modelOf(mesh) ?? mesh.name;
                    if (!_models.TryGetValue(name, out var model))
                        _models[name] = model = new RenderModel { Name = name };
                    model.Triangles += count;
                    model.Count++;
                    model.MeshTriangles = Math.Max(model.MeshTriangles, count);
                }
            }
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
