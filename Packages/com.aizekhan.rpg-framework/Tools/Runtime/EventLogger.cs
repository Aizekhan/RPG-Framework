using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RPGFramework.Tools
{
    /// <summary>
    /// Logs Framework events for debugging purposes.
    /// Subscribe to various event types to track game state changes.
    /// </summary>
    public class EventLogger : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private bool _logToConsole = true;
        [SerializeField] private bool _logToFile = false;
        [SerializeField] private int _maxLogEntries = 1000;
        [SerializeField] private KeyCode _toggleDisplayKey = KeyCode.F3;

        [Header("Filters")]
        [SerializeField] private bool _logCombatEvents = true;
        [SerializeField] private bool _logInventoryEvents = true;
        [SerializeField] private bool _logQuestEvents = true;
        [SerializeField] private bool _logDialogueEvents = true;
        [SerializeField] private bool _logTradingEvents = true;

        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private bool _isVisible = false;
        private Vector2 _scrollPosition;
        private string _searchFilter = "";

        private class LogEntry
        {
            public DateTime Timestamp;
            public string Category;
            public string Message;
            public LogType Type;
        }

        void Update()
        {
            if (ToolKeys.WasPressed(_toggleDisplayKey))
            {
                _isVisible = !_isVisible;
            }
        }

        void OnGUI()
        {
            if (!_isVisible) return;

            GUILayout.BeginArea(new Rect(420, 10, 400, Screen.height - 20));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Event Logger", GUI.skin.box);
            GUILayout.Label($"Press {_toggleDisplayKey} to toggle | Entries: {_logEntries.Count}");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = GUILayout.TextField(_searchFilter, GUILayout.Width(250));
            if (GUILayout.Button("Clear"))
            {
                ClearLog();
            }
            GUILayout.EndHorizontal();

            DrawLogEntries();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawLogEntries()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(Screen.height - 120));

            var filteredEntries = string.IsNullOrEmpty(_searchFilter)
                ? _logEntries
                : _logEntries.FindAll(e => e.Message.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

            foreach (var entry in filteredEntries)
            {
                var color = entry.Type switch
                {
                    LogType.Error => Color.red,
                    LogType.Warning => Color.yellow,
                    _ => Color.white
                };

                GUI.color = color;
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"[{entry.Timestamp:HH:mm:ss}]", GUILayout.Width(70));
                GUILayout.Label($"[{entry.Category}]", GUILayout.Width(80));
                GUILayout.Label(entry.Message);
                GUILayout.EndHorizontal();
                GUI.color = Color.white;
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Logs an event to the event logger.
        /// </summary>
        public void LogEvent(string category, string message, LogType type = LogType.Log)
        {
            if (!_enabled) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Category = category,
                Message = message,
                Type = type
            };

            _logEntries.Add(entry);

            // Trim old entries if exceeding max
            if (_logEntries.Count > _maxLogEntries)
            {
                _logEntries.RemoveAt(0);
            }

            if (_logToConsole)
            {
                var logMessage = $"[{category}] {message}";
                switch (type)
                {
                    case LogType.Error:
                        Debug.LogError(logMessage);
                        break;
                    case LogType.Warning:
                        Debug.LogWarning(logMessage);
                        break;
                    default:
                        Debug.Log(logMessage);
                        break;
                }
            }
        }

        /// <summary>
        /// Logs a combat event.
        /// </summary>
        public void LogCombatEvent(string message)
        {
            if (_logCombatEvents)
                LogEvent("Combat", message);
        }

        /// <summary>
        /// Logs an inventory event.
        /// </summary>
        public void LogInventoryEvent(string message)
        {
            if (_logInventoryEvents)
                LogEvent("Inventory", message);
        }

        /// <summary>
        /// Logs a quest event.
        /// </summary>
        public void LogQuestEvent(string message)
        {
            if (_logQuestEvents)
                LogEvent("Quest", message);
        }

        /// <summary>
        /// Logs a dialogue event.
        /// </summary>
        public void LogDialogueEvent(string message)
        {
            if (_logDialogueEvents)
                LogEvent("Dialogue", message);
        }

        /// <summary>
        /// Logs a trading event.
        /// </summary>
        public void LogTradingEvent(string message)
        {
            if (_logTradingEvents)
                LogEvent("Trading", message);
        }

        /// <summary>
        /// Clears all log entries.
        /// </summary>
        public void ClearLog()
        {
            _logEntries.Clear();
            Debug.Log("[EventLogger] Log cleared");
        }

        /// <summary>
        /// Exports log to a string.
        /// </summary>
        public string ExportLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Event Log Export ===");
            sb.AppendLine($"Export Time: {DateTime.Now}");
            sb.AppendLine($"Total Entries: {_logEntries.Count}");
            sb.AppendLine();

            foreach (var entry in _logEntries)
            {
                sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Category}] {entry.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Saves log to file.
        /// </summary>
        public void SaveLogToFile(string filename = "event_log.txt")
        {
            try
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, filename);
                System.IO.File.WriteAllText(path, ExportLog());
                Debug.Log($"[EventLogger] Log saved to: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventLogger] Failed to save log: {e.Message}");
            }
        }
    }
}
