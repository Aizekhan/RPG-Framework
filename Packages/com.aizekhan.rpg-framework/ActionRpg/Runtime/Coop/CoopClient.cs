using System;
using System.Collections.Generic;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Networking;

namespace RPGFramework.ActionRpg.Coop
{
    public enum CoopClientState
    {
        Idle,
        Connecting,
        Playing,
        Ended
    }

    /// <summary>
    /// How a game with the host ended.
    /// </summary>
    public enum CoopEndKind
    {
        None,

        /// <summary>
        /// This player left.
        /// </summary>
        Left,

        /// <summary>
        /// The host said no (another version, a full party).
        /// </summary>
        Refused,

        /// <summary>
        /// Nobody answered at that address or code.
        /// </summary>
        Unreachable,

        /// <summary>
        /// The host closed the game.
        /// </summary>
        HostLeft,

        /// <summary>
        /// Nothing came from the host for <see cref="CoopProtocol.ConnectionTimeout"/> seconds: worth joining again,
        /// with <see cref="CoopClient.RejoinToken"/> to get the same seat.
        /// </summary>
        ConnectionLost,

        /// <summary>
        /// The host kept sending what this game cannot read.
        /// </summary>
        BadData
    }

    /// <summary>
    /// Joins a friend's game: says hello with this game's fingerprint, then mirrors the host's world in
    /// <see cref="World"/> and sends this player's movement and actions. Call <see cref="Update"/> every frame.
    /// </summary>
    public sealed class CoopClient : IDisposable
    {
        private const string Category = "Coop";

        private readonly INetworkManager _network;
        private readonly string _fingerprint;
        private readonly ILogger _logger;
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly IEventBus _bus;
        private readonly HashSet<int> _incoming = new HashSet<int>();
        private string _address = "";
        private float _sinceSend;
        private string _pendingEnd;
        private CoopEndKind _pendingKind;

        /// <param name="network">An unstarted network manager made by <see cref="CoopProtocol.CreateNetwork"/>.</param>
        /// <param name="motion">Where characters are; null sends and applies no movement.</param>
        public CoopClient(ActionRpgSettings settings, ActionRpgContent content, IEventBus bus, INetworkManager network,
            IActorMotion motion, ILogger logger = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            Motion = motion;
            _logger = logger;
            _fingerprint = CoopProtocol.Fingerprint(settings, content);
            _bus = bus;
            World = new RemoteWorld(settings, content, bus, intent => _network.SendToServer(intent));

            _network.PeerConnected += OnConnected;
            _network.PeerDisconnected += OnDisconnected;
            _network.PeerDropped += OnDropped;
            _network.RegisterMessageHandler<CoopWelcomeMessage>(OnWelcome).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopWorldMessage>((_, message) => World.Apply(message)).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopMotionMessage>(OnMotion).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopCorrectionMessage>(OnCorrection).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopIncomingAttackMessage>(OnIncomingAttack).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopSceneMessage>(OnScene).AddTo(_subscriptions);
        }

        /// <summary>
        /// Handles a game message from the host (<see cref="CoopGameMessages"/>). Dispose the result to stop.
        /// </summary>
        public IDisposable On<T>(Action<T> handler) where T : INetworkMessage
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            return _network.RegisterMessageHandler<T>((_, message) => handler(message));
        }

