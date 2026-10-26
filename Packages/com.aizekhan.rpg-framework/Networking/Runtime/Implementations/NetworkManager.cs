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

        public void Stop()
        {
            _transport.Disconnect();
            IsConnected = false;
            Role = NetworkRole.None;
            _connections.Clear();
            _connectionsReadOnly.Clear();
            LocalConnectionId = -1;
        }

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
            while (_transport.Poll(out var transportEvent))
            {
                HandleTransportEvent(transportEvent);
            }

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
            }
            else if (Role == NetworkRole.Server)
            {
                var connection = new NetworkConnection(connectionId, remoteAddress, _transport, _serializer);
                _connections[connectionId] = connection;
                _connectionsReadOnly.Add(connection);
            }
            else
            {
                return;
            }
            PeerConnected?.Invoke(connectionId);
        }

        private void HandleDisconnected(int connectionId, string reason)
        {
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
            _stats.TotalBytesReceived += data.Length;
            _stats.TotalMessagesReceived++;

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
                    return;
                }

                Dispatch(connectionId, messageTypeId, (INetworkMessage)message);
            }
            catch (Exception ex)
            {
                _stats.DroppedPackets++;
                _logger?.LogError($"Could not deserialize message from connection {connectionId}", LogCategory, ex);
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

        private void UpdateConnectionTimeouts(float deltaTime)
        {
            // TODO: Implement connection timeout tracking
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
