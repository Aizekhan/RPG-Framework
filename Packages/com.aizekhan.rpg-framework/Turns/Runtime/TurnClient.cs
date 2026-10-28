using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Logging;
using RPGFramework.Networking;

namespace RPGFramework.Turns
{
    public enum TurnClientState
    {
        Idle,
        Connecting,

        /// <summary>
        /// Let in: in the lobby, or in the match once <see cref="TurnClient.IsStarted"/>.
        /// </summary>
        Seated,

        Ended
    }

    /// <summary>
    /// Takes a seat at a host's table: says hello with the game's fingerprint, then gets the lobby, the match options,
    /// this seat's view after every change, notices and questions, and sends this player's moves and answers. The rules
    /// run only on the host. Call <see cref="Update"/> every frame.
    /// </summary>
    public sealed class TurnClient : ITurnSession, IDisposable
    {
        private const string Category = "Turns";

        private readonly INetworkManager _network;
        private readonly string _fingerprint;
        private readonly ILogger _logger;
        private readonly List<IDisposable> _handlers = new List<IDisposable>();
        private readonly Dictionary<int, TurnPrompt> _prompts = new Dictionary<int, TurnPrompt>();
        private List<TurnSeat> _seats = new List<TurnSeat>();
        private string _address = "";
        private string _pendingEnd;
        private int _nextRequest;
        private int _viewNumber = int.MinValue;

        /// <param name="network">An unstarted network manager made by <see cref="TurnProtocol.CreateNetwork"/>.</param>
        /// <param name="fingerprint">The game and its rules version, the same as the host's.</param>
        public TurnClient(INetworkManager network, string playerName, string fingerprint = "", ILogger logger = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            PlayerName = playerName ?? "";
            _fingerprint = fingerprint ?? "";
            _logger = logger;
            _network.PeerConnected += OnConnected;
            _network.PeerDisconnected += OnDisconnected;
            _network.PeerDropped += OnDropped;
            _handlers.Add(_network.RegisterMessageHandler<TurnWelcomeMessage>(OnWelcome));
            _handlers.Add(_network.RegisterMessageHandler<TurnLobbyMessage>(OnLobby));
            _handlers.Add(_network.RegisterMessageHandler<TurnStartedMessage>(OnStarted));
            _handlers.Add(_network.RegisterMessageHandler<TurnViewMessage>(OnView));
            _handlers.Add(_network.RegisterMessageHandler<TurnReplyMessage>(OnReply));
            _handlers.Add(_network.RegisterMessageHandler<TurnNoticeMessage>(OnNotice));
            _handlers.Add(_network.RegisterMessageHandler<TurnPromptMessage>(OnPrompt));
            _handlers.Add(_network.RegisterMessageHandler<TurnPromptClosedMessage>(OnPromptClosed));
        }

        public string PlayerName { get; set; }

        /// <summary>
        /// Brings this player back to their seat after a lost link: given by the host on joining; set it on a new
        /// client before <see cref="Join"/> to come back (keep it with the match, not the profile).
        /// </summary>
        public string RejoinToken { get; set; } = "";

        public TurnClientState State { get; private set; } = TurnClientState.Idle;

        /// <summary>
        /// Why it ended (refused, host gone, unreachable), for the player to read.
        /// </summary>
        public string EndReason { get; private set; } = "";

        public int LocalSeat { get; private set; } = -1;

        public IReadOnlyList<TurnSeat> Seats => _seats;

        public bool IsStarted { get; private set; }

        public byte[] Options { get; private set; } = Array.Empty<byte>();

        public byte[] View { get; private set; } = Array.Empty<byte>();

        public IReadOnlyList<TurnPrompt> OpenPrompts => _prompts.Values.OrderBy(p => p.WindowId).ToList();

        /// <summary>
        /// The host let this player in.
        /// </summary>
        public event Action Joined;

        /// <summary>
        /// The table is left behind, with the reason.
        /// </summary>
        public event Action<string> Ended;

        public event Action LobbyChanged;
        public event Action Started;
        public event Action<byte[]> ViewChanged;
        public event Action<TurnNotice> Noticed;
        public event Action<TurnPrompt> Prompted;
        public event Action<int> PromptClosed;
        public event Action<int, TurnOutcome> Replied;

        public void Join(string address, int port) => Begin(address, () => _network.StartClient(address, port));

        /// <summary>
        /// Joins over a transport that already connects (a relay room's join code).
        /// </summary>
        public void JoinSession(string joinCode) => Begin(joinCode, () => _network.StartClient());

        public int Submit(TurnIntent intent)
        {
            if (intent == null)
                throw new ArgumentNullException(nameof(intent));
            if (State != TurnClientState.Seated || !IsStarted || !_network.IsConnected)
                return -1;
            int request = ++_nextRequest;
            _network.SendToServer(new TurnIntentMessage { RequestId = request, Intent = intent });
            return request;
        }

        public void Answer(int windowId, int option)
        {
            if (State != TurnClientState.Seated || !_prompts.ContainsKey(windowId) || !_network.IsConnected)
                return;
            _network.SendToServer(new TurnAnswerMessage { WindowId = windowId, Option = option });
        }

        public void SetReady(bool ready)
        {
            if (State == TurnClientState.Seated && !IsStarted && _network.IsConnected)
                _network.SendToServer(new TurnReadyMessage { Ready = ready });
        }

