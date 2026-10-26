using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.Networking;

namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// Opens this machine's game to friends: everyone who joins gets a player in the party, sees the world as the
    /// host has it and sends what they want to do; the host's rules decide. Each joined player gets, in order,
    /// characters entering and leaving, hits, changed numbers and the party's progress — measured against what that
    /// player last saw, so someone joining late gets the whole world at once. Call <see cref="Update"/> every frame.
    /// </summary>
    public sealed class CoopHost : IDisposable
    {
        private const string Category = "Coop";

        private sealed class Guest
        {
            public int PlayerId = -1;
            public readonly Dictionary<int, Actor> Known = new Dictionary<int, Actor>();
            public readonly Dictionary<int, ActorState> States = new Dictionary<int, ActorState>();
            public byte[] Party;
        }

        private struct Happening
        {
            public CoopWorldMessage.ChangeKind Kind;
            public Actor Actor;
            public int AttackerId;
            public float Amount;
            public float HealthLeft;
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
        private float _sinceSend;

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
            _motion = motion;
            _fingerprint = fingerprint ?? "";
            _logger = logger;

            foreach (var actor in world.Actors)
                _present[actor.EntityId] = actor;
            bus.Subscribe<ActorSpawnedEvent>(OnSpawned).AddTo(_subscriptions);
            bus.Subscribe<ActorDespawnedEvent>(OnDespawned).AddTo(_subscriptions);
            bus.Subscribe<ActorDamagedEvent>(OnDamaged).AddTo(_subscriptions);

            _network.PeerConnected += OnPeerConnected;
            _network.PeerDisconnected += OnPeerDisconnected;
            _network.RegisterMessageHandler<CoopHelloMessage>(OnHello).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopIntentMessage>(OnIntent).AddTo(_subscriptions);
            _network.RegisterMessageHandler<CoopMotionMessage>(OnMotion).AddTo(_subscriptions);
        }

        /// <summary>
        /// Most players in the party, the host included.
        /// </summary>
        public int MaxPlayers { get; set; } = CoopProtocol.DefaultMaxPlayers;

        /// <summary>
        /// Farthest a joined player may strike from (in world units, with some slack for lag); 0 trusts them.
        /// </summary>
        public float MaxAttackDistance { get; set; }

        public bool IsOpen => _network.IsConnected;

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
            _network.Update(deltaTime);
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
            _subscriptions.Dispose();
            _network.PeerConnected -= OnPeerConnected;
            _network.PeerDisconnected -= OnPeerDisconnected;
            if (_network.IsConnected)
                _network.Stop();
            foreach (var guest in _guests.Values.Where(g => g.PlayerId >= 0 && _world.IsStarted))
                _world.RemovePlayer(guest.PlayerId);
            _guests.Clear();
        }

        private void OnPeerConnected(int connectionId) => _guests[connectionId] = new Guest();

        private void OnPeerDisconnected(int connectionId)
        {
            if (!_guests.TryGetValue(connectionId, out var guest))
                return;
            _guests.Remove(connectionId);
            if (guest.PlayerId < 0)
                return;
            _world.RemovePlayer(guest.PlayerId);
            PlayerLeft?.Invoke(guest.PlayerId);
        }

        private void OnHello(int connectionId, CoopHelloMessage hello)
        {
            if (!_guests.TryGetValue(connectionId, out var guest) || guest.PlayerId >= 0)
                return;

            string refusal = null;
            if (hello.Fingerprint != _fingerprint)
                refusal = "The host plays a different version of the game. Both need the same build.";
            else if (PlayerCount >= MaxPlayers)
                refusal = $"The party is full ({MaxPlayers} players).";
            if (refusal != null)
            {
                _network.Send(connectionId, new CoopWelcomeMessage { Accepted = false, Reason = refusal });
                _logger?.LogInfo($"Refused connection {connectionId}: {refusal}", Category);
                return;
            }

            guest.PlayerId = _world.AddPlayer(name: hello.Name);
            _network.Send(connectionId, new CoopWelcomeMessage { Accepted = true, PlayerId = guest.PlayerId });
            PlayerJoined?.Invoke(guest.PlayerId);
        }

        private void OnIntent(int connectionId, CoopIntentMessage intent)
        {
            if (!_guests.TryGetValue(connectionId, out var guest) || guest.PlayerId < 0)
                return;
            switch (intent.Kind)
            {
                case CoopIntentMessage.IntentKind.Attack:
                    var target = _world.Get(intent.TargetId);
                    if (target != null && !target.IsPlayer && InReach(guest.PlayerId, target.EntityId))
                        _world.Attack(guest.PlayerId, target.EntityId);
                    break;
                case CoopIntentMessage.IntentKind.Revive:
                    _world.RevivePlayer(guest.PlayerId);
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
            // A player moves only their own character
            foreach (var (id, pose) in motion.Poses)
            {
                if (id == guest.PlayerId)
                    _motion.SetTargetPose(id, pose);
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
                Amount = damaged.Amount, HealthLeft = damaged.HealthLeft
            });
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
                                    HealthLeft = happening.HealthLeft
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

                party = party ?? PartyStatus.Of(_world).ToBytes();
                if (guest.Party == null || !guest.Party.SequenceEqual(party))
                {
                    guest.Party = party;
                    message.Party = PartyStatus.Of(_world);
                }

                if (!message.IsEmpty)
                    _network.Send(pair.Key, message);
            }
            _happenings.Clear();
        }

        private static void Show(Guest guest, CoopWorldMessage message, Actor actor)
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
