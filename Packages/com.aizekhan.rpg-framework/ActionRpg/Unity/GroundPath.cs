using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The way a walking character takes over the ground: around obstacles on the scene's NavMesh when there is one,
    /// straight to the point when the scene has none.
    /// </summary>
    public static class GroundPath
    {
        /// <summary>
        /// How far from the NavMesh a start or an end point may be and still count as on it (metres).
        /// </summary>
        public const float SnapDistance = 2f;

        private static readonly NavMeshPath Path = new NavMeshPath();

        /// <summary>
        /// Fills <paramref name="corners"/> with the points to walk through, the last one where the walk ends. With a
        /// NavMesh a point that cannot be reached is walked to as near as the mesh goes.
        /// </summary>
        /// <returns>False when the NavMesh has no way there (the start or the end is off it); corners are then empty.</returns>
        public static bool Find(Vector3 from, Vector3 to, List<Vector3> corners)
        {
            corners.Clear();
            // No NavMesh near the walker: the scene has none, walk straight
            if (!NavMesh.SamplePosition(from, out var start, SnapDistance, NavMesh.AllAreas))
            {
                corners.Add(to);
                return true;
            }
            if (!NavMesh.SamplePosition(to, out var end, SnapDistance, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, Path)
                || Path.status == NavMeshPathStatus.PathInvalid)
                return false;

            var points = Path.corners;
            // The first corner is where the walker stands
            for (int i = 1; i < points.Length; i++)
                corners.Add(points[i]);
            if (corners.Count == 0)
                corners.Add(end.position);
            return true;
        }
    }
}