        public void Update(float deltaTime)
        {
            _network.Update(deltaTime);
            if (_pendingEnd == null)
                return;
            // Ended from inside a message handler: the network is stopped here, outside its own update
            string reason = _pendingEnd;
            _pendingEnd = null;
            End(reason);
        }

        /// <summary>
        /// Leaves the table. In a match the seat stays for <see cref="TurnHostOptions.RejoinWindow"/> seconds, so
        /// joining again with <see cref="RejoinToken"/> takes it back.
        /// </summary>
        public void Leave()
        {
            if (State == TurnClientState.Connecting || State == TurnClientState.Seated)
                End("You left the table.");
        }

        public void Dispose()
        {
            Leave();
            foreach (var handler in _handlers)
                handler.Dispose();
            _handlers.Clear();
            _network.PeerConnected -= OnConnected;
            _network.PeerDisconnected -= OnDisconnected;
            _network.PeerDropped -= OnDropped;
        }

        private void Begin(string address, Action start)
        {
            if (State == TurnClientState.Connecting || State == TurnClientState.Seated)
                throw new InvalidOperationException("Already at a table");
            _address = address ?? "";
            EndReason = "";
            LocalSeat = -1;
            IsStarted = false;
            Options = Array.Empty<byte>();
            View = Array.Empty<byte>();
            _viewNumber = int.MinValue;
            _prompts.Clear();
            _seats = new List<TurnSeat>();
            State = TurnClientState.Connecting;
            try
            {
                start();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                State = TurnClientState.Idle;
                throw;
            }
        }

        private void OnConnected(int connectionId) => _network.SendToServer(new TurnHelloMessage
        {
            Fingerprint = _fingerprint, Name = PlayerName ?? "", RejoinToken = RejoinToken ?? ""
        });

        private void OnDisconnected(int connectionId)
        {
            if (_pendingEnd != null)
                return;
            if (State == TurnClientState.Connecting)
                _pendingEnd = $"Could not reach a table at {_address}.";
            else if (State == TurnClientState.Seated)
                _pendingEnd = "The host left.";
        }

        private void OnDropped(int connectionId, PeerDropReason reason)
        {
            if (State == TurnClientState.Connecting)
                _pendingEnd = $"Could not reach a table at {_address}.";
            else if (State == TurnClientState.Seated)
                _pendingEnd = reason == PeerDropReason.TimedOut
                    ? "Lost the connection to the host."
                    : "The host sent data this game cannot read.";
        }

        private void OnWelcome(int connectionId, TurnWelcomeMessage welcome)
        {
            if (State != TurnClientState.Connecting)
                return;
            if (!string.IsNullOrEmpty(welcome.Refusal) || welcome.Seat < 0)
            {
                _pendingEnd = string.IsNullOrEmpty(welcome.Refusal) ? "The host said no." : welcome.Refusal;
                return;
            }
            LocalSeat = welcome.Seat;
            RejoinToken = welcome.RejoinToken ?? "";
            State = TurnClientState.Seated;
            _logger?.LogInfo($"Seated at {_address} as seat {LocalSeat}", Category);
            Joined?.Invoke();
        }

        private void OnLobby(int connectionId, TurnLobbyMessage lobby)
        {
            if (State != TurnClientState.Seated && State != TurnClientState.Connecting)
                return;
            _seats = lobby.Seats ?? new List<TurnSeat>();
            LobbyChanged?.Invoke();
        }

        private void OnStarted(int connectionId, TurnStartedMessage started)
        {
            if (State != TurnClientState.Seated || IsStarted)
                return;
            IsStarted = true;
            Options = started.Options ?? Array.Empty<byte>();
            Started?.Invoke();
        }

        private void OnView(int connectionId, TurnViewMessage message)
        {
            // An older view arriving late is dropped (a rejoin's view may repeat the newest number)
            if (State != TurnClientState.Seated || message.Number < _viewNumber)
                return;
            _viewNumber = message.Number;
            View = message.View ?? Array.Empty<byte>();
            ViewChanged?.Invoke(View);
        }

        private void OnReply(int connectionId, TurnReplyMessage reply)
        {
            if (State != TurnClientState.Seated)
                return;
            Replied?.Invoke(reply.RequestId, reply.Accepted ? TurnOutcome.Ok : TurnOutcome.Refused(reply.Reason));
        }

        private void OnNotice(int connectionId, TurnNoticeMessage notice)
        {
            if (State == TurnClientState.Seated)
                Noticed?.Invoke(new TurnNotice(notice.Kind, notice.Data));
        }

        private void OnPrompt(int connectionId, TurnPromptMessage message)
        {
            if (State != TurnClientState.Seated)
                return;
            var prompt = new TurnPrompt(message.WindowId, message.Kind, message.Options, message.Seconds, message.Data);
            _prompts[prompt.WindowId] = prompt;
            Prompted?.Invoke(prompt);
        }

        private void OnPromptClosed(int connectionId, TurnPromptClosedMessage message)
        {
            if (_prompts.Remove(message.WindowId))
                PromptClosed?.Invoke(message.WindowId);
        }

        private void End(string reason)
        {
            if (_network.IsConnected || _network.Role != NetworkRole.None)
                _network.Stop();
            State = TurnClientState.Ended;
            IsStarted = false;
            _prompts.Clear();
            EndReason = reason;
            _logger?.LogInfo($"Left the table: {reason}", Category);
            Ended?.Invoke(reason);
        }
    }
}
