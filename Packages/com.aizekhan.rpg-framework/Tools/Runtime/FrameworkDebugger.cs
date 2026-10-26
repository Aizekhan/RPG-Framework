using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RPGFramework.ECS;
using UnityEngine;

namespace RPGFramework.Tools
{
    /// <summary>
    /// Runtime debugger for inspecting Framework state and entities.
    /// Attach to a GameObject to enable debugging features.
    /// </summary>
    public class FrameworkDebugger : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool _enableDebugLogging = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField] private bool _showOnScreen = true;

        [Header("Entity Manager")]
        [Tooltip("Assign your IEntityManager implementation here")]
        public MonoBehaviour entityManagerBehaviour;

        private IEntityManager _entityManager;
        private bool _isVisible = false;
        private Vector2 _scrollPosition;
        private int _selectedEntityId = -1;
        private readonly Dictionary<int, bool> _entityFoldouts = new Dictionary<int, bool>();

        void Start()
        {
            if (entityManagerBehaviour != null && entityManagerBehaviour is IEntityManager manager)
            {
                _entityManager = manager;
                Debug.Log("[FrameworkDebugger] Initialized with EntityManager");
            }
            else
            {
                Debug.LogWarning("[FrameworkDebugger] No EntityManager assigned! Debugger won't work.");
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _isVisible = !_isVisible;
            }
        }

        void OnGUI()
        {
            if (!_showOnScreen || !_isVisible || _entityManager == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Framework Debugger", GUI.skin.box);
            GUILayout.Label($"Press {_toggleKey} to toggle");

            DrawEntityList();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawEntityList()
        {
            var entities = _entityManager.GetAllEntities().ToList();
            GUILayout.Label($"Total Entities: {entities.Count}", GUI.skin.box);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(Screen.height - 100));

            foreach (var entityId in entities)
            {
                DrawEntity(entityId);
            }

            GUILayout.EndScrollView();
        }

        private void DrawEntity(int entityId)
        {
            if (!_entityFoldouts.ContainsKey(entityId))
            {
                _entityFoldouts[entityId] = false;
            }

            GUILayout.BeginHorizontal("box");

            if (GUILayout.Button(_entityFoldouts[entityId] ? "▼" : "►", GUILayout.Width(30)))
            {
                _entityFoldouts[entityId] = !_entityFoldouts[entityId];
            }

            GUILayout.Label($"Entity {entityId}");

            if (GUILayout.Button("Destroy", GUILayout.Width(70)))
            {
                _entityManager.DestroyEntity(entityId);
                _entityFoldouts.Remove(entityId);
            }

            GUILayout.EndHorizontal();

            if (_entityFoldouts[entityId])
            {
                GUILayout.BeginVertical("box");
                DrawEntityComponents(entityId);
                GUILayout.EndVertical();
            }
        }

        private void DrawEntityComponents(int entityId)
        {
            // Note: This is a simplified version. In a full implementation,
            // you'd need reflection or a component registry to enumerate all components.
            GUILayout.Label("Components:", EditorStyles.boldLabel);
            GUILayout.Label("(Component inspection requires reflection or component registry)");

            // Example: If you know specific component types, you can check for them:
            // if (_entityManager.HasComponent<HealthComponent>(entityId))
            // {
            //     var health = _entityManager.GetComponent<HealthComponent>(entityId);
            //     GUILayout.Label($"Health: {health.CurrentValue}/{health.MaxValue}");
            // }
        }

        /// <summary>
        /// Logs all entities and their components to the console.
        /// </summary>
        public void LogAllEntities()
        {
            if (_entityManager == null)
            {
                Debug.LogWarning("[FrameworkDebugger] No EntityManager assigned!");
                return;
            }

            var entities = _entityManager.GetAllEntities().ToList();
            var sb = new StringBuilder();
            sb.AppendLine("=== Framework Debug Report ===");
            sb.AppendLine($"Total Entities: {entities.Count}");

            foreach (var entityId in entities)
            {
                sb.AppendLine($"\nEntity {entityId}:");
                sb.AppendLine("  (Component details require reflection)");
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Counts entities by component type.
        /// </summary>
        public void LogEntityStatistics()
        {
            if (_entityManager == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== Entity Statistics ===");
            sb.AppendLine($"Total Entities: {_entityManager.GetAllEntities().Count()}");

            // Add component-specific statistics here
            // Example:
            // sb.AppendLine($"Entities with Health: {_entityManager.GetEntitiesWithComponent<HealthComponent>().Count()}");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Creates a test entity with sample components.
        /// </summary>
        public void CreateTestEntity()
        {
            if (_entityManager == null) return;

            int entityId = _entityManager.CreateEntity();
            Debug.Log($"[FrameworkDebugger] Created test entity: {entityId}");

            // Add test components here
            // Example:
            // _entityManager.AddComponent(entityId, new HealthComponent { CurrentValue = 100, MaxValue = 100 });
        }

        /// <summary>
        /// Destroys all entities (use with caution!).
        /// </summary>
        public void DestroyAllEntities()
        {
            if (_entityManager == null) return;

            var entities = _entityManager.GetAllEntities().ToList();
            foreach (var entityId in entities)
            {
                _entityManager.DestroyEntity(entityId);
            }

            _entityFoldouts.Clear();
            Debug.Log($"[FrameworkDebugger] Destroyed {entities.Count} entities");
        }
    }

    // Placeholder for EditorStyles in runtime
    internal static class EditorStyles
    {
        public static GUIStyle boldLabel => GUI.skin.label;
    }
}
