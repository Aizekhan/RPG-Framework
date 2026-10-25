using System;
using System.Collections.Generic;

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
        private readonly Dictionary<int, Action<int, INetworkMessage>> _messageHandlers;
        private readonly List<INetworkConnection> _connectionsReadOnly;
        private NetworkStats _stats;
        private float _timeSinceLastPing;

        public NetworkRole Role { get; private set; }
        public bool IsConnected { get; private set; }
        public int LocalConnectionId { get; private set; }
        public IReadOnlyList<INetworkConnection> Connections => _connectionsReadOnly;
        public NetworkConfig Config { get; private set; }

        public NetworkManager(INetworkTransport transport, INetworkSerializer serializer, NetworkConfig config)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Config = config;

            _connections = new Dictionary<int, NetworkConnection>();
            _messageHandlers = new Dictionary<int, Action<int, INetworkMessage>>();
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
            if (!IsConnected || Role != NetworkRole.Client)
                return;

            byte[] data = _serializer.Serialize(message);
            _transport.Send(LocalConnectionId, data, reliable);
            _stats.TotalMessagesSent++;
            _stats.TotalBytesSent += data.Length;
        }

        public void Broadcast(INetworkMessage message, bool reliable = true)
        {
            if (!IsConnected || Role != NetworkRole.Server)
                return;

            byte[] data = _serializer.Serialize(message);
            _transport.Broadcast(data, reliable);
            _stats.TotalMessagesSent += _connections.Count;
            _stats.TotalBytesSent += data.Length * _connections.Count;
        }

        public void RegisterMessageHandler<T>(Action<int, T> handler) where T : INetworkMessage
        {
            var message = Activator.CreateInstance<T>();
            int messageTypeId = message.MessageTypeId;

            _messageHandlers[messageTypeId] = (connectionId, msg) => handler(connectionId, (T)msg);
        }

        public void UnregisterMessageHandler<T>() where T : INetworkMessage
        {
            var message = Activator.CreateInstance<T>();
            int messageTypeId = message.MessageTypeId;

            _messageHandlers.Remove(messageTypeId);
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
        }

        private void HandleDisconnected(int connectionId, string reason)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.SetState(ConnectionState.Disconnected);
                _connections.Remove(connectionId);
                _connectionsReadOnly.Remove(connection);
            }

            if (Role == NetworkRole.Client && connectionId == LocalConnectionId)
            {
                IsConnected = false;
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

                // Invoke registered handlers
                if (_messageHandlers.TryGetValue(messageTypeId, out var handler))
                {
                    handler(connectionId, (INetworkMessage)message);
                }
            }
            catch (Exception ex)
            {
                // Log error (in production, use proper logging)
                Console.WriteLine($"Error deserializing message: {ex.Message}");
            }
        }

        private void HandleError(int connectionId, string error)
        {
            // Log error
            Console.WriteLine($"Network error on connection {connectionId}: {error}");
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

        private int EstimateMessageSize(INetworkMessage message)
        {
            // Simple estimation - in production, use actual serialized size
            return 64;
        }
    }
}
