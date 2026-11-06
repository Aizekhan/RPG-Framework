using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RPGFramework.Diagnostics.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.Diagnostics.Tests
{
    /// <summary>
    /// The "Render" section counts what the camera sees on the CPU (no graphics needed), by model, and marks values
    /// over the budget (MythRunner: Meshy models and dressed biomes must not lag).
    /// </summary>
    [TestFixture]
    public sealed class RenderSectionTests
    {
        private const string TestCube = "TestCube";

        private readonly List<Object> _made = new List<Object>();
        private Camera _camera;
        private Mesh _cubeMesh;
        private RenderSection _section;

        [SetUp]
        public void SetUp()
        {
            _camera = Make(new GameObject("RenderTestCamera")).AddComponent<Camera>();
            _camera.transform.position = Vector3.zero;
            _camera.transform.rotation = Quaternion.identity;
            _camera.aspect = 1f; // without a screen (batch mode) the aspect would come from a 0-size target
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            // Only the test's cubes are named: whatever else the test scene holds keeps its own names
            _section = new RenderSection(() => _camera, new RenderBudget(), modelOf: mesh => mesh == _cubeMesh ? TestCube : mesh.name);
        }

        [TearDown]
        public void TearDown()
        {
            _section.Dispose();
            foreach (var made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }
            _made.Clear();
        }

        [Test]
        public void ACubeInView_IsCounted_ACubeBehindTheCamera_IsNot()
        {
            Cube(new Vector3(0f, 0f, 5f));
            Cube(new Vector3(0f, 0f, -5f));

            var before = _section.Measure();
            var cubes = _section.Heaviest.Single(m => m.Name == TestCube);

            Assert.That(cubes.Count, Is.EqualTo(1), "the cube behind the camera is not in view");
            Assert.That(cubes.Triangles, Is.EqualTo(12));
            Assert.That(RenderSection.TrianglesOf(_cubeMesh), Is.EqualTo(12));
            Assert.That(before.Triangles, Is.GreaterThanOrEqualTo(12));
        }

        [Test]
        public void Models_AreGrouped_HeaviestFirst()
        {
            Cube(new Vector3(-1f, 0f, 6f));
            Cube(new Vector3(1f, 0f, 6f));
            var sphere = Make(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            sphere.transform.position = new Vector3(0f, 0f, 8f);

            _section.Measure();

            var cubes = _section.Heaviest.Single(m => m.Name == TestCube);
            Assert.That(cubes.Count, Is.EqualTo(2));
            Assert.That(cubes.Triangles, Is.EqualTo(24));
            Assert.That(cubes.MeshTriangles, Is.EqualTo(12));
            for (int i = 1; i < _section.Heaviest.Count; i++)
                Assert.That(_section.Heaviest[i - 1].Triangles, Is.GreaterThanOrEqualTo(_section.Heaviest[i].Triangles));
            Assert.That(_section.Heaviest.ToList().IndexOf(cubes), Is.GreaterThan(0), "the sphere has more triangles than two cubes");
        }

        [Test]
        public void ValuesOverTheBudget_AreMarkedOver()
        {
            Cube(new Vector3(0f, 0f, 5f));
            _section.Budget = new RenderBudget { Triangles = 1, ModelTriangles = 10 };

            string text = Write();

            StringAssert.Contains("OVER (budget 1)", text.Split('\n')[0]);
            StringAssert.Contains($"{TestCube} x1: 12  OVER (12 a mesh, budget 10)", text);
        }

        [Test]
        public void WithinTheBudget_NothingIsOver_AndNoLimitIsNoLimit()
        {
            Cube(new Vector3(0f, 0f, 5f));
            _section.Budget = new RenderBudget { Triangles = 0, DrawCalls = 0, SetPassCalls = 0, ModelTriangles = 12 };

            Assert.That(Write(), Does.Not.Contain("OVER"));
            Assert.That(RenderBudget.IsOver(13, 0), Is.False);
            Assert.That(RenderBudget.IsOver(13, 12), Is.True);
        }

        // A budget per device class, not only phones (The Bloodline Hunt asked what a phone may draw)
        [Test]
        public void APlatformPreset_ReplacesTheNumbers_AndEachClassHasOne()
        {
            Cube(new Vector3(0f, 0f, 5f));
            _section.Budget = new RenderBudget { Platform = RenderPlatform.Mobile, Triangles = 1 };

            string text = Write();

            StringAssert.StartsWith("Budget: Mobile", text);
            Assert.That(text, Does.Not.Contain("OVER"), "the preset, not Triangles = 1");
            var mobile = RenderBudget.For(RenderPlatform.Mobile);
            Assert.That((mobile.Triangles, mobile.DrawCalls, mobile.SetPassCalls, mobile.ModelTriangles), Is.EqualTo((300000L, 150L, 100L, 30000L)));
            Assert.That(RenderBudget.For(RenderPlatform.Desktop).Triangles, Is.EqualTo(new RenderBudget().Triangles), "Desktop = the defaults");

            var classes = new[]
            {
                RenderPlatform.LowEndMobile, RenderPlatform.Mobile, RenderPlatform.HighEndMobile, RenderPlatform.StandaloneVR,
                RenderPlatform.WebGL, RenderPlatform.Handheld, RenderPlatform.Desktop, RenderPlatform.HighEndDesktop
            };
            foreach (var platform in classes)
            {
                var budget = RenderBudget.For(platform);
                Assert.That(budget.Platform, Is.EqualTo(platform));
                Assert.That(budget.Triangles, Is.GreaterThan(0));
                Assert.That(budget.Resolved(), Is.Not.Null);
            }
            Assert.That(RenderBudget.For(RenderPlatform.LowEndMobile).Triangles, Is.LessThan(RenderBudget.For(RenderPlatform.HighEndDesktop).Triangles));
            Assert.That(RenderBudget.PlatformOf(RuntimePlatform.Android), Is.EqualTo(RenderPlatform.Mobile));
            Assert.That(RenderBudget.PlatformOf(RuntimePlatform.WebGLPlayer), Is.EqualTo(RenderPlatform.WebGL));
            Assert.That(RenderBudget.PlatformOf(RuntimePlatform.WindowsPlayer), Is.EqualTo(RenderPlatform.Desktop));
            Assert.That(RenderBudget.For(RenderPlatform.Auto).Platform, Is.EqualTo(RenderBudget.PlatformOf(Application.platform)));
            var custom = new RenderBudget { Triangles = 7 };
            Assert.That(custom.Resolved(), Is.SameAs(custom));
        }

        [Test]
        public void ADisabledRenderer_IsNotCounted()
        {
            Cube(new Vector3(0f, 0f, 5f)).GetComponent<Renderer>().enabled = false;

            _section.Measure();

            Assert.That(_section.Heaviest.Any(m => m.Name == TestCube), Is.False);
        }

        [Test]
        public void WithoutGpuCounters_TheSectionSaysWhereTheyRun()
        {
            var sample = _section.Measure();

            if (!sample.HasGpu)
                StringAssert.Contains("GPU counters: not here", Write());
            else
                StringAssert.Contains("draw calls", Write());
        }

        [Test]
        public void TheOverlaysToggleKey_IsSettableFromCode()
        {
            var overlay = Make(new GameObject("Overlay")).AddComponent<DiagnosticsOverlay>();

            overlay.ToggleKey = KeyCode.F4;
            Assert.That(overlay.ToggleKey, Is.EqualTo(KeyCode.F4));
            overlay.ToggleKey = KeyCode.Alpha5;
            Assert.That(overlay.ToggleKey, Is.EqualTo(KeyCode.Alpha5));
            overlay.ToggleKey = KeyCode.None;
            Assert.That(overlay.ToggleKey, Is.EqualTo(KeyCode.None));
        }

        private string Write()
        {
            var output = new StringBuilder();
            _section.Write(output);
            return output.ToString();
        }

        private GameObject Cube(Vector3 position)
        {
            var cube = Make(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position;
            return cube;
        }

        private T Make<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }
    }
}
