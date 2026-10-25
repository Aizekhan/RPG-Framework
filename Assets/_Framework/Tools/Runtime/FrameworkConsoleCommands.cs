using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPGFramework.Tools
{
    /// <summary>
    /// In-game console for Framework commands and debugging.
    /// Type commands to manipulate game state at runtime.
    /// </summary>
    public class FrameworkConsoleCommands : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote; // ~
        [SerializeField] private int _commandHistorySize = 50;

        private bool _isVisible = false;
        private string _inputText = "";
        private Vector2 _scrollPosition;
        private readonly List<string> _output = new List<string>();
        private readonly List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;

        private readonly Dictionary<string, ConsoleCommand> _commands = new Dictionary<string, ConsoleCommand>();

        private class ConsoleCommand
        {
            public string Description;
            public Action<string[]> Action;
        }

        void Start()
        {
            RegisterDefaultCommands();
            AddOutput("Framework Console initialized. Type 'help' for commands.");
        }

        void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _isVisible = !_isVisible;
            }

            if (!_isVisible) return;

            // Command history navigation
            if (Input.GetKeyDown(KeyCode.UpArrow) && _commandHistory.Count > 0)
            {
                _historyIndex = Math.Min(_historyIndex + 1, _commandHistory.Count - 1);
                _inputText = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) && _historyIndex > 0)
            {
                _historyIndex--;
                _inputText = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) && _historyIndex == 0)
            {
                _historyIndex = -1;
                _inputText = "";
            }
        }

        void OnGUI()
        {
            if (!_isVisible) return;

            DrawConsole();
        }

        private void DrawConsole()
        {
            float consoleHeight = Screen.height * 0.4f;

            GUILayout.BeginArea(new Rect(0, 0, Screen.width, consoleHeight));
            GUILayout.BeginVertical("box");

            // Output area
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(consoleHeight - 60));
            foreach (var line in _output)
            {
                GUILayout.Label(line);
            }
            GUILayout.EndScrollView();

            // Input area
            GUI.SetNextControlName("ConsoleInput");
            GUILayout.BeginHorizontal();
            GUILayout.Label(">", GUILayout.Width(20));

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && !string.IsNullOrWhiteSpace(_inputText))
            {
                ExecuteCommand(_inputText);
                _inputText = "";
                _historyIndex = -1;
                GUI.FocusControl("ConsoleInput");
            }

            _inputText = GUILayout.TextField(_inputText);
            GUILayout.EndHorizontal();

            GUI.FocusControl("ConsoleInput");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void RegisterDefaultCommands()
        {
            RegisterCommand("help", "Lists all available commands", args =>
            {
                AddOutput("=== Available Commands ===");
                foreach (var cmd in _commands.OrderBy(c => c.Key))
                {
                    AddOutput($"{cmd.Key} - {cmd.Value.Description}");
                }
            });

            RegisterCommand("clear", "Clears the console output", args =>
            {
                _output.Clear();
            });

            RegisterCommand("echo", "Echoes the given message", args =>
            {
                AddOutput(string.Join(" ", args));
            });

            RegisterCommand("time", "Shows current time", args =>
            {
                AddOutput($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                AddOutput($"Uptime: {Time.realtimeSinceStartup:F2} seconds");
            });

            RegisterCommand("quit", "Quits the application", args =>
            {
                AddOutput("Quitting application...");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            // Framework-specific commands
            RegisterCommand("entity.create", "Creates a test entity", args =>
            {
                AddOutput("Command 'entity.create' needs EntityManager reference");
            });

            RegisterCommand("entity.list", "Lists all entities", args =>
            {
                AddOutput("Command 'entity.list' needs EntityManager reference");
            });

            RegisterCommand("item.give", "Gives an item to player (usage: item.give <itemId> <quantity>)", args =>
            {
                if (args.Length < 2)
                {
                    AddOutput("Usage: item.give <itemId> <quantity>");
                    return;
                }
                AddOutput($"Command 'item.give' needs Inventory reference");
            });

            RegisterCommand("quest.start", "Starts a quest (usage: quest.start <questId>)", args =>
            {
                if (args.Length < 1)
                {
                    AddOutput("Usage: quest.start <questId>");
                    return;
                }
                AddOutput($"Command 'quest.start' needs QuestManager reference");
            });

            RegisterCommand("stats.show", "Shows player stats", args =>
            {
                AddOutput("Command 'stats.show' needs StatsSystem reference");
            });
        }

        /// <summary>
        /// Registers a custom console command.
        /// </summary>
        public void RegisterCommand(string commandName, string description, Action<string[]> action)
        {
            _commands[commandName.ToLower()] = new ConsoleCommand
            {
                Description = description,
                Action = action
            };
        }

        /// <summary>
        /// Executes a command string.
        /// </summary>
        private void ExecuteCommand(string commandString)
        {
            AddOutput($"> {commandString}");

            // Add to history
            _commandHistory.Add(commandString);
            if (_commandHistory.Count > _commandHistorySize)
            {
                _commandHistory.RemoveAt(0);
            }

            // Parse command
            var parts = commandString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var commandName = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            // Execute command
            if (_commands.TryGetValue(commandName, out var command))
            {
                try
                {
                    command.Action(args);
                }
                catch (Exception ex)
                {
                    AddOutput($"Error executing command: {ex.Message}");
                }
            }
            else
            {
                AddOutput($"Unknown command: {commandName}. Type 'help' for available commands.");
            }

            // Auto-scroll to bottom
            _scrollPosition.y = float.MaxValue;
        }

        /// <summary>
        /// Adds a line to the console output.
        /// </summary>
        public void AddOutput(string message)
        {
            _output.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

            // Limit output size
            if (_output.Count > 1000)
            {
                _output.RemoveAt(0);
            }
        }

        /// <summary>
        /// Clears the console output.
        /// </summary>
        public void ClearOutput()
        {
            _output.Clear();
        }
    }
}
