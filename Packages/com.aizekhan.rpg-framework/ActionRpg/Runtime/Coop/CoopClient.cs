using System;
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
        private string _address = "";
        private float _sinceSend;
        private string _pendingEnd;

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
            World = new RemoteWorld(settings, content, bus, intent => _network.SendToServer(intent));

            _network.PeerConnected += OnConnected;
            _network.PeerDisconnected += OnDisconnected;
            _network.RegisterMessageHandler<CoopWelcomeMessage>(OnWelcome).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopWorldMessage>((_, message) => World.Apply(message)).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopMotionMessage>(OnMotion).AddTo(_subscriptions);
        }

        public RemoteWorld World { get; }

        /// <summary>
        /// Where characters are; settable because the views that implement it usually need <see cref="World"/> first.
        /// </summary>
        public IActorMotion Motion { get; set; }
        public CoopClientState State { get; private set; } = CoopClientState.Idle;

        /// <summary>
        /// The name others see; set before <see cref="Join"/>. The host cleans it and makes it unique.
        /// </summary>
        public string PlayerName { get; set; } = "";

        /// <summary>
        /// Why the game ended (refused, host gone, unreachable), for the player to read.
        /// </summary>
        public string EndReason { get; private set; } = "";

        /// <summary>
        /// The host let this player in.
        /// </summary>
        public event Action Joined;

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
            _network.Update(deltaTime);
            if (_pendingEnd != null)
            {
                // Ended from inside a message handler: the network is stopped here, outside its own update
                string reason = _pendingEnd;
                _pendingEnd = null;
                End(reason);
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
            if (State == CoopClientState.Connecting || State == CoopClientState.Playing)
                End("You left the game.");
        }

        public void Dispose()
        {
            Leave();
            _subscriptions.Dispose();
            _network.PeerConnected -= OnConnected;
            _network.PeerDisconnected -= OnDisconnected;
        }

        private void OnConnected(int connectionId) =>
            _network.SendToServer(new CoopHelloMessage { Fingerprint = _fingerprint, Name = PlayerName });

        private void OnDisconnected(int connectionId)
        {
            if (State == CoopClientState.Connecting)
                _pendingEnd = $"Could not reach a game at {_address}.";
            else if (State == CoopClientState.Playing)
                _pendingEnd = "The host left the game.";
        }

        private void OnWelcome(int connectionId, CoopWelcomeMessage welcome)
        {
            if (State != CoopClientState.Connecting)
                return;
            if (!welcome.Accepted)
            {
                _pendingEnd = welcome.Reason;
                return;
            }
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
                if (id != World.PlayerId && World.Get(id) != null)
                    Motion.SetTargetPose(id, pose);
            }
        }

        private void End(string reason)
        {
            if (_network.IsConnected || _network.Role != NetworkRole.None)
                _network.Stop();
            World.End();
            State = CoopClientState.Ended;
            EndReason = reason;
            _logger?.LogInfo($"Co-op ended: {reason}", Category);
            Ended?.Invoke(reason);
        }
    }
}
