using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RPGFramework.ActionRpg.PlayTests
{
    /// <summary>
    /// <see cref="GroundPath"/>: straight without a NavMesh, around a wall on one baked at run time.
    /// </summary>
    public sealed class GroundPathPlayTests
    {
        private readonly List<Object> _made = new List<Object>();
        private NavMeshDataInstance _mesh;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_mesh.valid)
                _mesh.Remove();
            foreach (var made in _made)
                Object.Destroy(made);
            _made.Clear();
            yield return null;
        }

        [Test]
        public void NoNavMesh_WalksStraight()
        {
            var corners = new List<Vector3>();
            var to = new Vector3(5f, 0f, 3f);

            Assert.That(GroundPath.Find(Vector3.zero, to, corners), Is.True);
            Assert.That(corners, Is.EqualTo(new[] { to }));
        }

        [UnityTest]
        public IEnumerator NavMesh_GoesAroundAWall()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(10f, 3f, 1f);
            wall.transform.position = new Vector3(0f, 1.5f, 0f);
            _made.Add(floor);
            _made.Add(wall);

            var bounds = new Bounds(Vector3.zero, new Vector3(30f, 10f, 30f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);
            _made.Add(data);
            _mesh = NavMesh.AddNavMeshData(data);
            yield return null;

            var from = new Vector3(0f, 0f, -4f);
            var to = new Vector3(0f, 0f, 4f);
            var corners = new List<Vector3>();
            Assert.That(GroundPath.Find(from, to, corners), Is.True);

            Assert.That(corners.Count, Is.GreaterThan(1), "the way bends round the wall");
            float length = Vector3.Distance(from, corners[0]);
            for (int i = 1; i < corners.Count; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);
            Assert.That(length, Is.GreaterThan(Vector3.Distance(from, to) + 1f));
            Assert.That(Vector3.Distance(corners[corners.Count - 1], to), Is.LessThan(0.5f), "it ends at the point");

            // A point far off the mesh has no way there
            Assert.That(GroundPath.Find(from, new Vector3(0f, 50f, 0f), corners), Is.False);
            Assert.That(corners, Is.Empty);
        }
    }
}
