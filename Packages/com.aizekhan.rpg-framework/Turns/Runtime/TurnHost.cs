using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Logging;
using RPGFramework.Networking;

namespace RPGFramework.Turns
{
    public sealed class TurnHostOptions
    {
        /// <summary>
        /// Seats at the table, the host's included.
        /// </summary>
        public int MaxSeats { get; set; } = TurnProtocol.DefaultMaxSeats;

        /// <summary>
        /// The game and its rules version; players with another are refused.
        /// </summary>
        public string Fingerprint { get; set; } = "";

        /// <summary>
        /// Seconds a player has for a turn before <see cref="ITurnRules.DefaultIntent"/> is made for them (also while
        /// they are away); 0: no limit.
        /// </summary>
        public float TurnSeconds { get; set; }

        public float RejoinWindow { get; set; } = TurnProtocol.RejoinWindow;
    }

    /// <summary>
    /// Hosts a turn-based match: players join a lobby, the host starts it with the game's rules, every move is
    /// checked against them here (only those the rules let a seat make now), and every seat gets its own view after
    /// each change, so hidden cards stay hidden. Questions to players not on turn (block, pass) go through
    /// <see cref="OpenPrompt"/>. A player who drops keeps their seat and comes back with their rejoin token.
    /// The host plays at seat 0.
    /// </summary>
    public sealed class TurnHost : ITurnSession, IDisposable
    {
        private const string Category = "Turns";

        private sealed class Place
        {
            public TurnSeat Seat;
            public int ConnectionId = -1;
            public string Token = "";
            public float AwayFor;
        }

        private sealed class Window
        {
            public TurnPrompt Prompt;
            public HashSet<int> Seats;
            public Dictionary<int, int> Answers = new Dictionary<int, int>();
            public float Left;
            public Action<IReadOnlyDictionary<int, int>> Closed;
        }

        private readonly INetworkManager _network;
        private readonly TurnHostOptions _options;
        private readonly ILogger _logger;
        private readonly List<Place> _places = new List<Place>();
        private readonly Dictionary<int, Place> _byConnection = new Dictionary<int, Place>();
        private readonly Dictionary<int, Window> _windows = new Dictionary<int, Window>();
        private readonly List<IDisposable> _handlers = new List<IDisposable>();
        private ITurnRules _rules;
        private int _nextWindow;
        private int _nextRequest;
        private int _viewNumber;
        private int _turnSeat = -1;
        private bool _applying;
        private bool _viewsDirty;

        public TurnHost(INetworkManager network, string hostName, TurnHostOptions options = null, ILogger logger = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _options = options ?? new TurnHostOptions();
            _logger = logger;
            _places.Add(new Place { Seat = new TurnSeat { Seat = 0, Name = string.IsNullOrEmpty(hostName) ? "Host" : hostName } });
            _network.PeerDisconnected += OnPeerDisconnected;
            _handlers.Add(_network.RegisterMessageHandler<TurnHelloMessage>(OnHello));
            _handlers.Add(_network.RegisterMessageHandler<TurnReadyMessage>(OnReady));
            _handlers.Add(_network.RegisterMessageHandler<TurnIntentMessage>(OnIntent));
            _handlers.Add(_network.RegisterMessageHandler<TurnAnswerMessage>(OnAnswer));
        }

        public int LocalSeat => 0;

        public IReadOnlyList<TurnSeat> Seats => _places.Select(p => p.Seat).ToList();

        public bool IsStarted => _rules != null;

        public byte[] Options { get; private set; } = Array.Empty<byte>();

        public byte[] View { get; private set; } = Array.Empty<byte>();

        public IReadOnlyList<TurnPrompt> OpenPrompts =>
            _windows.Values.Where(w => w.Seats.Contains(0) && !w.Answers.ContainsKey(0)).Select(w => w.Prompt).ToList();

        /// <summary>
        /// Every seat in the lobby is ready (the host's included).
        /// </summary>
        public bool AllReady => _places.Count > 1 && _places.All(p => p.Seat.Ready);

        /// <summary>
        /// Seconds left of the current turn; 0 without a limit.
        /// </summary>
        public float TurnTimeLeft { get; private set; }

        public event Action LobbyChanged;
        public event Action Started;
        public event Action<byte[]> ViewChanged;
        public event Action<TurnNotice> Noticed;
        public event Action<TurnPrompt> Prompted;
        public event Action<int> PromptClosed;
        public event Action<int, TurnOutcome> Replied;

        /// <summary>
        /// A move was made (seat, move): for the match record (seed + moves replays it).
        /// </summary>
        public event Action<int, TurnIntent> Accepted;

        public void Open(int port) => _network.StartServer(port);

