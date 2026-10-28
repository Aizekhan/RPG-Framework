using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Networking;
using RPGFramework.StatusEffects;

namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// Opens this machine's game to friends: everyone who joins gets a player in the party, sees the world as the
    /// host has it and sends what they want to do; the host's rules decide. Each joined player gets, in order,
    /// characters entering and leaving, hits, changed numbers and the party's progress — measured against what that
    /// player last saw, so someone joining late gets the whole world at once. Call <see cref="Update"/> every frame.
    /// A player whose link is lost (silence for <see cref="CoopProtocol.ConnectionTimeout"/> seconds) leaves the party,
    /// but their seat, name and health are kept for <see cref="RejoinWindow"/> seconds: joining again with the token
    /// from their welcome puts them back as themselves. Leaving on purpose keeps nothing.
    /// </summary>
    public sealed class CoopHost : IDisposable
    {
        private const string Category = "Coop";

        private sealed class Guest
        {
            public int PlayerId = -1;
            public string Token = "";
            public bool LeavingOnPurpose;
            public readonly Dictionary<int, Actor> Known = new Dictionary<int, Actor>();
            public readonly Dictionary<int, ActorState> States = new Dictionary<int, ActorState>();
            public byte[] Party;

            // Where the host last accepted the player to be, and when (for the speed check)
            public bool Placed;
            public ActorPose Place;
            public float PlacedAt;

            // A boost the game allows (speed multiplier until a time on the host's clock)
            public float Boost = 1f;
            public float BoostUntil = float.NegativeInfinity;

            // After a teleport or a correction: until then, poses sent before the player got there are dropped
            public float SettlingUntil = float.NegativeInfinity;
        }

        private sealed class KeptSeat
        {
            public int PlayerId;
            public int Seat;
            public string Name;
            public float Health;
            public float Since;
            public ActorPose? Pose;

            // Their own progress, when every player has their own: they come back with it
            public PlayerProgress Progress;
        }

        private struct Happening
        {
            public CoopWorldMessage.ChangeKind Kind;
            public Actor Actor;
            public int AttackerId;
            public float Amount;
            public float HealthLeft;
            public AttackContext Context;
            public string Name;
            public int Stacks;
        }

        private readonly ActorWorld _world;
        private readonly INetworkManager _network;
        private readonly IActorMotion _motion;
        private readonly string _fingerprint;
        private readonly ILogger _logger;
        private readonly Dictionary<int, Guest> _guests = new Dictionary<int, Guest>();
        private readonly Dictionary<int, Actor> _present = new Dictionary<int, Actor>();
        private readonly List<Happening> _happenings = new List<Happening>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly Dictionary<string, KeptSeat> _keptSeats = new Dictionary<string, KeptSeat>();
        // With progress of their own: names whose brought profile was taken this session, and what they left with
        private readonly HashSet<string> _profilesTaken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PlayerProgress> _leftWith = new Dictionary<string, PlayerProgress>(StringComparer.OrdinalIgnoreCase);
        private long _stateSequence;
        private readonly IEventBus _bus;
        private readonly List<AnnouncedAttack> _announced = new List<AnnouncedAttack>();
        private int _nextAttackId = 1;
        private bool _settling;
        private float _sinceSend;
        private float _clock;

        private sealed class AnnouncedAttack
        {
            public int Id;
            public int AttackerId;
            public int TargetId;
            public AttackContext Context;
            public float HitAt;

            // Waiting for the attacked player's machine to answer
            public bool AskedOwner;
            public bool Answered;
            public DefenseOutcome Outcome;
            public float Share = 1f;
        }

        /// <param name="world">The game this machine runs.</param>
        /// <param name="bus">The game's event bus (to hear characters enter, leave and get hit).</param>
        /// <param name="network">An unstarted network manager made by <see cref="CoopProtocol.CreateNetwork"/>.</param>
        /// <param name="motion">Where characters are; null sends no movement (tests of the rules alone).</param>
        /// <param name="fingerprint"><see cref="CoopProtocol.Fingerprint"/> of this game.</param>
        public CoopHost(ActorWorld world, IEventBus bus, INetworkManager network, IActorMotion motion, string fingerprint,
            ILogger logger = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _bus = bus;
            _motion = motion;
            _fingerprint = fingerprint ?? "";
            _logger = logger;

            foreach (var actor in world.Actors)
                _present[actor.EntityId] = actor;
            bus.Subscribe<ActorSpawnedEvent>(OnSpawned).AddTo(_subscriptions);
            bus.Subscribe<ActorDespawnedEvent>(OnDespawned).AddTo(_subscriptions);
            bus.Subscribe<ActorDamagedEvent>(OnDamaged).AddTo(_subscriptions);
            bus.Subscribe<EffectAppliedEvent>(OnEffectApplied).AddTo(_subscriptions);
            bus.Subscribe<EffectRemovedEvent>(OnEffectRemoved).AddTo(_subscriptions);
            bus.Subscribe<CooldownStartedEvent>(OnCooldownStarted).AddTo(_subscriptions);

            _network.PeerConnected += OnPeerConnected;
            _network.PeerDisconnected += OnPeerDisconnected;
            _network.RegisterMessageHandler<CoopHelloMessage>(OnHello).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopIntentMessage>(OnIntent).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopMotionMessage>(OnMotion).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopDefenseResultMessage>(OnDefenseResult).AddTo(_subscriptions);
            _world.HoldAttack = HoldAttack;
        }

        /// <summary>
        /// Who decides dodges and blocks of attacks on joined players (<see cref="Coop.DefenseAuthority"/>).
        /// </summary>
        public DefenseAuthority DefenseAuthority { get; set; } = DefenseAuthority.Host;

        /// <summary>
        /// Seconds after an announced strike the host waits for the attacked player's answer before it lands.
        /// </summary>
        public float DefenseWindow { get; set; } = 0.3f;

        /// <summary>
        /// Answers about attacks that were never announced to that player, or came too late.
        /// </summary>
        public int RejectedDefenseResults { get; private set; }

        /// <summary>
        /// Attacks announced and not settled yet.
        /// </summary>
        public int PendingAttacks => _announced.Count;

        /// <summary>
        /// Announces an attack that lands in <paramref name="delay"/> seconds (a wind-up the players can see coming).
        /// With <see cref="DefenseAuthority.Owner"/> a joined player's own machine says how they met it; otherwise
        /// the host's rules settle it when it lands.
        /// </summary>
        /// <returns>The attack's id.</returns>
        public int AnnounceAttack(int attackerId, int targetId, float delay, AttackContext context = default)
        {
            if (float.IsNaN(delay) || delay < 0f)
                throw new ArgumentOutOfRangeException(nameof(delay));
            var attack = new AnnouncedAttack
            {
                Id = _nextAttackId++, AttackerId = attackerId, TargetId = targetId,
                Context = context.Between(attackerId, targetId), HitAt = _clock + delay
            };
            int owner = DefenseAuthority == DefenseAuthority.Owner ? ConnectionOf(targetId) : -1;
            if (owner >= 0 && _network.IsConnected)
            {
                attack.AskedOwner = true;
                _network.Send(owner, new CoopIncomingAttackMessage
                {
                    AttackId = attack.Id, AttackerId = attackerId, TargetId = targetId, HitIn = delay,
                    Tags = context.Tags, SourceId = context.SourceId
                });
            }
            _announced.Add(attack);
            return attack.Id;
        }

        // An attack on a joined player whose own machine defends: announced now, settled by their answer
        private bool HoldAttack(int attackerId, int targetId, AttackContext context)
        {
            if (_settling || DefenseAuthority != DefenseAuthority.Owner || ConnectionOf(targetId) < 0)
                return false;
            AnnounceAttack(attackerId, targetId, 0f, context);
            return true;
        }

        private int ConnectionOf(int playerId)
        {
            foreach (var pair in _guests)
            {
                if (playerId >= 0 && pair.Value.PlayerId == playerId)
                    return pair.Key;
            }
            return -1;
        }

        private void OnDefenseResult(int connectionId, CoopDefenseResultMessage result)
        {
            var attack = _guests.TryGetValue(connectionId, out var guest) && guest.PlayerId >= 0
                ? _announced.FirstOrDefault(a => a.Id == result.AttackId && a.AskedOwner && !a.Answered && a.TargetId == guest.PlayerId)
                : null;
            if (attack == null || _clock > attack.HitAt + DefenseWindow || !Enum.IsDefined(typeof(DefenseOutcome), result.Outcome))
            {
                RejectedDefenseResults++;
                return;
            }
            attack.Answered = true;
            attack.Outcome = result.Outcome;
            switch (result.Outcome)
            {
                case DefenseOutcome.Dodged:
                    attack.Share = 0f;
                    break;
                case DefenseOutcome.Blocked:
                    float share = result.DamageMultiplier;
                    attack.Share = float.IsNaN(share) ? 0f : Math.Max(0f, Math.Min(1f, share));
                    break;
            }
            // The answer is in: land it at its time (now, if that has passed)
            SettleDueAttacks();
        }

        // Attacks whose time has come: answered ones as answered, the host's own at once, unanswered ones once the
        // window is over
        private void SettleDueAttacks()
        {
            if (_announced.Count == 0)
                return;
            foreach (var attack in _announced.ToList())
            {
                bool due = attack.AskedOwner && !attack.Answered
                    ? _clock > attack.HitAt + DefenseWindow
                    : _clock >= attack.HitAt;
                if (!due)
                    continue;
                _announced.Remove(attack);
                var outcome = attack.Answered ? attack.Outcome : DefenseOutcome.Hit;
                float damage = 0f;
                if (attack.Share > 0f)
                {
                    _settling = true;
                    try
                    {
                        damage = _world.Strike(attack.AttackerId, attack.TargetId, attack.Context, attack.Share);
                    }
                    finally
                    {
                        _settling = false;
                    }
                }
                _bus.Publish(new DefenseResolvedEvent(attack.Id, attack.AttackerId, attack.TargetId, outcome,
                    attack.Answered, damage));
            }
        }

        /// <summary>
        /// Handles a game message from joined players (<see cref="CoopGameMessages"/>), with the sender's player id.
        /// Messages from someone not playing yet are dropped. Dispose the result to stop.
        /// </summary>
        public IDisposable On<T>(Action<int, T> handler) where T : INetworkMessage
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            return _network.RegisterMessageHandler<T>((connectionId, message) =>
            {
                if (_guests.TryGetValue(connectionId, out var guest) && guest.PlayerId >= 0)
                    handler(guest.PlayerId, message);
            });
        }

        /// <summary>
        /// The scene the game is in, as joined machines are told (empty: they stay in their own).
        /// </summary>
        public string Scene { get; private set; } = "";

        /// <summary>
        /// The game moved to another scene (a hub, a run): every joined machine loads it, and so does each player
        /// who joins later. Only the scene changes: the party, the world and the link stay as they are.
        /// </summary>
        public void ChangeScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("A scene name is required", nameof(sceneName));
            Scene = sceneName;
            SendToAll(new CoopSceneMessage { Scene = sceneName });
        }

        /// <summary>
        /// Sends a game message to the machine of one joined player.
        /// </summary>
        /// <returns>False when nobody who joined plays that player.</returns>
        public bool Send(int playerId, INetworkMessage message, bool reliable = true)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            int connection = ConnectionOf(playerId);
            if (connection < 0 || !_network.IsConnected)
                return false;
            _network.Send(connection, message, reliable);
            return true;
        }

        /// <summary>
        /// Sends a game message to everyone who joined.
        /// </summary>
        public void SendToAll(INetworkMessage message, bool reliable = true)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (!_network.IsConnected)
                return;
            foreach (var pair in _guests)
            {
                if (pair.Value.PlayerId >= 0)
                    _network.Send(pair.Key, message, reliable);
            }
        }

        /// <summary>
        /// Sends the newest state of something that changes all the time (a run's snapshot, a scoreboard) to everyone
        /// who joined, unreliably: a lost packet is not resent and does not hold up newer ones, and machines drop a
        /// packet older than one they already have (<see cref="CoopClient.OnLatest{T}"/>). The message's
        /// <c>Timestamp</c> becomes its place in the order. Send the last state of a series (the run ended) with
        /// <paramref name="final"/> true: it goes reliably, so it always arrives.
        /// </summary>
        public void SendLatestToAll(NetworkMessage message, bool final = false)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            message.Timestamp = ++_stateSequence;
            SendToAll(message, reliable: final);
        }

        /// <summary>
        /// Most players in the party, the host included.
        /// </summary>
        public int MaxPlayers { get; set; } = CoopProtocol.DefaultMaxPlayers;

        /// <summary>
        /// Farthest a joined player may strike from (in world units, with some slack for lag); 0 trusts them.
        /// </summary>
        public float MaxAttackDistance { get; set; }

        /// <summary>
        /// Seconds the seat of a player whose link was lost waits for them; 0 keeps none.
        /// </summary>
        public float RejoinWindow { get; set; } = CoopProtocol.RejoinWindow;

        /// <summary>
        /// How much faster than their character's <see cref="CharacterData.MoveSpeed"/> a joined player may move
        /// before the host puts them back (slack for uneven packets).
        /// </summary>
        public float MoveSpeedTolerance { get; set; } = 1.25f;

        /// <summary>
        /// Distance on the ground a joined player may be off at any moment without a correction.
        /// </summary>
        public float MoveSlack { get; set; } = 0.5f;

        /// <summary>
        /// Seconds after a teleport or a correction during which a joined player's poses that do not fit are
        /// dropped quietly: they were sent before the player got the news.
        /// </summary>
        public float SettleTime { get; set; } = 0.5f;

        /// <summary>
        /// Times a joined player moved too fast and was put back.
        /// </summary>
        public int MovementCorrections { get; private set; }

        /// <summary>
        /// Where players come back after a lost link or a revive (the game's checkpoints); null leaves them where
        /// the game puts new players.
        /// </summary>
        public IRespawnPointProvider RespawnPoints { get; set; }

        /// <summary>
        /// Decides whether a joined player's attack, with the tags and source they claim, is accepted
        /// (player id, target id, context); null accepts every attack in reach. The host's own rules
        /// (hostility, <see cref="MaxAttackDistance"/>) apply either way.
        /// </summary>
        public Func<int, int, AttackContext, bool> AcceptAttack { get; set; }

        /// <summary>
        /// Lets a joined player move this many times faster than their character for a while (a boost, a
        /// trampoline, a dash), so the speed check does not put them back. A new call replaces the last one.
        /// </summary>
        /// <param name="multiplier">At least 1 (slowing a player down needs no permission).</param>
        /// <param name="duration">Seconds on the host's clock.</param>
        /// <returns>False when nobody who joined plays that player.</returns>
        public bool SetAllowedSpeedMultiplier(int playerId, float multiplier, float duration)
        {
            if (float.IsNaN(multiplier) || float.IsNaN(duration))
                throw new ArgumentException("Multiplier and duration must be numbers");
            var guest = GuestOf(playerId);
            if (guest == null)
                return false;
            guest.Boost = Math.Max(1f, multiplier);
            guest.BoostUntil = _clock + Math.Max(0f, duration);
            return true;
        }

        /// <summary>
        /// How many times faster than their character this player may move right now (1 without a boost).
        /// </summary>
        public float AllowedSpeedMultiplier(int playerId)
        {
            var guest = GuestOf(playerId);
            return guest != null && guest.BoostUntil >= _clock ? guest.Boost : 1f;
        }

        private Guest GuestOf(int playerId) =>
            playerId < 0 ? null : _guests.Values.FirstOrDefault(g => g.PlayerId == playerId);

        public bool IsOpen => _network.IsConnected;

        /// <summary>
        /// Seats waiting for players whose link was lost.
        /// </summary>
        public int KeptSeats => _keptSeats.Count;

        /// <summary>
        /// Players in the party, the host included.
        /// </summary>
        public int PlayerCount => 1 + _guests.Values.Count(g => g.PlayerId >= 0);

        /// <summary>
        /// Player ids of those who joined.
        /// </summary>
        public IEnumerable<int> GuestPlayers => _guests.Values.Where(g => g.PlayerId >= 0).Select(g => g.PlayerId);

        /// <summary>
        /// Someone joined (their player id) or left.
        /// </summary>
        public event Action<int> PlayerJoined;

        public event Action<int> PlayerLeft;

        /// <summary>
        /// A player whose link was lost came back within <see cref="RejoinWindow"/> (previous player id, new player id),
        /// raised right before <see cref="PlayerJoined"/>: hand them back what the game kept under the old id (their
        /// runner, their loot, their place in a vote).
        /// </summary>
        public event Action<int, int> PlayerRejoined;

        /// <summary>
        /// Checks the progress a joining player brings (<see cref="ProgressMode.PerPlayer"/>) before it is used: cap
        /// levels, drop items the game does not sell yet, refuse a save that cannot be real. Null takes it as it is
        /// (fine among friends). Either way a player's own profile is taken once per session (by name): coming back
        /// later, they continue with what they left with here, not with what their machine brings.
        /// </summary>
        public IGuestProfileValidator ProfileValidator { get; set; }

        /// <summary>
        /// Nothing has come from this joined player for a while (<see cref="CoopProtocol.UnstableAfter"/> seconds).
        /// </summary>
        public bool IsUnstable(int playerId)
        {
            foreach (var pair in _guests)
            {
                if (pair.Value.PlayerId == playerId)
                    return _network.SecondsSinceHeard(pair.Key) > CoopProtocol.UnstableAfter;
            }
            return false;
        }

        /// <summary>
        /// Starts listening for players on this port.
        /// </summary>
        public void Open(int port)
        {
            ThrowIfWorldNotStarted();
            _network.StartServer(port);
            _logger?.LogInfo($"Co-op open on port {port}", Category);
        }

        /// <summary>
        /// Starts taking players on a transport that already listens (from <see cref="INetworkSessionProvider.HostAsync"/>).
        /// </summary>
        public void Open()
        {
            ThrowIfWorldNotStarted();
            _network.StartServer();
            _logger?.LogInfo("Co-op open", Category);
        }

        private void ThrowIfWorldNotStarted()
        {
            if (!_world.IsStarted)
                throw new InvalidOperationException("Start the world before opening it to others");
        }

        public void Update(float deltaTime)
        {
            _clock += deltaTime;
            _network.Update(deltaTime);
            ForgetOldSeats(deltaTime);
            SettleDueAttacks();
            if (!_network.IsConnected)
                return;
            _sinceSend += deltaTime;
            if (_sinceSend < 1f / CoopProtocol.SendRate)
                return;
            _sinceSend = 0f;
            SendWorld();
            SendMotion();
        }

        /// <summary>
        /// Sends what changed right now instead of at the next tick (tests, or before closing).
        /// </summary>
        public void Flush()
        {
            SendWorld();
            SendMotion();
        }

        public void Dispose()
        {
            if (_world.HoldAttack == HoldAttack)
                _world.HoldAttack = null;
            _announced.Clear();
            _subscriptions.Dispose();
            _network.PeerConnected -= OnPeerConnected;
            _network.PeerDisconnected -= OnPeerDisconnected;
            if (_network.IsConnected)
                _network.Stop();
            foreach (var guest in _guests.Values.Where(g => g.PlayerId >= 0 && _world.IsStarted))
                _world.RemovePlayer(guest.PlayerId);
            _guests.Clear();
            _keptSeats.Clear();
            _profilesTaken.Clear();
            _leftWith.Clear();
        }

        private void ForgetOldSeats(float deltaTime)
        {
            if (_keptSeats.Count == 0)
                return;
            foreach (var token in _keptSeats.Keys.ToList())
            {
                var kept = _keptSeats[token];
                kept.Since += deltaTime;
                if (kept.Since > RejoinWindow)
                {
                    _keptSeats.Remove(token);
                    Remember(kept.Name, kept.Progress);
                    _logger?.LogInfo($"Seat {kept.Seat} of {kept.Name} is free again", Category);
                }
            }
        }

        private void OnPeerConnected(int connectionId) => _guests[connectionId] = new Guest();

        private void OnPeerDisconnected(int connectionId)
        {
            if (!_guests.TryGetValue(connectionId, out var guest))
                return;
            _guests.Remove(connectionId);
            if (guest.PlayerId < 0)
                return;
            var player = _world.Get(guest.PlayerId);
            if (!guest.LeavingOnPurpose && RejoinWindow > 0f && player != null)
            {
                _keptSeats[guest.Token] = KeepSeat(guest, player);
                _logger?.LogInfo($"Lost {player.DisplayName}; their seat waits {RejoinWindow:0} s", Category);
            }
            else if (player != null && _world.ProgressMode == ProgressMode.PerPlayer)
            {
                Remember(player.PlayerName, _world.CaptureProgress(player.EntityId));
            }
            _world.RemovePlayer(guest.PlayerId);
            PlayerLeft?.Invoke(guest.PlayerId);
        }

        private void OnHello(int connectionId, CoopHelloMessage hello)
        {
            if (!_guests.TryGetValue(connectionId, out var guest) || guest.PlayerId >= 0)
                return;

            var kept = TakeKeptSeat(hello.RejoinToken, connectionId);
            string refusal = null;
            PlayerProgress profile = null;
            if (hello.Fingerprint != _fingerprint)
                refusal = "The host plays a different version of the game. Both need the same build.";
            // Seats kept for lost players count as taken for newcomers
            else if (PlayerCount + (kept != null ? 0 : _keptSeats.Count) >= MaxPlayers)
                refusal = $"The party is full ({MaxPlayers} players).";
            else if (kept == null && _world.ProgressMode == ProgressMode.PerPlayer)
                profile = ProfileFor(hello.Name, hello.Profile, out refusal);
            if (refusal != null)
            {
                if (kept != null)
                    _keptSeats[hello.RejoinToken] = kept;
                _network.Send(connectionId, new CoopWelcomeMessage { Accepted = false, Reason = refusal });
                _logger?.LogInfo($"Refused connection {connectionId}: {refusal}", Category);
                return;
            }

            if (kept != null)
            {
                guest.PlayerId = _world.AddPlayer(name: kept.Name, seat: kept.Seat, progress: kept.Progress);
                // Back with the health they had (at least a little): reconnecting is not a way to heal
                _world.Get(guest.PlayerId)?.SetHealth(Math.Max(1f, kept.Health));
            }
            else
            {
                // With progress of their own, they bring it (checked by the validator, once per session)
                guest.PlayerId = _world.AddPlayer(name: hello.Name, seat: FreeSeat(), progress: profile);
            }
            guest.Token = Guid.NewGuid().ToString("N");
            _network.Send(connectionId, new CoopWelcomeMessage
            {
                Accepted = true, PlayerId = guest.PlayerId, RejoinToken = guest.Token, Rejoined = kept != null
            });
            // A joiner plays where the game is
            if (Scene.Length > 0)
                _network.Send(connectionId, new CoopSceneMessage { Scene = Scene });
            if (kept != null)
            {
                PutBack(guest.PlayerId, kept.Seat, RespawnReason.Rejoin, kept.Pose);
                PlayerRejoined?.Invoke(kept.PlayerId, guest.PlayerId);
            }
            PlayerJoined?.Invoke(guest.PlayerId);
        }

        private KeptSeat KeepSeat(Guest guest, Actor player)
        {
            ActorPose? pose = null;
            if (guest.Placed)
                pose = guest.Place;
            else if (_motion != null && _motion.TryGetPose(player.EntityId, out var seen))
                pose = seen;
            return new KeptSeat
            {
                PlayerId = player.EntityId, Seat = player.Seat, Name = player.PlayerName, Health = player.Health, Pose = pose,
                Progress = _world.ProgressMode == ProgressMode.PerPlayer ? _world.CaptureProgress(player.EntityId) : null
            };
        }

        // The game's checkpoint for this player, if it has one
        private void PutBack(int playerId, int seat, RespawnReason reason, ActorPose? lastPose)
        {
            if (RespawnPoints != null && RespawnPoints.TryGetRespawnPoint(playerId, seat, reason, lastPose, out var pose)
                && pose.IsFinite)
                Teleport(playerId, pose);
        }

        /// <summary>
        /// The seat kept for this token. A player may come back before this machine noticed the old link was lost:
        /// that link is let go of now (its late messages are ignored) and its seat goes to the new one.
        /// </summary>
        private KeptSeat TakeKeptSeat(string token, int connectionId)
        {
            if (string.IsNullOrEmpty(token))
                return null;
            if (_keptSeats.TryGetValue(token, out var kept))
            {
                _keptSeats.Remove(token);
                return kept;
            }
            foreach (var pair in _guests)
            {
                var old = pair.Value;
                if (pair.Key == connectionId || old.Token != token || old.PlayerId < 0)
                    continue;
                var player = _world.Get(old.PlayerId);
                _guests.Remove(pair.Key);
                if (player == null)
                    return null;
                kept = KeepSeat(old, player);
                _world.RemovePlayer(old.PlayerId);
                PlayerLeft?.Invoke(old.PlayerId);
                _logger?.LogInfo($"{player.DisplayName} came back on a new link", Category);
                return kept;
            }
            return null;
        }

        // The progress a newcomer starts with: what they left with this session, or what they bring (checked)
        private PlayerProgress ProfileFor(string name, PlayerProgress brought, out string refusal)
        {
            refusal = null;
            string key = (name ?? "").Trim();
            if (key.Length > 0 && _leftWith.TryGetValue(key, out var left))
                return left;
            if (key.Length > 0 && _profilesTaken.Contains(key))
                return null;
            if (brought == null)
                return null;
            var check = ProfileValidator?.Check(key, brought) ?? GuestProfileCheck.Accept(brought);
            if (check.Refused)
            {
                refusal = check.Reason;
                return null;
            }
            if (key.Length > 0)
                _profilesTaken.Add(key);
            return check.Profile;
        }

        private void Remember(string name, PlayerProgress progress)
        {
            string key = (name ?? "").Trim();
            if (key.Length > 0 && progress != null)
                _leftWith[key] = progress;
        }

        private int FreeSeat()
        {
            int seat = 0;
            while (_world.IsSeatTaken(seat) || _keptSeats.Values.Any(k => k.Seat == seat))
                seat++;
            return seat;
        }

        private void OnIntent(int connectionId, CoopIntentMessage intent)
        {
            if (!_guests.TryGetValue(connectionId, out var guest) || guest.PlayerId < 0)
                return;
            switch (intent.Kind)
            {
                case CoopIntentMessage.IntentKind.Attack:
                    var target = _world.Get(intent.TargetId);
                    // A player's claim is a strike: only the host's rules deal effect damage
                    var context = new AttackContext(intent.Tags, intent.SourceId).Between(guest.PlayerId, intent.TargetId);
                    if (target != null && _world.AreHostile(guest.PlayerId, target.EntityId) && InReach(guest.PlayerId, target.EntityId)
                        && (AcceptAttack == null || AcceptAttack(guest.PlayerId, target.EntityId, context)))
                        _world.Attack(guest.PlayerId, target.EntityId, context);
                    break;
                case CoopIntentMessage.IntentKind.Revive:
                    // In PvP the match brings players back, nobody revives on demand
                    var fallen = _world.Get(guest.PlayerId);
                    if (_world.Settings.Pvp == null && fallen != null && _world.RevivePlayer(guest.PlayerId))
                        PutBack(guest.PlayerId, fallen.Seat, RespawnReason.Revive, guest.Placed ? guest.Place : (ActorPose?)null);
                    break;
                case CoopIntentMessage.IntentKind.Leave:
                    guest.LeavingOnPurpose = true;
                    break;
                case CoopIntentMessage.IntentKind.UseAbility:
                    if (_world.Abilities != null && !string.IsNullOrEmpty(intent.AbilityId))
                        _world.Abilities.Use(guest.PlayerId, intent.AbilityId, intent.TargetId);
                    break;
                case CoopIntentMessage.IntentKind.TakeOffer:
                    if (!string.IsNullOrEmpty(intent.OfferId))
                        _world.Trade(guest.PlayerId, intent.OfferId);
                    break;
            }
        }

        private bool InReach(int attackerId, int targetId)
        {
            if (MaxAttackDistance <= 0f || _motion == null)
                return true;
            if (!_motion.TryGetPose(attackerId, out var from) || !_motion.TryGetPose(targetId, out var to))
                return true;
            float dx = from.X - to.X, dz = from.Z - to.Z;
            return dx * dx + dz * dz <= MaxAttackDistance * MaxAttackDistance;
        }

        private void OnMotion(int connectionId, CoopMotionMessage motion)
        {
            if (_motion == null || !_guests.TryGetValue(connectionId, out var guest) || guest.PlayerId < 0)
                return;
            // A player moves only their own character, and no faster than it can
            foreach (var (id, pose) in motion.Poses)
            {
                if (id != guest.PlayerId || !pose.IsFinite)
                    continue;
                if (WithinReach(guest, id, pose, out var allowed))
                {
                    _motion.SetTargetPose(id, pose);
                    continue;
                }
                // Sent before the player got to where they were put: not cheating, just late
                if (_clock <= guest.SettlingUntil)
                    continue;
                MovementCorrections++;
                _logger?.LogWarning($"Player {id} moved faster than their character can; put back", Category);
                Place(guest, allowed);
                guest.SettlingUntil = _clock + SettleTime;
                _motion.SetTargetPose(id, allowed);
                _network.Send(connectionId, new CoopCorrectionMessage { Pose = allowed });
            }
        }

        // The ground distance from the last accepted place must fit the character's speed over the time since;
        // otherwise the player goes as far as they could have, towards where they claim to be
        private bool WithinReach(Guest guest, int playerId, ActorPose pose, out ActorPose allowed)
        {
            allowed = pose;
            float speed = _world.Get(playerId)?.Character.MoveSpeed ?? 0f;
            if (speed <= 0f || !guest.Placed)
            {
                Place(guest, pose);
                return true;
            }

            // A boost that was on at any time since the last accepted place counts for the whole stretch; a status
            // effect that speeds the character up counts too
            float boost = guest.BoostUntil >= guest.PlacedAt ? guest.Boost : 1f;
            boost = Math.Max(boost, _world.Get(playerId)?.MoveMultiplier ?? 1f);
            float reach = speed * boost * MoveSpeedTolerance * (_clock - guest.PlacedAt) + MoveSlack;
            float dx = pose.X - guest.Place.X, dz = pose.Z - guest.Place.Z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            if (distance <= reach)
            {
                Place(guest, pose);
                return true;
            }

            float part = reach / distance;
            allowed = new ActorPose(guest.Place.X + dx * part, pose.Y, guest.Place.Z + dz * part, pose.Yaw);
            return false;
        }

        private void Place(Guest guest, ActorPose pose)
        {
            guest.Placed = true;
            guest.Place = pose;
            guest.PlacedAt = _clock;
        }

        /// <summary>
        /// Puts a player somewhere at once (a spawn point, a new round): here, and on the machine of the player who
        /// plays them, which then moves on from there.
        /// </summary>
        public void Teleport(int playerId, ActorPose pose)
        {
            if (!pose.IsFinite)
                throw new ArgumentException("The pose must be real numbers", nameof(pose));
            _motion?.Teleport(playerId, pose);
            foreach (var pair in _guests)
            {
                if (pair.Value.PlayerId != playerId)
                    continue;
                Place(pair.Value, pose);
                pair.Value.SettlingUntil = _clock + SettleTime;
                if (_network.IsConnected)
                    _network.Send(pair.Key, new CoopCorrectionMessage { Pose = pose });
            }
        }

        private void OnSpawned(ActorSpawnedEvent spawned)
        {
            var actor = _world.Get(spawned.EntityId);
            if (actor == null)
                return;
            _present[actor.EntityId] = actor;
            _happenings.Add(new Happening { Kind = CoopWorldMessage.ChangeKind.Spawn, Actor = actor });
        }

        private void OnDespawned(ActorDespawnedEvent despawned)
        {
            if (!_present.TryGetValue(despawned.EntityId, out var actor))
                return;
            _present.Remove(despawned.EntityId);
            _happenings.Add(new Happening { Kind = CoopWorldMessage.ChangeKind.Despawn, Actor = actor });
        }

        private void OnDamaged(ActorDamagedEvent damaged)
        {
            if (!_present.TryGetValue(damaged.TargetId, out var actor))
                return;
            _happenings.Add(new Happening
            {
                Kind = CoopWorldMessage.ChangeKind.Hit, Actor = actor, AttackerId = damaged.AttackerId,
                Amount = damaged.Amount, HealthLeft = damaged.HealthLeft, Context = damaged.Context
            });
        }

        private void OnEffectApplied(EffectAppliedEvent applied)
        {
            if (!_present.TryGetValue(applied.EntityId, out var actor))
                return;
            var effect = _world.EffectsOn(applied.EntityId).FirstOrDefault(e => e.EffectId == applied.EffectId);
            _happenings.Add(new Happening
            {
                Kind = CoopWorldMessage.ChangeKind.EffectOn, Actor = actor, Name = applied.EffectId,
                Stacks = applied.Stacks, AttackerId = effect.EffectId != null ? effect.SourceId : -1,
                Amount = effect.EffectId != null ? effect.Remaining : -1f
            });
        }

        private void OnEffectRemoved(EffectRemovedEvent removed)
        {
            if (_present.TryGetValue(removed.EntityId, out var actor))
                _happenings.Add(new Happening { Kind = CoopWorldMessage.ChangeKind.EffectOff, Actor = actor, Name = removed.EffectId });
        }

        private void OnCooldownStarted(CooldownStartedEvent started)
        {
            if (_present.TryGetValue(started.EntityId, out var actor))
            {
                _happenings.Add(new Happening
                {
                    Kind = CoopWorldMessage.ChangeKind.Cooldown, Actor = actor, Name = started.AbilityId, Amount = started.Duration
                });
            }
        }

        private void SendWorld()
        {
            if (!_network.IsConnected)
            {
                _happenings.Clear();
                return;
            }

            byte[] party = null;
            foreach (var pair in _guests)
            {
                var guest = pair.Value;
                if (guest.PlayerId < 0)
                    continue;
                var message = new CoopWorldMessage();

                // What happened since the last send, in order
                foreach (var happening in _happenings)
                {
                    var actor = happening.Actor;
                    switch (happening.Kind)
                    {
                        case CoopWorldMessage.ChangeKind.Spawn:
                            Show(guest, message, actor);
                            break;
                        case CoopWorldMessage.ChangeKind.Despawn:
                            if (guest.Known.TryGetValue(actor.EntityId, out var known) && known == actor)
                                Hide(guest, message, actor.EntityId);
                            break;
                        case CoopWorldMessage.ChangeKind.Hit:
                            if (guest.Known.TryGetValue(actor.EntityId, out known) && known == actor)
                            {
                                message.Changes.Add(new CoopWorldMessage.Change
                                {
                                    Kind = CoopWorldMessage.ChangeKind.Hit, Id = actor.EntityId,
                                    AttackerId = happening.AttackerId, Amount = happening.Amount,
                                    HealthLeft = happening.HealthLeft, Tags = happening.Context.Tags,
                                    SourceId = happening.Context.SourceId, FromEffect = happening.Context.FromEffect
                                });
                            }
                            break;
                        case CoopWorldMessage.ChangeKind.EffectOn:
                        case CoopWorldMessage.ChangeKind.EffectOff:
                        case CoopWorldMessage.ChangeKind.Cooldown:
                            if (guest.Known.TryGetValue(actor.EntityId, out known) && known == actor)
                            {
                                message.Changes.Add(new CoopWorldMessage.Change
                                {
                                    Kind = happening.Kind, Id = actor.EntityId, Name = happening.Name,
                                    Stacks = happening.Stacks, AttackerId = happening.AttackerId, Amount = happening.Amount
                                });
                            }
                            break;
                    }
                }

                // Whatever the player has not seen yet (they just joined) or still sees but is gone
                foreach (var actor in _present.Values)
                    Show(guest, message, actor);
                foreach (int gone in guest.Known.Keys.Where(id => !_present.ContainsKey(id)).ToList())
                    Hide(guest, message, gone);

                foreach (var actor in guest.Known.Values)
                {
                    var state = ActorState.Of(actor);
                    if (guest.States.TryGetValue(actor.EntityId, out var seen) && seen.SameAs(state))
                        continue;
                    guest.States[actor.EntityId] = state;
                    message.States.Add(state);
                }

                // Shared progress is the same for everyone; progress of their own is each player's
                var status = _world.ProgressMode == ProgressMode.PerPlayer ? PartyStatus.Of(_world, guest.PlayerId) : null;
                byte[] bytes;
                if (status != null)
                    bytes = status.ToBytes();
                else
                    bytes = party = party ?? PartyStatus.Of(_world).ToBytes();
                if (guest.Party == null || !guest.Party.SequenceEqual(bytes))
                {
                    guest.Party = bytes;
                    message.Party = status ?? PartyStatus.Of(_world);
                }

                if (!message.IsEmpty)
                    _network.Send(pair.Key, message);
            }
            _happenings.Clear();
        }

        private void Show(Guest guest, CoopWorldMessage message, Actor actor)
        {
            if (guest.Known.TryGetValue(actor.EntityId, out var known))
            {
                if (known == actor)
                    return;
                // The id was reused by a new character
                Hide(guest, message, actor.EntityId);
            }
            guest.Known[actor.EntityId] = actor;
            message.Changes.Add(new CoopWorldMessage.Change
            {
                Kind = CoopWorldMessage.ChangeKind.Spawn, Id = actor.EntityId, CharacterId = actor.CharacterId,
                IsPlayer = actor.IsPlayer, Name = actor.PlayerName, Seat = actor.Seat
            });
            // Effects it already has, for someone who sees it for the first time
            foreach (var effect in _world.EffectsOn(actor.EntityId))
            {
                message.Changes.Add(new CoopWorldMessage.Change
                {
                    Kind = CoopWorldMessage.ChangeKind.EffectOn, Id = actor.EntityId, Name = effect.EffectId,
                    Stacks = effect.Stacks, AttackerId = effect.SourceId, Amount = effect.Remaining
                });
            }
        }

        private static void Hide(Guest guest, CoopWorldMessage message, int entityId)
        {
            guest.Known.Remove(entityId);
            guest.States.Remove(entityId);
            message.Changes.Add(new CoopWorldMessage.Change { Kind = CoopWorldMessage.ChangeKind.Despawn, Id = entityId });
        }

        private void SendMotion()
        {
            if (_motion == null || !_network.IsConnected)
                return;
            var message = new CoopMotionMessage();
            foreach (int id in _present.Keys)
            {
                if (_motion.TryGetPose(id, out var pose))
                    message.Poses.Add((id, pose));
            }
            if (message.Poses.Count == 0)
                return;
            foreach (var pair in _guests)
            {
                if (pair.Value.PlayerId >= 0)
                    _network.Send(pair.Key, message, reliable: false);
            }
        }
    }
}
