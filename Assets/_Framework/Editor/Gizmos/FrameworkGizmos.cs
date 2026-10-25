using UnityEngine;
using UnityEditor;

namespace RPGFramework.Editor.Gizmos
{
    /// <summary>
    /// Gizmos for visualizing Framework elements in the Scene view.
    /// </summary>
    public static class FrameworkGizmos
    {
        /// <summary>
        /// Draw a quest marker gizmo.
        /// </summary>
        public static void DrawQuestMarker(Vector3 position, Color color, float size = 1f)
        {
            UnityEngine.Gizmos.color = color;

            // Draw exclamation mark
            Vector3 top = position + Vector3.up * size;
            Vector3 bottom = position + Vector3.up * (size * 0.3f);

            UnityEngine.Gizmos.DrawLine(top, bottom);
            UnityEngine.Gizmos.DrawSphere(position + Vector3.up * (size * 0.15f), size * 0.1f);

            // Draw circle base
            DrawWireCircle(position, size * 0.5f, color);
        }

        /// <summary>
        /// Draw an interaction radius gizmo.
        /// </summary>
        public static void DrawInteractionRadius(Vector3 center, float radius, Color color)
        {
            UnityEngine.Gizmos.color = color;
            DrawWireCircle(center, radius, color);

            // Draw cross
            UnityEngine.Gizmos.DrawLine(center + Vector3.left * radius, center + Vector3.right * radius);
            UnityEngine.Gizmos.DrawLine(center + Vector3.forward * radius, center + Vector3.back * radius);
        }

        /// <summary>
        /// Draw a loot spawn point gizmo.
        /// </summary>
        public static void DrawLootSpawnPoint(Vector3 position, float size = 0.5f)
        {
            Color goldColor = new Color(1f, 0.84f, 0f);
            UnityEngine.Gizmos.color = goldColor;

            // Draw chest-like shape
            Vector3 basePos = position;
            Vector3 topPos = position + Vector3.up * size;

            UnityEngine.Gizmos.DrawWireCube(basePos + Vector3.up * (size * 0.25f), new Vector3(size, size * 0.5f, size * 0.7f));
            UnityEngine.Gizmos.DrawSphere(topPos, size * 0.15f);

            DrawWireCircle(position, size * 0.3f, goldColor);
        }

        /// <summary>
        /// Draw a crafting station gizmo.
        /// </summary>
        public static void DrawCraftingStation(Vector3 position, float size = 1f)
        {
            Color craftColor = new Color(0.6f, 0.4f, 0.8f);
            UnityEngine.Gizmos.color = craftColor;

            // Draw anvil-like shape
            Vector3 top = position + Vector3.up * size;
            Vector3 middle = position + Vector3.up * (size * 0.6f);
            Vector3 bottom = position;

            UnityEngine.Gizmos.DrawWireCube(middle, new Vector3(size * 0.8f, size * 0.2f, size * 0.5f));
            UnityEngine.Gizmos.DrawWireCube(bottom + Vector3.up * (size * 0.15f), new Vector3(size * 0.4f, size * 0.3f, size * 0.3f));

            DrawWireCircle(position, size * 0.6f, craftColor);
        }

        /// <summary>
        /// Draw a dialogue trigger gizmo.
        /// </summary>
        public static void DrawDialogueTrigger(Vector3 position, float radius, Color color)
        {
            UnityEngine.Gizmos.color = color;

            // Draw speech bubble shape
            Vector3 bubbleCenter = position + Vector3.up * radius;
            UnityEngine.Gizmos.DrawWireSphere(bubbleCenter, radius * 0.5f);

            // Draw tail
            Vector3 tailStart = bubbleCenter + Vector3.down * (radius * 0.4f);
            Vector3 tailEnd = position + Vector3.up * (radius * 0.2f);
            UnityEngine.Gizmos.DrawLine(tailStart, tailEnd);

            DrawWireCircle(position, radius, color);
        }