        /// <summary>
        /// Opens on a session provider's transport (a relay room).
        /// </summary>
        public void Open() => _network.StartServer();

        public void SetReady(bool ready)
        {
            _places[0].Seat.Ready = ready;
            LobbyHasChanged();
        }

        /// <summary>
        /// Starts the match with the game's rules (made for the seats in <see cref="Seats"/>) and options for the
        /// players' screens. Players who join after this are refused; those away may come back.
        /// </summary>
        public void Start(ITurnRules rules, byte[] options = null)
        {
            if (IsStarted)
                throw new InvalidOperationException("The match has already started");
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Options = options ?? Array.Empty<byte>();
            _network.Broadcast(new TurnStartedMessage { Options = Options });
            Started?.Invoke();
            ResetTurnTimer();
            SendViews();
        }

        public int Submit(TurnIntent intent)
        {
            int request = ++_nextRequest;
            var outcome = Act(0, intent);
            Replied?.Invoke(request, outcome);
            return request;
        }

        /// <summary>
        /// Makes a move for a seat, as if it came from that seat's player (tests, a bot sitting in an empty seat).
        /// </summary>
        public TurnOutcome Act(int seat, TurnIntent intent)
        {
            if (intent == null)
                return TurnOutcome.Refused("No move");
            if (!IsStarted)
                return TurnOutcome.Refused("The match has not started");
            if (_applying)
                return TurnOutcome.Refused("Another move is being made");
            if (!_rules.CanAct(seat, intent))
                return TurnOutcome.Refused("Not your move now");
            _applying = true;
            TurnOutcome outcome;
            try
            {
                outcome = _rules.Apply(seat, intent);
            }
            finally
            {
                _applying = false;
            }
            if (outcome.Accepted)
            {
                Accepted?.Invoke(seat, intent);
                ResetTurnTimer();
                SendViews();
            }
            return outcome;
        }

        /// <summary>
        /// Tells seats what just happened (a die rolled, a card drawn), for their screens to show.
        /// </summary>
        /// <param name="seat">The seat it is for with <see cref="TurnVisibility.Seat"/>, the one left out with
        /// <see cref="TurnVisibility.Others"/>.</param>
        public void Notify(TurnNotice notice, TurnVisibility visibility = TurnVisibility.All, int seat = -1)
        {
            if (notice == null)
                throw new ArgumentNullException(nameof(notice));
            var message = new TurnNoticeMessage { Kind = notice.Kind, Data = notice.Data };
            switch (visibility)
            {
                case TurnVisibility.All:
                    _network.Broadcast(message);
                    Noticed?.Invoke(notice);
                    break;
                case TurnVisibility.Seat:
                    if (seat == 0)
                        Noticed?.Invoke(notice);
                    else
                        SendTo(seat, message);
                    break;
                case TurnVisibility.Host:
                    Noticed?.Invoke(notice);
                    break;
                case TurnVisibility.Others:
                    if (seat != 0)
                        Noticed?.Invoke(notice);
                    foreach (var place in _places)
                    {
                        if (place.Seat.Seat != 0 && place.Seat.Seat != seat && place.ConnectionId >= 0)
                            _network.Send(place.ConnectionId, message);
                    }
                    break;
            }
        }

        /// <summary>
        /// Asks seats a question (block it? pay to stop it?) that they answer out of turn. It closes when every one of
        /// them answered (away players pass) or the time is up (the rest pass); then <paramref name="closed"/> gets the
        /// answers by seat and every seat a new view.
        /// </summary>
        /// <param name="seconds">0 or less: no limit.</param>
        /// <param name="title">What to ask the players, for their screens.</param>
        /// <returns>The window id.</returns>
        public int OpenPrompt(string kind, IReadOnlyList<string> options, IEnumerable<int> seats, float seconds,
            Action<IReadOnlyDictionary<int, int>> closed, byte[] data = null, string title = null)
        {
            if (seats == null)
                throw new ArgumentNullException(nameof(seats));
            int id = ++_nextWindow;
            var window = new Window
            {
                Prompt = new TurnPrompt(id, kind, options, seconds, data, title),
                Seats = new HashSet<int>(seats.Where(s => PlaceOf(s) != null)),
                Left = seconds,
                Closed = closed
            };
            _windows[id] = window;
            var message = new TurnPromptMessage
            {
                WindowId = id, Kind = window.Prompt.Kind, Title = window.Prompt.Title, Options = window.Prompt.Options.ToList(), Seconds = seconds,
                Data = window.Prompt.Data
            };
            foreach (int seat in window.Seats)
            {
                if (seat == 0)
                    Prompted?.Invoke(window.Prompt);
                else
                    SendTo(seat, message);
            }
            CloseIfAnswered(window);
            return id;
        }

