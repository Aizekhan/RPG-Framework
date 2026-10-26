using UnityEngine;

namespace RPGFramework.Diagnostics.Unity
{
    /// <summary>
    /// Debug drawing utilities for visualizing game state.
    /// Wraps Unity's Debug.DrawLine/DrawRay with additional features.
    /// </summary>
    public static class DebugDraw
    {
        /// <summary>
        /// Draw a circle in 3D space.
        /// </summary>
        public static void DrawCircle(Vector3 center, float radius, Color color, float duration = 0f, int segments = 32)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                UnityEngine.Debug.DrawLine(prevPoint, newPoint, color, duration);
                prevPoint = newPoint;
            }
        }

        /// <summary>
        /// Draw a sphere wireframe.
        /// </summary>
        public static void DrawSphere(Vector3 center, float radius, Color color, float duration = 0f, int segments = 16)
        {
            // Draw 3 circles for XY, XZ, YZ planes
            DrawCircleXY(center, radius, color, duration, segments);
            DrawCircleXZ(center, radius, color, duration, segments);
            DrawCircleYZ(center, radius, color, duration, segments);
        }

        /// <summary>
        /// Draw a circle in XY plane.
        /// </summary>
        public static void DrawCircleXY(Vector3 center, float radius, Color color, float duration = 0f, int segments = 32)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                UnityEngine.Debug.DrawLine(prevPoint, newPoint, color, duration);
                prevPoint = newPoint;
            }
        }

        /// <summary>
        /// Draw a circle in XZ plane (horizontal circle).
        /// </summary>
        public static void DrawCircleXZ(Vector3 center, float radius, Color color, float duration = 0f, int segments = 32)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                UnityEngine.Debug.DrawLine(prevPoint, newPoint, color, duration);
                prevPoint = newPoint;
            }
        }

        /// <summary>
        /// Draw a circle in YZ plane.
        /// </summary>
        public static void DrawCircleYZ(Vector3 center, float radius, Color color, float duration = 0f, int segments = 32)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(0, radius, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(0, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                UnityEngine.Debug.DrawLine(prevPoint, newPoint, color, duration);
                prevPoint = newPoint;
            }
        }

        /// <summary>
        /// Draw a box wireframe.
        /// </summary>
        public static void DrawBox(Vector3 center, Vector3 size, Color color, float duration = 0f)
        {
            Vector3 halfSize = size * 0.5f;

            // Bottom face
            Vector3 p0 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p1 = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p2 = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            Vector3 p3 = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);

            // Top face
            Vector3 p4 = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p5 = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p6 = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
            Vector3 p7 = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

            // Draw bottom face
            UnityEngine.Debug.DrawLine(p0, p1, color, duration);
            UnityEngine.Debug.DrawLine(p1, p2, color, duration);
            UnityEngine.Debug.DrawLine(p2, p3, color, duration);
            UnityEngine.Debug.DrawLine(p3, p0, color, duration);

            // Draw top face
            UnityEngine.Debug.DrawLine(p4, p5, color, duration);
            UnityEngine.Debug.DrawLine(p5, p6, color, duration);
            UnityEngine.Debug.DrawLine(p6, p7, color, duration);
            UnityEngine.Debug.DrawLine(p7, p4, color, duration);

            // Draw vertical lines
            UnityEngine.Debug.DrawLine(p0, p4, color, duration);
            UnityEngine.Debug.DrawLine(p1, p5, color, duration);
            UnityEngine.Debug.DrawLine(p2, p6, color, duration);
            UnityEngine.Debug.DrawLine(p3, p7, color, duration);
        }

        /// <summary>
        /// Draw a cross at position.
        /// </summary>
        public static void DrawCross(Vector3 position, float size, Color color, float duration = 0f)
        {
            float halfSize = size * 0.5f;

            UnityEngine.Debug.DrawLine(position + Vector3.left * halfSize, position + Vector3.right * halfSize, color, duration);
            UnityEngine.Debug.DrawLine(position + Vector3.up * halfSize, position + Vector3.down * halfSize, color, duration);
            UnityEngine.Debug.DrawLine(position + Vector3.forward * halfSize, position + Vector3.back * halfSize, color, duration);
        }

        /// <summary>
        /// Draw an arrow.
        /// </summary>
        public static void DrawArrow(Vector3 start, Vector3 direction, Color color, float duration = 0f, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f)
        {
            Vector3 end = start + direction;
            UnityEngine.Debug.DrawLine(start, end, color, duration);

            if (direction != Vector3.zero)
            {
                Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
                Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;

                UnityEngine.Debug.DrawLine(end, end + right * arrowHeadLength, color, duration);
                UnityEngine.Debug.DrawLine(end, end + left * arrowHeadLength, color, duration);
            }
        }
    }
}
