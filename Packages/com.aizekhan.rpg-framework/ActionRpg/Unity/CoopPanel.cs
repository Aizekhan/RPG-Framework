using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ActionRpg.Coop;
using RPGFramework.Localization;
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
            GUILayout.Label(L("kit.coop.title"), _title);
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
                GUILayout.Label(L("kit.coop.switching"), _text);
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
            GUILayout.Label(L("kit.coop.intro", ("service", _host.SessionProvider.DisplayName), ("count", _host.MaxPlayers - 1)), _text);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L("kit.coop.name"), _text, GUILayout.Width(100f));
            _name = _name ?? _host.PlayerName;
            _name = GUILayout.TextField(_name, ActorWorld.MaxNameLength, GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            if (GUI.changed && ActorWorld.CleanName(_name) != null && ActorWorld.CleanName(_name) != _host.PlayerName)
                _host.PlayerName = _name;
            if (GUILayout.Button(L("kit.coop.host"), GUILayout.Height(34f)))
                _host.HostGame();
            GUILayout.Space(10f);
            GUILayout.Label(L("kit.coop.joinHint", ("hint", _host.SessionProvider.JoinCodeHint)), _text);
            GUILayout.BeginHorizontal();
            _address = GUILayout.TextField(_address, 45, GUILayout.Height(30f));
            if (GUILayout.Button(L("kit.coop.join"), GUILayout.Width(90f), GUILayout.Height(30f)))
                _host.JoinGame(_address);
            GUILayout.EndHorizontal();
        }

        private void DrawHosting()
        {
            GUILayout.Label(L("kit.coop.code", ("code", _host.JoinCode)), _rich);
            if (_host.OtherJoinCodes.Count > 0)
                GUILayout.Label(L("kit.coop.otherCodes", ("codes", string.Join(",  ", _host.OtherJoinCodes))), _rich);
            var coop = _host.CoopHost;
            if (!coop.MatchStarted)
            {
                DrawLobby(coop.Lobby, coop.MaxPlayers, coop.StartCountdown, kick: id => coop.Kick(id));
                bool ready = coop.Lobby.Count > 0 && coop.Lobby[0].Ready;
                if (GUILayout.Button(ready ? L("kit.coop.notReady") : L("kit.coop.ready"), GUILayout.Height(28f)))
                    coop.SetLobbyChoice(!ready);
                if (coop.StartCountdown < 0f && GUILayout.Button(L("kit.coop.start"), GUILayout.Height(34f)))
                    coop.StartMatch();
                else if (coop.StartCountdown >= 0f && GUILayout.Button(L("kit.coop.wait"), GUILayout.Height(34f)))
                    coop.CancelStart();
            }
            else
            {
                GUILayout.Label(L("kit.coop.players", ("count", coop.PlayerCount), ("max", coop.MaxPlayers), ("party", Party())), _rich);
            }
            if (GUILayout.Button(L("kit.coop.stopHosting"), GUILayout.Height(34f)))
                _host.LeaveGame();
        }

        // Who waits in the lobby; the host can send a player out
        private void DrawLobby(IReadOnlyList<CoopLobbyEntry> lobby, int maxPlayers, float countdown, Action<int> kick)
        {
            GUILayout.Label(L("kit.coop.lobby", ("count", lobby.Count), ("max", maxPlayers))
                + (countdown >= 0f ? "   " + L("kit.coop.startingIn", ("seconds", Mathf.CeilToInt(countdown))) : ""), _rich);
            foreach (var entry in lobby)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Escape(entry.Name)}{(entry.Loadout.Length > 0 ? "  (" + Escape(entry.Loadout) + ")" : "")}"
                    + (entry.Ready ? "  <color=#7CFC00>" + L("kit.coop.isReady") + "</color>" : ""), _rich);
                if (kick != null && !entry.IsHost && GUILayout.Button(L("kit.coop.remove"), GUILayout.Width(80f)))
                    kick(entry.Id);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawJoined()
        {
            var client = _host.CoopClient;
            if (client.State == CoopClientState.Lobby)
            {
                DrawLobby(client.Lobby, client.LobbyMaxPlayers, client.StartCountdown, kick: null);
                var mine = client.Lobby.FirstOrDefault(e => e.Id == client.LobbyId);
                bool ready = mine != null && mine.Ready;
                if (GUILayout.Button(ready ? L("kit.coop.notReady") : L("kit.coop.ready"), GUILayout.Height(28f)))
                    client.SetLobbyChoice(!ready, mine?.Loadout ?? "");
            }
            else
            {
                GUILayout.Label(client.State == CoopClientState.Playing
                    ? L("kit.coop.playing", ("party", Party()))
                    : L("kit.coop.connecting"), _rich);
            }
            if (client.IsUnstable)
                GUILayout.Label("<color=#FFB000>" + L("kit.coop.unstableHost") + "</color>", _rich);
            if (GUILayout.Button(L("kit.coop.leave"), GUILayout.Height(34f)))
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
                .Select(a => $"<color=#{ColorUtility.ToHtmlStringRGB(_host.ColorOfSeat(a.Seat))}>{Escape(a.DisplayName)}</color>"
                    + (_host.CoopHost != null && _host.CoopHost.IsUnstable(a.EntityId) ? " <color=#FFB000>" + L("kit.coop.unstable") + "</color>" : ""));
            return string.Join("  ", names);
        }

        /// <summary>
        /// The panel's texts by key, in English, for a game's string tables.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
        {
            ["kit.coop.title"] = "Co-op  (F2)",
            ["kit.coop.switching"] = "Switching...",
            ["kit.coop.intro"] = "Play together ({service}): one hosts, up to {count} join.",
            ["kit.coop.name"] = "Your name",
            ["kit.coop.host"] = "Host a game",
            ["kit.coop.joinHint"] = "Join a friend's game ({hint}):",
            ["kit.coop.join"] = "Join",
            ["kit.coop.code"] = "Friends join with  <b>{code}</b>",
            ["kit.coop.otherCodes"] = "or, over a VPN or another network:  {codes}",
            ["kit.coop.ready"] = "Ready",
            ["kit.coop.notReady"] = "Not ready",
            ["kit.coop.isReady"] = "ready",
            ["kit.coop.start"] = "Start",
            ["kit.coop.wait"] = "Wait",
            ["kit.coop.players"] = "Players  {count} / {max}:  {party}",
            ["kit.coop.stopHosting"] = "Stop hosting",
            ["kit.coop.lobby"] = "Lobby  {count} / {max}",
            ["kit.coop.startingIn"] = "starting in {seconds}...",
            ["kit.coop.remove"] = "Remove",
            ["kit.coop.playing"] = "Playing in a friend's game:  {party}",
            ["kit.coop.connecting"] = "Connecting...",
            ["kit.coop.inLobby"] = "In the lobby",
            ["kit.coop.unstableHost"] = "The connection to the host is unstable...",
            ["kit.coop.unstable"] = "(unstable)",
            ["kit.coop.leave"] = "Leave"
        };

        private string L(string key, params (string Name, object Value)[] args) =>
            // The host's, asked each time: switching to co-op starts a new one
            (_host.Localization ?? KitTexts.English).Text(new LocalizedText(key, args), Keys[key]);

        // Names come from other players: keep them from opening rich-text tags of their own
        private static string Escape(string name) => name.Replace("<", "‹").Replace(">", "›");

        private string Summary()
        {
            switch (_host.Role)
            {
                case CoopRole.Hosting:
                    return $"{Party()}   (F2)";
                case CoopRole.Joined:
                    return _host.CoopClient.State == CoopClientState.Playing ? $"{Party()}   (F2)"
                        : (_host.CoopClient.State == CoopClientState.Lobby ? L("kit.coop.inLobby") : L("kit.coop.connecting")) + "   (F2)";
                default:
                    return "";
            }
        }
    }
}