        /// <summary>
        /// Closes a question without waiting (everyone not answered passes).
        /// </summary>
        public void ClosePrompt(int windowId)
        {
            if (_windows.TryGetValue(windowId, out var window))
                Close(window);
        }

        public void Answer(int windowId, int option) => TakeAnswer(0, windowId, option);

        /// <summary>
        /// Sends every seat its view again (after something changed outside a move).
        /// </summary>
        public void Refresh() => SendViews();

        public void Update(float deltaTime)
        {
            _network.Update(deltaTime);
            foreach (var place in _places)
            {
                if (!place.Seat.Connected)
                    place.AwayFor += deltaTime;
            }
            foreach (var window in _windows.Values.ToList())
            {
                if (window.Prompt.Seconds <= 0f)
                    continue;
                window.Left -= deltaTime;
                if (window.Left <= 0f)
                    Close(window);
            }
            UpdateTurnTimer(deltaTime);
            if (_viewsDirty)
                SendViews();
        }

        public void Stop()
        {
            _network.Stop();
            _rules = null;
        }

        public void Dispose()
        {
            _network.PeerDisconnected -= OnPeerDisconnected;
            foreach (var handler in _handlers)
                handler.Dispose();
            _handlers.Clear();
            _network.Stop();
        }

        private void OnHello(int connectionId, TurnHelloMessage hello)
        {
            if (_byConnection.ContainsKey(connectionId))
                return;
            string refusal = null;
            if (hello.Version != TurnProtocol.Version)
                refusal = $"Another version of the turn protocol ({hello.Version}, the host has {TurnProtocol.Version})";
            else if (!string.Equals(hello.Fingerprint ?? "", _options.Fingerprint ?? "", StringComparison.Ordinal))
                refusal = "Another version of the game";

            Place place = null;
            if (refusal == null && !string.IsNullOrEmpty(hello.RejoinToken))
            {
                place = _places.FirstOrDefault(p => p.Seat.Seat != 0 && p.Token == hello.RejoinToken && !p.Seat.Connected);
                if (place != null && place.AwayFor > _options.RejoinWindow)
                {
                    place = null;
                    refusal = "Away too long; the seat is gone";
                }
            }
            if (refusal == null && place == null)
            {
                if (IsStarted)
                    refusal = "The match has already started";
                else if (_places.Count >= Math.Max(1, _options.MaxSeats))
                    refusal = "The table is full";
            }
            if (refusal != null)
            {
                _logger?.LogInfo($"Refused connection {connectionId}: {refusal}", Category);
                _network.Send(connectionId, new TurnWelcomeMessage { Refusal = refusal });
                return;
            }

            bool back = place != null;
            if (place == null)
            {
                int seat = 1;
                while (_places.Any(p => p.Seat.Seat == seat))
                    seat++;
                place = new Place
                {
                    Seat = new TurnSeat { Seat = seat },
                    Token = Guid.NewGuid().ToString("N")
                };
                _places.Add(place);
                _places.Sort((a, b) => a.Seat.Seat.CompareTo(b.Seat.Seat));
            }
            place.Seat.Name = string.IsNullOrEmpty(hello.Name) ? (back ? place.Seat.Name : "Player " + (place.Seat.Seat + 1)) : hello.Name;
            place.Seat.Connected = true;
            place.AwayFor = 0f;
            place.ConnectionId = connectionId;
            _byConnection[connectionId] = place;
            _network.Send(connectionId, new TurnWelcomeMessage { Seat = place.Seat.Seat, RejoinToken = place.Token });
            LobbyHasChanged();
            if (!IsStarted)
                return;

            // Back in a match: the options, the view and the questions still open for them
            _network.Send(connectionId, new TurnStartedMessage { Options = Options });
            SendView(place);
            foreach (var window in _windows.Values)
            {
                if (!window.Seats.Contains(place.Seat.Seat) || window.Answers.ContainsKey(place.Seat.Seat))
                    continue;
                _network.Send(connectionId, new TurnPromptMessage
                {
                    WindowId = window.Prompt.WindowId, Kind = window.Prompt.Kind, Title = window.Prompt.Title, Options = window.Prompt.Options.ToList(),
                    Seconds = Math.Max(0f, window.Left), Data = window.Prompt.Data
                });
            }
        }

        private void OnPeerDisconnected(int connectionId)
        {
            if (!_byConnection.TryGetValue(connectionId, out var place))
                return;
            _byConnection.Remove(connectionId);
            place.ConnectionId = -1;
            if (!IsStarted)
            {
                _places.Remove(place);
            }
            else
            {
                place.Seat.Connected = false;
                place.AwayFor = 0f;
                foreach (var window in _windows.Values.ToList())
                    CloseIfAnswered(window);
            }
            LobbyHasChanged();
        }

