using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RPGFramework.Tools
{
    /// <summary>
    /// Monitors Framework system health and reports issues.
    /// Tracks memory usage, performance, and system states.
    /// </summary>
    public class SystemHealthMonitor : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private float _checkInterval = 2f;
        [SerializeField] private KeyCode _toggleDisplayKey = KeyCode.F4;

        [Header("Thresholds")]
        [SerializeField] private float _warningFrameTime = 16.7f; // ms (60 FPS)
        [SerializeField] private long _warningMemory = 500 * 1024 * 1024; // 500 MB

        private bool _isVisible = false;
        private float _nextCheckTime = 0f;
        private readonly List<HealthIssue> _issues = new List<HealthIssue>();

        private class HealthIssue
        {
            public DateTime Timestamp;
            public string Category;
            public string Message;
            public HealthLevel Level;
        }

        private enum HealthLevel
        {
            Info,
            Warning,
            Error
        }

        private struct HealthStats
        {
            public float CurrentFPS;
            public float AverageFrameTime;
            public long TotalMemory;
            public long UsedMemory;
            public int EntityCount;
        }

        private HealthStats _currentStats;

        void Update()
        {
            if (!_enabled) return;

            if (ToolKeys.WasPressed(_toggleDisplayKey))
            {
                _isVisible = !_isVisible;
            }

            if (Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + _checkInterval;
                PerformHealthCheck();
            }
        }

        void OnGUI()
        {
            if (!_isVisible) return;

            GUILayout.BeginArea(new Rect(10, Screen.height - 310, 300, 300));
            GUILayout.BeginVertical("box");

            GUILayout.Label("System Health Monitor", GUI.skin.box);
            GUILayout.Label($"Press {_toggleDisplayKey} to toggle");

            DrawHealthStats();
            DrawIssues();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawHealthStats()
        {
            GUILayout.Label("=== Stats ===", EditorStyles.boldLabel);
            GUILayout.Label($"FPS: {_currentStats.CurrentFPS:F1}");
            GUILayout.Label($"Frame Time: {_currentStats.AverageFrameTime:F2} ms");
            GUILayout.Label($"Memory: {_currentStats.UsedMemory / (1024 * 1024)} / {_currentStats.TotalMemory / (1024 * 1024)} MB");
            GUILayout.Label($"Entities: {_currentStats.EntityCount}");
        }

        private void DrawIssues()
        {
            if (_issues.Count == 0)
            {
                GUILayout.Label("Status: Healthy", GUI.skin.box);
                return;
            }

            GUILayout.Label($"=== Issues ({_issues.Count}) ===", EditorStyles.boldLabel);

            foreach (var issue in _issues)
            {
                var color = issue.Level switch
                {
                    HealthLevel.Error => Color.red,
                    HealthLevel.Warning => Color.yellow,
                    _ => Color.white
                };

                GUI.color = color;
                GUILayout.Label($"[{issue.Category}] {issue.Message}");
                GUI.color = Color.white;
            }
        }

        private void PerformHealthCheck()
        {
            UpdateStats();
            CheckPerformance();
            CheckMemory();
        }

        private void UpdateStats()
        {
            _currentStats.CurrentFPS = 1f / Time.unscaledDeltaTime;
            _currentStats.AverageFrameTime = Time.unscaledDeltaTime * 1000f;
            _currentStats.TotalMemory = GC.GetTotalMemory(false);
            _currentStats.UsedMemory = GC.GetTotalMemory(false);
            _currentStats.EntityCount = 0; // Would need EntityManager reference
        }

        private void CheckPerformance()
        {
            if (_currentStats.AverageFrameTime > _warningFrameTime)
            {
                AddIssue(HealthLevel.Warning, "Performance",
                    $"Frame time is {_currentStats.AverageFrameTime:F2} ms (target: {_warningFrameTime:F2} ms)");
            }
        }

        private void CheckMemory()
        {
            if (_currentStats.UsedMemory > _warningMemory)
            {
                AddIssue(HealthLevel.Warning, "Memory",
                    $"Memory usage is {_currentStats.UsedMemory / (1024 * 1024)} MB (threshold: {_warningMemory / (1024 * 1024)} MB)");
            }
        }

        private void AddIssue(HealthLevel level, string category, string message)
        {
            var issue = new HealthIssue
            {
                Timestamp = DateTime.Now,
                Category = category,
                Message = message,
                Level = level
            };

            // Remove duplicate issues
            _issues.RemoveAll(i => i.Category == category && i.Message == message);
            _issues.Add(issue);

            // Limit issue count
            if (_issues.Count > 20)
            {
                _issues.RemoveAt(0);
            }
        }

        /// <summary>
        /// Generates a health report.
        /// </summary>
        public string GenerateHealthReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== System Health Report ===");
            sb.AppendLine($"Report Time: {DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine($"FPS: {_currentStats.CurrentFPS:F1}");
            sb.AppendLine($"Frame Time: {_currentStats.AverageFrameTime:F2} ms");
            sb.AppendLine($"Memory Usage: {_currentStats.UsedMemory / (1024 * 1024)} MB");
            sb.AppendLine($"Total Memory: {_currentStats.TotalMemory / (1024 * 1024)} MB");
            sb.AppendLine($"Entity Count: {_currentStats.EntityCount}");
            sb.AppendLine();

            if (_issues.Count > 0)
            {
                sb.AppendLine($"Issues ({_issues.Count}):");
                foreach (var issue in _issues)
                {
                    sb.AppendLine($"[{issue.Level}] [{issue.Category}] {issue.Message}");
                }
            }
            else
            {
                sb.AppendLine("Status: No issues detected");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Clears all health issues.
        /// </summary>
        public void ClearIssues()
        {
            _issues.Clear();
        }
    }
}