        /// <summary>
        /// Handles the newest state the host sends with <see cref="CoopHost.SendLatestToAll"/>: a packet older than one
        /// already handled (it came late over the unreliable channel) is dropped. Dispose the result to stop.
        /// </summary>
        public IDisposable OnLatest<T>(Action<T> handler) where T : INetworkMessage
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            long newest = long.MinValue;
            return _network.RegisterMessageHandler<T>((_, message) =>
            {
                if (message.Timestamp <= newest)
                    return;
                newest = message.Timestamp;
                handler(message);
            });
        }

        /// <summary>
        /// Sends a game message to the host (<see cref="CoopGameMessages"/>).
        /// </summary>
        /// <returns>False when not playing.</returns>
        public bool Send(INetworkMessage message, bool reliable = true)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (State != CoopClientState.Playing || !_network.IsConnected)
                return false;
            _network.SendToServer(message, reliable);
            return true;
        }

        /// <summary>
        /// Attacks on this player the host is waiting to hear about (<see cref="DefenseAuthority.Owner"/>).
        /// </summary>
        public IReadOnlyCollection<int> IncomingAttacks => _incoming;

        /// <summary>
        /// Answers an announced attack (<see cref="IncomingAttackEvent"/>): dodged, blocked (with the share of
        /// damage that still lands) or hit. Each attack is answered once.
        /// </summary>
        /// <returns>False when there is no such attack waiting.</returns>
        public bool Defend(int attackId, DefenseOutcome outcome, float damageMultiplier = 0f)
        {
            if (!_incoming.Remove(attackId) || State != CoopClientState.Playing)
                return false;
            _network.SendToServer(new CoopDefenseResultMessage
            {
                AttackId = attackId, Outcome = outcome, DamageMultiplier = damageMultiplier
            });
            return true;
        }

        private void OnIncomingAttack(int connectionId, CoopIncomingAttackMessage attack)
        {
            if (State != CoopClientState.Playing || attack.TargetId != World.PlayerId)
                return;
            _incoming.Add(attack.AttackId);
            var context = new AttackContext(attack.Tags, attack.SourceId).Between(attack.AttackerId, attack.TargetId);
            _bus?.Publish(new IncomingAttackEvent(attack.AttackId, attack.AttackerId, attack.TargetId, attack.HitIn, context));
        }

        public RemoteWorld World { get; }

        /// <summary>
        /// Where characters are; settable because the views that implement it usually need <see cref="World"/> first.
        /// </summary>
        public IActorMotion Motion { get; set; }

        /// <summary>
        /// Times the host moved this player (too fast, or to a spawn point).
        /// </summary>
        public int Corrections { get; private set; }
        public CoopClientState State { get; private set; } = CoopClientState.Idle;

        /// <summary>
        /// The name others see; set before <see cref="Join"/>. The host cleans it and makes it unique.
        /// </summary>
        public string PlayerName { get; set; } = "";

        /// <summary>
        /// Why the game ended (refused, host gone, unreachable), for the player to read.
        /// </summary>
        public string EndReason { get; private set; } = "";

        public CoopEndKind EndKind { get; private set; }

        /// <summary>
        /// Lets this player back into their seat after a lost link: given by the host on joining; set it on a new
        /// client before <see cref="Join"/> to come back as the same player.
        /// </summary>
        public string RejoinToken { get; set; } = "";

        /// <summary>
        /// The progress this player brings when every player has their own (<see cref="ProgressMode.PerPlayer"/>):
        /// their saved progress, set before <see cref="Join"/>. Take it home with <see cref="RemoteWorld.CaptureProgress"/>.
        /// </summary>
        public PlayerProgress Profile { get; set; }

        /// <summary>
        /// The host put this player back into the seat they had.
        /// </summary>
        public bool Rejoined { get; private set; }

        /// <summary>
        /// Nothing has come from the host for a while (<see cref="CoopProtocol.UnstableAfter"/> seconds).
        /// </summary>
        public bool IsUnstable =>
            State == CoopClientState.Playing && _network.SecondsSinceHeard(_network.LocalConnectionId) > CoopProtocol.UnstableAfter;

        /// <summary>
        /// The host let this player in.
        /// </summary>
        public event Action Joined;

        /// <summary>
        /// The scene the host's game is in (empty until the host names one).
        /// </summary>
        public string Scene { get; private set; } = "";

        /// <summary>
        /// The host's game moved to another scene, or this player joined a game in one: load it to play there.
        /// </summary>
        public event Action<string> SceneChanged;

        /// <summary>
        /// The game with the host ended, with the reason.
        /// </summary>
        public event Action<string> Ended;

        /// <summary>
        /// Joins the game at this address and port.
        /// </summary>
        public void Join(string address, int port) => Begin(address, () => _network.StartClient(address, port));

        /// <summary>
        /// Joins over a transport that already connects (from <see cref="INetworkSessionProvider.JoinAsync"/>).
        /// </summary>
        /// <param name="joinCode">The code it joins with, for messages ("Could not reach a game at ...").</param>
        public void JoinSession(string joinCode) => Begin(joinCode, () => _network.StartClient());

        private void Begin(string address, Action start)
        {
            if (State == CoopClientState.Connecting || State == CoopClientState.Playing)
                throw new InvalidOperationException("Already joining a game");
            _address = address;
            EndReason = "";
            EndKind = CoopEndKind.None;
            Rejoined = false;
            State = CoopClientState.Connecting;
            try
            {
                start();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                State = CoopClientState.Idle;
                throw;
            }
        }

        public void Update(float deltaTime)
        {
            World.Tick(deltaTime);
            _network.Update(deltaTime);
            if (_pendingEnd != null)
            {
                // Ended from inside a message handler: the network is stopped here, outside its own update
                string reason = _pendingEnd;
                _pendingEnd = null;
                End(reason, _pendingKind);
                return;
            }
            if (State != CoopClientState.Playing || Motion == null)
                return;
            _sinceSend += deltaTime;
            if (_sinceSend < 1f / CoopProtocol.SendRate)
                return;
            _sinceSend = 0f;
            SendMotion();
        }

        /// <summary>
        /// Sends this player's position now instead of at the next tick.
        /// </summary>
        public void SendMotion()
        {
            if (State != CoopClientState.Playing || Motion == null || !Motion.TryGetPose(World.PlayerId, out var pose))
                return;
            var message = new CoopMotionMessage();
            message.Poses.Add((World.PlayerId, pose));
            _network.SendToServer(message, reliable: false);
        }

        public void Leave()
        {
            // Tell the host this is on purpose, so it keeps no seat
            if (State == CoopClientState.Playing && _network.IsConnected)
                _network.SendToServer(new CoopIntentMessage { Kind = CoopIntentMessage.IntentKind.Leave });
            if (State == CoopClientState.Connecting || State == CoopClientState.Playing)
                End("You left the game.", CoopEndKind.Left);
        }

        public void Dispose()
        {
            Leave();
            _subscriptions.Dispose();
            _network.PeerConnected -= OnConnected;
            _network.PeerDisconnected -= OnDisconnected;
            _network.PeerDropped -= OnDropped;
        }

        private void OnConnected(int connectionId) => _network.SendToServer(new CoopHelloMessage
        {
            Fingerprint = _fingerprint, Name = PlayerName, RejoinToken = RejoinToken ?? "", Profile = Profile
        });

        private void OnDisconnected(int connectionId)
        {
            if (_pendingEnd != null)
                return; // already known why (dropped, refused)
            if (State == CoopClientState.Connecting)
                EndLater($"Could not reach a game at {_address}.", CoopEndKind.Unreachable);
            else if (State == CoopClientState.Playing)
                EndLater("The host left the game.", CoopEndKind.HostLeft);
        }

        private void OnDropped(int connectionId, PeerDropReason reason)
        {
            if (State == CoopClientState.Connecting)
                EndLater($"Could not reach a game at {_address}.", CoopEndKind.Unreachable);
            else if (State != CoopClientState.Playing)
                return;
            else if (reason == PeerDropReason.TimedOut)
                EndLater("Lost the connection to the host.", CoopEndKind.ConnectionLost);
            else
                EndLater("The host sent data this game cannot read.", CoopEndKind.BadData);
        }

        private void EndLater(string reason, CoopEndKind kind)
        {
            _pendingEnd = reason;
            _pendingKind = kind;
        }

        private void OnScene(int connectionId, CoopSceneMessage message)
        {
            if (string.IsNullOrEmpty(message.Scene))
                return;
            Scene = message.Scene;
            SceneChanged?.Invoke(Scene);
        }

        private void OnWelcome(int connectionId, CoopWelcomeMessage welcome)
        {
            if (State != CoopClientState.Connecting)
                return;
            if (!welcome.Accepted)
            {
                EndLater(welcome.Reason, CoopEndKind.Refused);
                return;
            }
            RejoinToken = welcome.RejoinToken ?? "";
            Rejoined = welcome.Rejoined;
            World.Begin(welcome.PlayerId);
            State = CoopClientState.Playing;
            _logger?.LogInfo($"Joined {_address} as player {welcome.PlayerId}", Category);
            Joined?.Invoke();
        }

        private void OnMotion(int connectionId, CoopMotionMessage motion)
        {
            if (Motion == null || State != CoopClientState.Playing)
                return;
            foreach (var (id, pose) in motion.Poses)
            {
                // This player moves themselves
                if (id != World.PlayerId && pose.IsFinite && World.Get(id) != null)
                    Motion.SetTargetPose(id, pose);
            }
        }

        // The host put this player somewhere else: go there at once, and tell it so from the new place
        private void OnCorrection(int connectionId, CoopCorrectionMessage correction)
        {
            if (Motion == null || State != CoopClientState.Playing || !correction.Pose.IsFinite)
                return;
            Corrections++;
            Motion.Teleport(World.PlayerId, correction.Pose);
        }

        private void End(string reason, CoopEndKind kind)
        {
            if (_network.IsConnected || _network.Role != NetworkRole.None)
                _network.Stop();
            World.End();
            _incoming.Clear();
            State = CoopClientState.Ended;
            EndReason = reason;
            EndKind = kind;
            _logger?.LogInfo($"Co-op ended: {reason}", Category);
            Ended?.Invoke(reason);
        }
    }
}