        private void OnReady(int connectionId, TurnReadyMessage ready)
        {
            if (!_byConnection.TryGetValue(connectionId, out var place) || IsStarted)
                return;
            place.Seat.Ready = ready.Ready;
            LobbyHasChanged();
        }

        private void OnIntent(int connectionId, TurnIntentMessage message)
        {
            if (!_byConnection.TryGetValue(connectionId, out var place))
                return;
            var outcome = Act(place.Seat.Seat, message.Intent);
            _network.Send(connectionId, new TurnReplyMessage
            {
                RequestId = message.RequestId, Accepted = outcome.Accepted, Reason = outcome.Reason
            });
        }

        private void OnAnswer(int connectionId, TurnAnswerMessage answer)
        {
            if (_byConnection.TryGetValue(connectionId, out var place))
                TakeAnswer(place.Seat.Seat, answer.WindowId, answer.Option);
        }

        private void TakeAnswer(int seat, int windowId, int option)
        {
            if (!_windows.TryGetValue(windowId, out var window) || !window.Seats.Contains(seat) || window.Answers.ContainsKey(seat))
                return;
            if (option != TurnPrompt.Pass && (option < 0 || option >= window.Prompt.Options.Count))
                return;
            window.Answers[seat] = option;
            CloseIfAnswered(window);
        }

        private void CloseIfAnswered(Window window)
        {
            if (_windows.ContainsKey(window.Prompt.WindowId) &&
                window.Seats.All(seat => window.Answers.ContainsKey(seat) || PlaceOf(seat)?.Seat.Connected != true))
            {
                Close(window);
            }
        }

        private void Close(Window window)
        {
            int id = window.Prompt.WindowId;
            if (!_windows.Remove(id))
                return;
            foreach (int seat in window.Seats)
            {
                if (!window.Answers.ContainsKey(seat))
                    window.Answers[seat] = TurnPrompt.Pass;
                if (seat == 0)
                    PromptClosed?.Invoke(id);
                else
                    SendTo(seat, new TurnPromptClosedMessage { WindowId = id });
            }
            window.Closed?.Invoke(window.Answers);
            ResetTurnTimer();
            // The answers usually change the match; while a move is being made its own views follow
            if (_applying)
                _viewsDirty = true;
            else
                SendViews();
        }

        private void UpdateTurnTimer(float deltaTime)
        {
            if (!IsStarted || _options.TurnSeconds <= 0f)
            {
                TurnTimeLeft = 0f;
                return;
            }
            int seat = _rules.CurrentSeat;
            if (seat != _turnSeat)
                ResetTurnTimer();
            if (seat < 0 || _windows.Count > 0)
                return;
            TurnTimeLeft -= deltaTime;
            if (TurnTimeLeft > 0f)
                return;
            var intent = _rules.DefaultIntent(seat);
            ResetTurnTimer();
            if (intent != null)
            {
                var outcome = Act(seat, intent);
                if (!outcome.Accepted)
                    _logger?.LogWarning($"The move made for seat {seat} when its time ran out was refused: {outcome.Reason}", Category);
            }
        }

        private void ResetTurnTimer()
        {
            _turnSeat = IsStarted ? _rules.CurrentSeat : -1;
            TurnTimeLeft = _options.TurnSeconds;
        }

        private void SendViews()
        {
            _viewsDirty = false;
            if (!IsStarted)
                return;
            _viewNumber++;
            foreach (var place in _places)
                SendView(place);
        }

        private void SendView(Place place)
        {
            byte[] view;
            try
            {
                view = _rules.ViewFor(place.Seat.Seat) ?? Array.Empty<byte>();
            }
            catch (Exception e)
            {
                _logger?.LogError($"The view for seat {place.Seat.Seat} failed: {e.Message}", Category);
                return;
            }
            if (place.Seat.Seat == 0)
            {
                View = view;
                ViewChanged?.Invoke(view);
            }
            else if (place.ConnectionId >= 0)
            {
                _network.Send(place.ConnectionId, new TurnViewMessage { Number = _viewNumber, View = view });
            }
        }

        private void SendTo(int seat, INetworkMessage message)
        {
            var place = PlaceOf(seat);
            if (place != null && place.ConnectionId >= 0)
                _network.Send(place.ConnectionId, message);
        }

        private Place PlaceOf(int seat) => _places.FirstOrDefault(p => p.Seat.Seat == seat);

        private void LobbyHasChanged()
        {
            _network.Broadcast(new TurnLobbyMessage { Seats = _places.Select(p => p.Seat).ToList() });
            LobbyChanged?.Invoke();
        }
    }
}
