using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RPGFramework.Tools
{
    /// <summary>
    /// Performance profiler for Framework systems.
    /// Tracks execution time, memory allocation, and call frequency.
    /// </summary>
    public class PerformanceProfiler : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private KeyCode _toggleDisplayKey = KeyCode.F2;
        [SerializeField] private float _reportInterval = 5f; // seconds

        [Header("Display")]
        [SerializeField] private bool _showOnScreen = true;
        [SerializeField] private int _maxDisplayedSystems = 10;

        private readonly Dictionary<string, ProfileData> _profiles = new Dictionary<string, ProfileData>();
        private bool _isVisible = false;
        private float _nextReportTime = 0f;
        private Vector2 _scrollPosition;

        private class ProfileData
        {
            public long TotalTicks;
            public int CallCount;
            public long MinTicks = long.MaxValue;
            public long MaxTicks = long.MinValue;
            public DateTime LastCall;

            public double AverageMs => CallCount > 0 ? (TotalTicks / (double)CallCount) / Stopwatch.Frequency * 1000 : 0;
            public double TotalMs => TotalTicks / (double)Stopwatch.Frequency * 1000;
            public double MinMs => MinTicks / (double)Stopwatch.Frequency * 1000;
            public double MaxMs => MaxTicks / (double)Stopwatch.Frequency * 1000;
        }

        void Update()
        {
            if (Input.GetKeyDown(_toggleDisplayKey))
            {
                _isVisible = !_isVisible;
            }

            if (Time.time >= _nextReportTime)
            {
                _nextReportTime = Time.time + _reportInterval;
                if (_enabled)
                {
                    LogPerformanceReport();
                }
            }
        }

        void OnGUI()
        {
            if (!_showOnScreen || !_isVisible) return;

            GUILayout.BeginArea(new Rect(Screen.width - 410, 10, 400, Screen.height - 20));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Performance Profiler", GUI.skin.box);
            GUILayout.Label($"Press {_toggleDisplayKey} to toggle");

            DrawProfileData();

            if (GUILayout.Button("Clear Data"))
            {
                ClearAllProfiles();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawProfileData()
        {
            if (_profiles.Count == 0)
            {
                GUILayout.Label("No profiling data yet...");
                return;
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(Screen.height - 100));

            var sortedProfiles = _profiles
                .OrderByDescending(p => p.Value.TotalMs)
                .Take(_maxDisplayedSystems);

            foreach (var kvp in sortedProfiles)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label($"{kvp.Key}", EditorStyles.boldLabel);
                GUILayout.Label($"Calls: {kvp.Value.CallCount}");
                GUILayout.Label($"Avg: {kvp.Value.AverageMs:F3} ms");
                GUILayout.Label($"Total: {kvp.Value.TotalMs:F2} ms");
                GUILayout.Label($"Min/Max: {kvp.Value.MinMs:F3} / {kvp.Value.MaxMs:F3} ms");
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Profiles a method execution.
        /// </summary>
        /// <param name="systemName">Name of the system or method</param>
        /// <param name="action">Action to profile</param>
        public void Profile(string systemName, Action action)
        {
            if (!_enabled)
            {
                action?.Invoke();
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            action?.Invoke();
            stopwatch.Stop();

            RecordProfile(systemName, stopwatch.ElapsedTicks);
        }

        /// <summary>
        /// Profiles a function execution and returns its result.
        /// </summary>
        public T Profile<T>(string systemName, Func<T> func)
        {
            if (!_enabled)
            {
                return func != null ? func() : default;
            }

            var stopwatch = Stopwatch.StartNew();
            T result = func != null ? func() : default;
            stopwatch.Stop();

            RecordProfile(systemName, stopwatch.ElapsedTicks);
            return result;
        }

        /// <summary>
        /// Starts a manual profiling session.
        /// Use with EndProfile() for custom profiling blocks.
        /// </summary>
        public Stopwatch StartProfile()
        {
            if (!_enabled) return null;
            return Stopwatch.StartNew();
        }

        /// <summary>
        /// Ends a manual profiling session.
        /// </summary>
        public void EndProfile(string systemName, Stopwatch stopwatch)
        {
            if (!_enabled || stopwatch == null) return;

            stopwatch.Stop();
            RecordProfile(systemName, stopwatch.ElapsedTicks);
        }

        private void RecordProfile(string systemName, long ticks)
        {
            if (!_profiles.TryGetValue(systemName, out var data))
            {
                data = new ProfileData();
                _profiles[systemName] = data;
            }

            data.TotalTicks += ticks;
            data.CallCount++;
            data.MinTicks = Math.Min(data.MinTicks, ticks);
            data.MaxTicks = Math.Max(data.MaxTicks, ticks);
            data.LastCall = DateTime.Now;
        }

        /// <summary>
        /// Logs a performance report to the console.
        /// </summary>
        public void LogPerformanceReport()
        {
            if (_profiles.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== Performance Report ===");
            sb.AppendLine($"Report Time: {DateTime.Now:HH:mm:ss}");
            sb.AppendLine($"Profiled Systems: {_profiles.Count}");
            sb.AppendLine();

            var sortedProfiles = _profiles.OrderByDescending(p => p.Value.TotalMs);

            foreach (var kvp in sortedProfiles)
            {
                sb.AppendLine($"{kvp.Key}:");
                sb.AppendLine($"  Calls: {kvp.Value.CallCount}");
                sb.AppendLine($"  Avg Time: {kvp.Value.AverageMs:F3} ms");
                sb.AppendLine($"  Total Time: {kvp.Value.TotalMs:F2} ms");
                sb.AppendLine($"  Min/Max: {kvp.Value.MinMs:F3} / {kvp.Value.MaxMs:F3} ms");
                sb.AppendLine($"  Last Call: {kvp.Value.LastCall:HH:mm:ss}");
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Gets profiling data for a specific system.
        /// </summary>
        public void LogSystemProfile(string systemName)
        {
            if (!_profiles.TryGetValue(systemName, out var data))
            {
                Debug.LogWarning($"No profiling data for system: {systemName}");
                return;
            }

            Debug.Log($"=== {systemName} Profile ===\n" +
                      $"Calls: {data.CallCount}\n" +
                      $"Average: {data.AverageMs:F3} ms\n" +
                      $"Total: {data.TotalMs:F2} ms\n" +
                      $"Min: {data.MinMs:F3} ms\n" +
                      $"Max: {data.MaxMs:F3} ms\n" +
                      $"Last Call: {data.LastCall:HH:mm:ss}");
        }

        /// <summary>
        /// Clears all profiling data.
        /// </summary>
        public void ClearAllProfiles()
        {
            _profiles.Clear();
            Debug.Log("[PerformanceProfiler] All profiling data cleared");
        }

        /// <summary>
        /// Clears profiling data for a specific system.
        /// </summary>
        public void ClearSystemProfile(string systemName)
        {
            if (_profiles.Remove(systemName))
            {
                Debug.Log($"[PerformanceProfiler] Cleared profile for: {systemName}");
            }
        }

        /// <summary>
        /// Gets the top N slowest systems.
        /// </summary>
        public List<KeyValuePair<string, double>> GetSlowestSystems(int count)
        {
            return _profiles
                .OrderByDescending(p => p.Value.TotalMs)
                .Take(count)
                .Select(p => new KeyValuePair<string, double>(p.Key, p.Value.TotalMs))
                .ToList();
        }

        /// <summary>
        /// Enables or disables profiling.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            Debug.Log($"[PerformanceProfiler] Profiling {(enabled ? "enabled" : "disabled")}");
        }
    }
}