        /// <summary>
        /// Draw a faction territory boundary.
        /// </summary>
        public static void DrawFactionTerritory(Vector3 center, float radius, Color factionColor)
        {
            UnityEngine.Gizmos.color = factionColor;

            // Draw territory circle
            DrawWireCircle(center, radius, factionColor);

            // Draw inner circle
            DrawWireCircle(center, radius * 0.9f, factionColor);

            // Draw faction symbol (simplified flag)
            Vector3 flagPos = center + Vector3.up * (radius * 0.5f);
            UnityEngine.Gizmos.DrawLine(flagPos, flagPos + Vector3.up * (radius * 0.5f));
            UnityEngine.Gizmos.DrawLine(flagPos + Vector3.up * (radius * 0.5f),
                                       flagPos + Vector3.up * (radius * 0.3f) + Vector3.right * (radius * 0.2f));
        }

        /// <summary>
        /// Draw a combat range indicator.
        /// </summary>
        public static void DrawCombatRange(Vector3 origin, Vector3 target, float range, Color color)
        {
            UnityEngine.Gizmos.color = color;

            // Draw line to target
            UnityEngine.Gizmos.DrawLine(origin, target);

            // Draw range circle
            DrawWireCircle(origin, range, color);

            // Draw arrow at target
            Vector3 direction = (target - origin).normalized;
            Vector3 arrowHead = target;
            Vector3 arrowLeft = arrowHead - direction * 0.3f + Vector3.Cross(direction, Vector3.up) * 0.15f;
            Vector3 arrowRight = arrowHead - direction * 0.3f - Vector3.Cross(direction, Vector3.up) * 0.15f;

            UnityEngine.Gizmos.DrawLine(arrowHead, arrowLeft);
            UnityEngine.Gizmos.DrawLine(arrowHead, arrowRight);
        }

        /// <summary>
        /// Draw a waypoint path.
        /// </summary>
        public static void DrawWaypointPath(Vector3[] waypoints, Color color)
        {
            if (waypoints == null || waypoints.Length < 2) return;

            UnityEngine.Gizmos.color = color;

            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                UnityEngine.Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
                UnityEngine.Gizmos.DrawSphere(waypoints[i], 0.2f);
            }

            // Draw last waypoint
            UnityEngine.Gizmos.DrawSphere(waypoints[waypoints.Length - 1], 0.2f);
        }

        /// <summary>
        /// Helper method to draw a wire circle in 3D space.
        /// </summary>
        private static void DrawWireCircle(Vector3 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.up, radius);
        }

        /// <summary>
        /// Draw a trigger zone with height.
        /// </summary>
        public static void DrawTriggerZone(Vector3 center, float radius, float height, Color color)
        {
            UnityEngine.Gizmos.color = color;

            // Draw bottom circle
            DrawWireCircle(center, radius, color);

            // Draw top circle
            Vector3 topCenter = center + Vector3.up * height;
            DrawWireCircle(topCenter, radius, color);

            // Draw vertical lines
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (var dir in directions)
            {
                Vector3 bottom = center + dir * radius;
                Vector3 top = topCenter + dir * radius;
                UnityEngine.Gizmos.DrawLine(bottom, top);
            }
        }

        /// <summary>
        /// Draw ability cast range indicator.
        /// </summary>
        public static void DrawAbilityCastRange(Vector3 casterPosition, float minRange, float maxRange, Color color)
        {
            Color minColor = new Color(color.r, color.g, color.b, 0.3f);

            // Draw min range (dead zone)
            if (minRange > 0)
            {
                Handles.color = minColor;
                Handles.DrawSolidDisc(casterPosition, Vector3.up, minRange);
            }

            // Draw max range
            UnityEngine.Gizmos.color = color;
            DrawWireCircle(casterPosition, maxRange, color);

            // Draw effective range ring
            if (minRange > 0)
            {
                DrawWireCircle(casterPosition, minRange, color);
            }
        }
    }
}
