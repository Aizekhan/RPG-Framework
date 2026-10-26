using System;
using System.Collections.Generic;
using RPGFramework.Logging;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Implementation of INetworkManager.
    /// </summary>
    public class NetworkManager : INetworkManager
    {
        private readonly INetworkTransport _transport;
        private readonly INetworkSerializer _serializer;
        private readonly Dictionary<int, NetworkConnection> _connections;
        private const string LogCategory = "Network";

        private readonly Dictionary<int, List<HandlerEntry>> _messageHandlers;
        private readonly ILogger _logger;
        private readonly List<INetworkConnection> _connectionsReadOnly;
        private NetworkStats _stats;
        private float _timeSinceLastPing;
        // Per connection: seconds since it was last heard, and how many bad messages it sent
        private readonly Dictionary<int, float> _silence = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _badMessages = new Dictionary<int, int>();
        private readonly List<int> _toDrop = new List<int>();
        // Server: connections it let go of, whose late messages are not read
        private readonly HashSet<int> _dropped = new HashSet<int>();

        public NetworkRole Role { get; private set; }
        public bool IsConnected { get; private set; }
        public int LocalConnectionId { get; private set; }
        public IReadOnlyList<INetworkConnection> Connections => _connectionsReadOnly;
        public NetworkConfig Config { get; private set; }

        /// <summary>
        /// Optional per-message authority rules; null disables the checks.
        /// </summary>
        public NetworkAuthority Authority { get; set; }

        public event Action<int> PeerConnected;
        public event Action<int> PeerDisconnected;
        public event Action<int, PeerDropReason> PeerDropped;

        public NetworkManager(INetworkTransport transport, INetworkSerializer serializer, NetworkConfig config, ILogger logger = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Config = config;

            _connections = new Dictionary<int, NetworkConnection>();
            _messageHandlers = new Dictionary<int, List<HandlerEntry>>();
            _logger = logger;
            _connectionsReadOnly = new List<INetworkConnection>();
            Role = NetworkRole.None;
            LocalConnectionId = -1;
        }

        public void StartServer(int port)
        {
            if (IsConnected)
                throw new InvalidOperationException("Already connected");

            _transport.StartServer(port);
            Role = NetworkRole.Server;
            IsConnected = true;
            LocalConnectionId = 0; // Server is always connection 0
        }

        public void StartClient(string address, int port)
        {
            if (IsConnected)
                throw new InvalidOperationException("Already connected");

            _transport.Connect(address, port);
            Role = NetworkRole.Client;
        }

        /// <summary>
        /// Starts as server on a transport that already listens (opened by an <see cref="INetworkSessionProvider"/>).
        /// </summary>
        public void StartServer()
        {
            if (IsConnected)
                throw new InvalidOperationException("Already connected");
            if (!_transport.IsActive)
                throw new InvalidOperationException("The transport is not listening; open it with a session provider first");
            Role = NetworkRole.Server;
            IsConnected = true;
            LocalConnectionId = 0;
        }

        /// <summary>
        /// Starts as client on a transport that already connects (from <see cref="INetworkSessionProvider.JoinAsync"/>);
        /// <see cref="PeerConnected"/> or <see cref="PeerDisconnected"/> tells how it went.
        /// </summary>
        public void StartClient()
        {
            if (IsConnected)
                throw new InvalidOperationException("Already connected");
            if (!_transport.IsActive)
                throw new InvalidOperationException("The transport is not connecting; join with a session provider first");
            Role = NetworkRole.Client;
        }

        public void Stop()
        {
            _transport.Disconnect();
            IsConnected = false;
            Role = NetworkRole.None;
            _connections.Clear();
            _connectionsReadOnly.Clear();
            _silence.Clear();
            _badMessages.Clear();
            _dropped.Clear();
            LocalConnectionId = -1;
        }

        public float SecondsSinceHeard(int connectionId) => _silence.TryGetValue(connectionId, out float seconds) ? seconds : 0f;

        public void Send(int connectionId, INetworkMessage message, bool reliable = true)
        {
            EnsureCanSend(message);
            if (!IsConnected || Role != NetworkRole.Server)
                return;

            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.Send(message, reliable);
                _stats.TotalMessagesSent++;
                _stats.TotalBytesSent += EstimateMessageSize(message);
            }
        }

        public void SendToServer(INetworkMessage message, bool reliable = true)
        {
            EnsureCanSend(message);
            if (!IsConnected || Role != NetworkRole.Client)
                return;

            byte[] data = _serializer.Serialize(message);
            _transport.Send(LocalConnectionId, data, reliable);
            _stats.TotalMessagesSent++;
            _stats.TotalBytesSent += data.Length;
        }

        public void Broadcast(INetworkMessage message, bool reliable = true)
        {
            EnsureCanSend(message);
            if (!IsConnected || Role != NetworkRole.Server)
                return;

            byte[] data = _serializer.Serialize(message);
            _transport.Broadcast(data, reliable);
            _stats.TotalMessagesSent += _connections.Count;
            _stats.TotalBytesSent += data.Length * _connections.Count;
        }

        public IDisposable RegisterMessageHandler<T>(Action<int, T> handler) where T : INetworkMessage
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            int messageTypeId = TypeIdOf<T>();
            if (!_messageHandlers.TryGetValue(messageTypeId, out var handlers))
            {
                handlers = new List<HandlerEntry>();
                _messageHandlers.Add(messageTypeId, handlers);
            }

            var entry = new HandlerEntry(handler, (connectionId, msg) => handler(connectionId, (T)msg));
            handlers.Add(entry);
            return new HandlerRegistration(this, messageTypeId, entry);
        }

        public void UnregisterMessageHandler<T>() where T : INetworkMessage
        {
            _messageHandlers.Remove(TypeIdOf<T>());
        }

        public bool UnregisterMessageHandler<T>(Action<int, T> handler) where T : INetworkMessage
        {
            int messageTypeId = TypeIdOf<T>();
            if (handler == null || !_messageHandlers.TryGetValue(messageTypeId, out var handlers))
                return false;

            int index = handlers.FindIndex(h => h.Original.Equals(handler));
            if (index < 0)
                return false;
            RemoveHandler(messageTypeId, handlers[index]);
            return true;
        }

        public void Update(float deltaTime)
        {
            if (!_transport.IsActive)
                return;

            // Poll transport for events
            while (_transport.IsActive && _transport.Poll(out var transportEvent))
            {
                HandleTransportEvent(transportEvent);
            }
            if (!_transport.IsActive)
                return;

            // Send periodic pings
            if (Config.PingInterval > 0)
            {
                _timeSinceLastPing += deltaTime;
                if (_timeSinceLastPing >= Config.PingInterval)
                {
                    SendPings();
                    _timeSinceLastPing = 0;
                }
            }

            // Update connection timeouts
            UpdateConnectionTimeouts(deltaTime);
        }

        public INetworkConnection GetConnection(int connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        public NetworkStats GetStats()
        {
            _stats.ActiveConnections = _connections.Count;
            return _stats;
        }

        private void HandleTransportEvent(NetworkTransportEvent transportEvent)
        {
            switch (transportEvent.Type)
            {
                case NetworkTransportEventType.Connected:
                    HandleConnected(transportEvent.ConnectionId, "");
                    break;

                case NetworkTransportEventType.Disconnected:
                    HandleDisconnected(transportEvent.ConnectionId, "Disconnected");
                    break;

                case NetworkTransportEventType.Data:
                    HandleData(transportEvent.ConnectionId, transportEvent.Data);
                    break;

                case NetworkTransportEventType.Error:
                    HandleError(transportEvent.ConnectionId, transportEvent.Error);
                    break;
            }
        }

        private void HandleConnected(int connectionId, string remoteAddress)
        {
            if (Role == NetworkRole.Client)
            {
                LocalConnectionId = connectionId;
                IsConnected = true;
                _silence[connectionId] = 0f;
            }
            else if (Role == NetworkRole.Server)
            {
                var connection = new NetworkConnection(connectionId, remoteAddress, _transport, _serializer);
                _connections[connectionId] = connection;
                _connectionsReadOnly.Add(connection);
                _silence[connectionId] = 0f;
                _dropped.Remove(connectionId);
            }
            else
            {
                return;
            }
            PeerConnected?.Invoke(connectionId);
        }

        private void HandleDisconnected(int connectionId, string reason)
        {
            _silence.Remove(connectionId);
            _badMessages.Remove(connectionId);
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.SetState(ConnectionState.Disconnected);
                _connections.Remove(connectionId);
                _connectionsReadOnly.Remove(connection);
                PeerDisconnected?.Invoke(connectionId);
            }
            else if (Role == NetworkRole.Client && (connectionId == LocalConnectionId || !IsConnected))
            {
                // Lost the server, or never reached it
                IsConnected = false;
                PeerDisconnected?.Invoke(connectionId);
            }
        }

        private void HandleData(int connectionId, byte[] data)
        {
            // Still queued from a peer that was dropped
            if (_dropped.Contains(connectionId))
                return;
            _stats.TotalBytesReceived += data.Length;
            _stats.TotalMessagesReceived++;
            if (_silence.ContainsKey(connectionId))
                _silence[connectionId] = 0f;

            try
            {
                var (messageTypeId, message) = _serializer.Deserialize(data);

                // Handle ping/pong internally
                if (messageTypeId == MessageType.Ping)
                {
                    HandlePing(connectionId, (PingMessage)message);
                    return;
                }
                else if (messageTypeId == MessageType.Pong)
                {
                    HandlePong(connectionId, (PongMessage)message);
                    return;
                }

                if (Authority != null && !Authority.CanReceive(messageTypeId, Role))
                {
                    _stats.RejectedMessages++;
                    _logger?.LogWarning($"Rejected message type {messageTypeId} from connection {connectionId}: not allowed from that side", LogCategory);
                    CountBadMessage(connectionId);
                    return;
                }

                Dispatch(connectionId, messageTypeId, (INetworkMessage)message);
            }
            catch (Exception ex)
            {
                _stats.DroppedPackets++;
                _logger?.LogError($"Could not deserialize message from connection {connectionId}", LogCategory, ex);
                CountBadMessage(connectionId);
            }
        }

        private void Dispatch(int connectionId, int messageTypeId, INetworkMessage message)
        {
            if (!_messageHandlers.TryGetValue(messageTypeId, out var handlers) || handlers.Count == 0)
                return;

            // Snapshot: handlers may (un)register while handling
            foreach (var entry in handlers.ToArray())
            {
                try
                {
                    entry.Invoke(connectionId, message);
                }
                catch (Exception ex)
                {
                    _stats.HandlerErrors++;
                    _logger?.LogError($"Handler for message type {messageTypeId} failed", LogCategory, ex);
                }
            }
        }

        private void HandleError(int connectionId, string error)
        {
            _logger?.LogError($"Network error on connection {connectionId}: {error}", LogCategory);
        }

        private void HandlePing(int connectionId, PingMessage ping)
        {
            if (Role == NetworkRole.Server)
            {
                // Send pong back to client
                var pong = new PongMessage
                {
                    ClientTimestamp = ping.ClientTimestamp,
                    ServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                Send(connectionId, pong, true);
            }
        }

        private void HandlePong(int connectionId, PongMessage pong)
        {
            if (Role == NetworkRole.Client)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                float rtt = (now - pong.ClientTimestamp) / 1000f; // Convert to seconds

                _stats.AverageLatency = rtt;
            }
            else if (Role == NetworkRole.Server && _connections.TryGetValue(connectionId, out var connection))
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                float rtt = (now - pong.ClientTimestamp) / 1000f;
                connection.UpdateRtt(rtt);
            }
        }

        private void SendPings()
        {
            var ping = new PingMessage
            {
                ClientTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            if (Role == NetworkRole.Client)
            {
                SendToServer(ping, true);
            }
            else if (Role == NetworkRole.Server)
            {
                Broadcast(ping, true);
            }
        }

        private void CountBadMessage(int connectionId)
        {
            if (Config.MaxBadMessages <= 0 || !_silence.ContainsKey(connectionId))
                return;
            _badMessages.TryGetValue(connectionId, out int count);
            _badMessages[connectionId] = ++count;
            if (count > Config.MaxBadMessages)
            {
                _logger?.LogWarning($"Dropping connection {connectionId}: {count} messages that could not be read or were not its to send", LogCategory);
                Drop(connectionId, PeerDropReason.BadMessages);
            }
        }

        private void UpdateConnectionTimeouts(float deltaTime)
        {
            if (_silence.Count == 0)
                return;
            _toDrop.Clear();
            foreach (int id in new List<int>(_silence.Keys))
            {
                float seconds = _silence[id] + deltaTime;
                _silence[id] = seconds;
                if (Config.ConnectionTimeout > 0f && seconds > Config.ConnectionTimeout)
                    _toDrop.Add(id);
            }
            foreach (int id in _toDrop)
            {
                _logger?.LogWarning($"Connection {id} timed out: nothing heard for {Config.ConnectionTimeout:0.#} s", LogCategory);
                Drop(id, PeerDropReason.TimedOut);
            }
            _toDrop.Clear();
        }

        /// <summary>
        /// Lets go of a peer the manager gave up on: a server closes that one connection, a client the whole link.
        /// </summary>
        private void Drop(int connectionId, PeerDropReason reason)
        {
            if (!_silence.ContainsKey(connectionId))
                return;
            if (Role == NetworkRole.Server)
            {
                (_transport as INetworkPeerDisconnect)?.Disconnect(connectionId);
                _dropped.Add(connectionId);
                PeerDropped?.Invoke(connectionId, reason);
                HandleDisconnected(connectionId, reason.ToString());
            }
            else
            {
                _silence.Remove(connectionId);
                _badMessages.Remove(connectionId);
                IsConnected = false;
                _transport.Disconnect();
                PeerDropped?.Invoke(connectionId, reason);
                PeerDisconnected?.Invoke(connectionId);
            }
        }

        private void EnsureCanSend(INetworkMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (Authority != null && !Authority.CanSend(message.MessageTypeId, Role))
                throw new InvalidOperationException(
                    $"{message.GetType().Name} has {Authority.Get(message.MessageTypeId)} authority and cannot be sent as {Role}");
        }

        private static int TypeIdOf<T>() where T : INetworkMessage => Activator.CreateInstance<T>().MessageTypeId;

        private void RemoveHandler(int messageTypeId, HandlerEntry entry)
        {
            if (_messageHandlers.TryGetValue(messageTypeId, out var handlers) && handlers.Remove(entry) && handlers.Count == 0)
                _messageHandlers.Remove(messageTypeId);
        }

        private sealed class HandlerEntry
        {
            public HandlerEntry(Delegate original, Action<int, INetworkMessage> invoke)
            {
                Original = original;
                Invoke = invoke;
            }

            public Delegate Original { get; }
            public Action<int, INetworkMessage> Invoke { get; }
        }

        private sealed class HandlerRegistration : IDisposable
        {
            private NetworkManager _owner;
            private readonly int _messageTypeId;
            private readonly HandlerEntry _entry;

            public HandlerRegistration(NetworkManager owner, int messageTypeId, HandlerEntry entry)
            {
                _owner = owner;
                _messageTypeId = messageTypeId;
                _entry = entry;
            }

            public void Dispose()
            {
                _owner?.RemoveHandler(_messageTypeId, _entry);
                _owner = null;
            }
        }

        private int EstimateMessageSize(INetworkMessage message)
        {
            // Simple estimation - in production, use actual serialized size
            return 64;
        }
    }
}
