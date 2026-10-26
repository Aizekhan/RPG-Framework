using System;
using System.Linq;
using RPGFramework.ActionRpg.Coop;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The co-op panel (F2): host a game and see the address friends join at, or type a friend's address and join;
    /// stop hosting or leave. While playing together a short line at the top shows who is in.
    /// <see cref="ActionRpgHost"/> adds it when 'Co-op' is on.
    /// </summary>
    public sealed class CoopPanel : MonoBehaviour
    {
        [SerializeField] private Key _toggleKey = Key.F2;

        private ActionRpgHost _host;
        private string _address = "";
        private string _name;
        private GUIStyle _text;
        private GUIStyle _title;
        private GUIStyle _centered;
        private GUIStyle _rich;

        public bool Visible { get; set; }

        public void Bind(ActionRpgHost host) => _host = host;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (_toggleKey != Key.None && keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
                Visible = !Visible;
        }

        private void OnGUI()
        {
            if (_host == null)
                return;
            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
                _title = new GUIStyle(_text) { fontSize = 20, fontStyle = FontStyle.Bold };
                _centered = new GUIStyle(_text) { alignment = TextAnchor.MiddleCenter, richText = true };
                _rich = new GUIStyle(_text) { richText = true };
            }

            if (!Visible)
            {
                string summary = Summary();
                if (summary.Length > 0)
                    GUI.Label(new Rect(Screen.width * 0.5f - 250f, 8f, 500f, 28f), summary, _centered);
                return;
            }

            const float width = 440f;
            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - width * 0.5f, 60f, width, 320f), GUI.skin.box);
            GUILayout.Label("Co-op  (F2)", _title);
            switch (_host.Role)
            {
                case CoopRole.Solo:
                    DrawSolo();
                    break;
                case CoopRole.Hosting:
                    DrawHosting();
                    break;
                case CoopRole.Joined:
                    DrawJoined();
                    break;
            }
            if (!string.IsNullOrEmpty(_host.CoopStatus))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(_host.CoopStatus, _text);
            }
            GUILayout.EndArea();
        }

        private void DrawSolo()
        {
            if (_host.IsSwitching)
            {
                GUILayout.Label("Switching...", _text);
                return;
            }
            var providers = _host.SessionProviders;
            if (providers.Count > 1)
            {
                // Local network or an installed online service (Unity Relay, ...)
                int current = 0;
                var names = new string[providers.Count];
                for (int i = 0; i < providers.Count; i++)
                {
                    names[i] = providers[i].DisplayName;
                    if (providers[i] == _host.SessionProvider)
                        current = i;
                }
                int chosen = GUILayout.Toolbar(current, names, GUILayout.Height(28f));
                if (chosen != current)
                    _host.SessionProvider = providers[chosen];
            }
            GUILayout.Label($"Play together ({_host.SessionProvider.DisplayName}): one hosts, up to {_host.MaxPlayers - 1} join.", _text);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Your name", _text, GUILayout.Width(100f));
            _name = _name ?? _host.PlayerName;
            _name = GUILayout.TextField(_name, ActorWorld.MaxNameLength, GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            if (GUI.changed && ActorWorld.CleanName(_name) != null && ActorWorld.CleanName(_name) != _host.PlayerName)
                _host.PlayerName = _name;
            if (GUILayout.Button("Host a game", GUILayout.Height(34f)))
                _host.HostGame();
            GUILayout.Space(10f);
            GUILayout.Label($"Join a friend's game ({_host.SessionProvider.JoinCodeHint}):", _text);
            GUILayout.BeginHorizontal();
            _address = GUILayout.TextField(_address, 45, GUILayout.Height(30f));
            if (GUILayout.Button("Join", GUILayout.Width(90f), GUILayout.Height(30f)))
                _host.JoinGame(_address);
            GUILayout.EndHorizontal();
        }

        private void DrawHosting()
        {
            GUILayout.Label($"Friends join with  <b>{_host.JoinCode}</b>", _rich);
            GUILayout.Label($"Players  {_host.CoopHost.PlayerCount} / {_host.CoopHost.MaxPlayers}:  {Party()}", _rich);
            if (GUILayout.Button("Stop hosting", GUILayout.Height(34f)))
                _host.LeaveGame();
        }

        private void DrawJoined()
        {
            var client = _host.CoopClient;
            GUILayout.Label(client.State == CoopClientState.Playing
                ? $"Playing in a friend's game:  {Party()}"
                : "Connecting...", _rich);
            if (GUILayout.Button("Leave", GUILayout.Height(34f)))
                _host.LeaveGame();
        }

        /// <summary>
        /// Everyone in the party, each name in their colour.
        /// </summary>
        public string Party()
        {
            var world = _host.CurrentWorld;
            if (world == null)
                return "";
            var names = world.Actors.Where(a => a.IsPlayer).OrderBy(a => a.Seat)
                .Select(a => $"<color=#{ColorUtility.ToHtmlStringRGB(_host.ColorOfSeat(a.Seat))}>{Escape(a.DisplayName)}</color>");
            return string.Join("  ", names);
        }

        // Names come from other players: keep them from opening rich-text tags of their own
        private static string Escape(string name) => name.Replace("<", "‹").Replace(">", "›");

        private string Summary()
        {
            switch (_host.Role)
            {
                case CoopRole.Hosting:
                    return $"{Party()}   (F2)";
                case CoopRole.Joined:
                    return _host.CoopClient.State == CoopClientState.Playing ? $"{Party()}   (F2)" : "Connecting...   (F2)";
                default:
                    return "";
            }
        }
    }
}
