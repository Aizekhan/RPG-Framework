using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UtpConnection = Unity.Networking.Transport.NetworkConnection;

namespace RPGFramework.Networking.UnityTransport
{
    /// <summary>
    /// <see cref="INetworkTransport"/> over Unity Transport (UDP): reliable sends go through a reliable, ordered,
    /// fragmenting pipeline; unreliable ones through a fragmenting pipeline only. A client sees the server as
    /// connection 0; a server numbers its clients from 1. Call <see cref="Dispose"/> when done (native memory).
    /// Addresses are IPv4 or IPv6 literals ("192.168.1.20"), not host names.
    /// </summary>
    public sealed class UnityNetworkTransport : INetworkTransport, IDisposable
    {
        private const int ServerConnectionId = 0;
        private const int MaxMessageSize = 64 * 1024;
        private const int ConnectTimeoutMs = 1000;
        private const int ConnectAttempts = 5;

        private readonly Queue<NetworkTransportEvent> _events = new Queue<NetworkTransportEvent>();
        private readonly Dictionary<int, UtpConnection> _connections = new Dictionary<int, UtpConnection>();
        private readonly Dictionary<UtpConnection, int> _ids = new Dictionary<UtpConnection, int>();
        private NetworkDriver _driver;
        private NetworkPipeline _reliable;
        private NetworkPipeline _unreliable;
        private bool _isServer;
        private int _nextClientId = 1;

        public bool IsActive => _driver.IsCreated;

        /// <summary>
        /// The port the server listens on (useful with port 0, which picks a free one).
        /// </summary>
        public int ListeningPort => _driver.IsCreated && _isServer ? _driver.GetLocalEndpoint().Port : 0;

        public void StartServer(int port)
        {
            CreateDriver();
            _isServer = true;
            var endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort)port);
            if (_driver.Bind(endpoint) != 0 || _driver.Listen() != 0)
            {
                Dispose();
                throw new InvalidOperationException($"Could not listen on port {port}; is another game using it?");
            }
        }

        public void Connect(string address, int port)
        {
            if (!NetworkEndpoint.TryParse(address, (ushort)port, out var endpoint)
                && !NetworkEndpoint.TryParse(address, (ushort)port, out endpoint, NetworkFamily.Ipv6))
                throw new ArgumentException($"'{address}' is not an IP address", nameof(address));

            CreateDriver();
            _isServer = false;
            var connection = _driver.Connect(endpoint);
            _connections[ServerConnectionId] = connection;
            _ids[connection] = ServerConnectionId;
        }

        public void Disconnect()
        {
            if (!_driver.IsCreated)
                return;
            foreach (var connection in _connections.Values)
            {
                if (connection.IsCreated)
                    _driver.Disconnect(connection);
            }
            // Let the disconnect messages leave before the driver goes
            _driver.ScheduleUpdate().Complete();
            Dispose();
        }

        public void Send(int connectionId, byte[] data, bool reliable)
        {
            if (_driver.IsCreated && _connections.TryGetValue(connectionId, out var connection))
                Write(connection, data, reliable);
        }

        public void Broadcast(byte[] data, bool reliable)
        {
            if (!_driver.IsCreated || !_isServer)
                return;
            foreach (var connection in _connections.Values)
                Write(connection, data, reliable);
        }

        public bool Poll(out NetworkTransportEvent transportEvent)
        {
            if (_events.Count == 0 && _driver.IsCreated)
                Pump();
            if (_events.Count == 0)
            {
                transportEvent = default;
                return false;
            }
            transportEvent = _events.Dequeue();
            return true;
        }

        public void Dispose()
        {
            if (_driver.IsCreated)
                _driver.Dispose();
            _connections.Clear();
            _ids.Clear();
        }

        private void CreateDriver()
        {
            if (_driver.IsCreated)
                throw new InvalidOperationException("Already active");
            var settings = new NetworkSettings();
            settings.WithFragmentationStageParameters(payloadCapacity: MaxMessageSize);
            settings.WithReliableStageParameters(windowSize: 64);
            // Give up on an address nobody answers at after about 5 seconds, not a minute
            settings.WithNetworkConfigParameters(connectTimeoutMS: ConnectTimeoutMs, maxConnectAttempts: ConnectAttempts);
            _driver = NetworkDriver.Create(settings);
            _reliable = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            _unreliable = _driver.CreatePipeline(typeof(FragmentationPipelineStage));
            _nextClientId = 1;
            _events.Clear();
        }

        private void Pump()
        {
            _driver.ScheduleUpdate().Complete();

            if (_isServer)
            {
                UtpConnection accepted;
                while ((accepted = _driver.Accept()) != default)
                {
                    int id = _nextClientId++;
                    _connections[id] = accepted;
                    _ids[accepted] = id;
                    _events.Enqueue(new NetworkTransportEvent { Type = NetworkTransportEventType.Connected, ConnectionId = id });
                }
            }

            NetworkEvent.Type type;
            while ((type = _driver.PopEvent(out var connection, out var reader, out _)) != NetworkEvent.Type.Empty)
            {
                if (!_ids.TryGetValue(connection, out int id))
                    continue;
                switch (type)
                {
                    case NetworkEvent.Type.Connect:
                        _events.Enqueue(new NetworkTransportEvent { Type = NetworkTransportEventType.Connected, ConnectionId = id });
                        break;
                    case NetworkEvent.Type.Data:
                        var bytes = new NativeArray<byte>(reader.Length, Allocator.Temp);
                        reader.ReadBytes(bytes);
                        _events.Enqueue(new NetworkTransportEvent
                        {
                            Type = NetworkTransportEventType.Data, ConnectionId = id, Data = bytes.ToArray()
                        });
                        bytes.Dispose();
                        break;
                    case NetworkEvent.Type.Disconnect:
                        _connections.Remove(id);
                        _ids.Remove(connection);
                        _events.Enqueue(new NetworkTransportEvent { Type = NetworkTransportEventType.Disconnected, ConnectionId = id });
                        break;
                }
            }
        }

        private void Write(UtpConnection connection, byte[] data, bool reliable)
        {
            if (data.Length > MaxMessageSize)
                throw new ArgumentException($"Message of {data.Length} bytes is over the {MaxMessageSize} byte limit");
            int status = _driver.BeginSend(reliable ? _reliable : _unreliable, connection, out var writer);
            if (status != 0)
            {
                _events.Enqueue(new NetworkTransportEvent
                {
                    Type = NetworkTransportEventType.Error, ConnectionId = _ids.TryGetValue(connection, out int id) ? id : -1,
                    Error = $"Send failed ({(Unity.Networking.Transport.Error.StatusCode)status})"
                });
                return;
            }
            var bytes = new NativeArray<byte>(data, Allocator.Temp);
            writer.WriteBytes(bytes);
            bytes.Dispose();
            _driver.EndSend(writer);
        }
    }
}
