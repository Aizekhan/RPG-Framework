using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        private string _localAddress;
        private GUIStyle _text;
        private GUIStyle _title;
        private GUIStyle _centered;
        private GUIStyle _rich;

        public bool Visible { get; set; }

        public void Bind(ActionRpgHost host) => _host = host;

        /// <summary>
        /// This machine's address on the local network (what friends type to join), or empty when there is none.
        /// </summary>
        public static string LocalAddress()
        {
            try
            {
                var address = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                return address?.ToString() ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

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
                _centered = new GUIStyle(_text) { alignment = TextAnchor.MiddleCenter };
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
            GUILayout.Label("Play together with friends on the same network: one hosts, up to three join.", _text);
            if (GUILayout.Button("Host a game", GUILayout.Height(34f)))
                _host.HostGame();
            GUILayout.Space(10f);
            GUILayout.Label("Join a friend's game at (their IP address):", _text);
            GUILayout.BeginHorizontal();
            _address = GUILayout.TextField(_address, 45, GUILayout.Height(30f));
            if (GUILayout.Button("Join", GUILayout.Width(90f), GUILayout.Height(30f)))
                _host.JoinGame(_address);
            GUILayout.EndHorizontal();
        }

        private void DrawHosting()
        {
            _localAddress = _localAddress ?? LocalAddress();
            string at = _localAddress.Length > 0 ? _localAddress : "this computer's IP address";
            GUILayout.Label($"Friends join at  <b>{at}</b>  (port {_host.HostingPort})", _rich);
            GUILayout.Label($"Players  {_host.CoopHost.PlayerCount} / {_host.CoopHost.MaxPlayers}", _text);
            if (GUILayout.Button("Stop hosting", GUILayout.Height(34f)))
                _host.LeaveGame();
        }

        private void DrawJoined()
        {
            var client = _host.CoopClient;
            GUILayout.Label(client.State == CoopClientState.Playing
                ? $"Playing in a friend's game  ({client.World.LivingPlayers.Count()} standing)"
                : "Connecting...", _text);
            if (GUILayout.Button("Leave", GUILayout.Height(34f)))
                _host.LeaveGame();
        }

        private string Summary()
        {
            switch (_host.Role)
            {
                case CoopRole.Hosting:
                    return $"Hosting  {_host.CoopHost.PlayerCount}/{_host.CoopHost.MaxPlayers} players   (F2)";
                case CoopRole.Joined:
                    return _host.CoopClient.State == CoopClientState.Playing ? "In a friend's game   (F2)" : "Connecting...   (F2)";
                default:
                    return "";
            }
        }
    }
}
