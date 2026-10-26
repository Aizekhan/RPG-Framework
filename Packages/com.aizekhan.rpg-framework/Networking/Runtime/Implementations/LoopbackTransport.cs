using System;
using System.Collections.Generic;

namespace RPGFramework.Networking
{
    /// <summary>
    /// An in-memory network: transports created from it reach each other by port without sockets, delivering in
    /// order on the next <see cref="INetworkTransport.Poll"/>. For tests and for running a host and clients in one
    /// process. Messages sent unreliably are delivered too (nothing is lost in memory).
    /// </summary>
    public sealed class LoopbackNetwork
    {
        private readonly Dictionary<int, LoopbackTransport> _servers = new Dictionary<int, LoopbackTransport>();

        public LoopbackTransport CreateTransport() => new LoopbackTransport(this);

        internal bool Listen(int port, LoopbackTransport server)
        {
            if (_servers.ContainsKey(port))
                return false;
            _servers[port] = server;
            return true;
        }

        internal void StopListening(int port, LoopbackTransport server)
        {
            if (_servers.TryGetValue(port, out var current) && current == server)
                _servers.Remove(port);
        }

        internal LoopbackTransport ServerAt(int port) => _servers.TryGetValue(port, out var server) ? server : null;
    }

    /// <summary>
    /// One end of a <see cref="LoopbackNetwork"/>. As a client it reaches the server as connection 0; as a server
    /// its clients get connection ids from 1 up.
    /// </summary>
    public sealed class LoopbackTransport : INetworkTransport, INetworkPeerDisconnect
    {
        private const int ServerConnectionId = 0;

        private readonly LoopbackNetwork _network;
        private readonly Queue<NetworkTransportEvent> _incoming = new Queue<NetworkTransportEvent>();
        // Server: client id → client transport. Client: 0 → server transport.
        private readonly Dictionary<int, LoopbackTransport> _peers = new Dictionary<int, LoopbackTransport>();
        private int _port = -1;
        private int _idOnServer = -1;
        private int _nextClientId = 1;
        private bool _isServer;
        private bool _silent;

        internal LoopbackTransport(LoopbackNetwork network) => _network = network;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Pulls the cable (tests): from now on nothing this end sends arrives and nothing reaches it, and nobody is
        /// told; peers find out only by not hearing from it.
        /// </summary>
        public void GoSilent() => _silent = true;

        public void StartServer(int port)
        {
            if (IsActive)
                throw new InvalidOperationException("Already active");
            if (!_network.Listen(port, this))
                throw new InvalidOperationException($"Port {port} is already in use on this loopback network");
            _port = port;
            _isServer = true;
            IsActive = true;
        }

        public void Connect(string address, int port)
        {
            if (IsActive)
                throw new InvalidOperationException("Already active");
            IsActive = true;
            var server = _network.ServerAt(port);
            if (server == null)
            {
                Enqueue(NetworkTransportEventType.Disconnected, ServerConnectionId, null, $"Nothing listens on port {port}");
                return;
            }
            _peers[ServerConnectionId] = server;
            int idOnServer = server.Accept(this);
            Enqueue(NetworkTransportEventType.Connected, ServerConnectionId, null, null);
            _idOnServer = idOnServer;
        }

        private int Accept(LoopbackTransport client)
        {
            int id = _nextClientId++;
            _peers[id] = client;
            Enqueue(NetworkTransportEventType.Connected, id, null, null);
            return id;
        }

        public void Disconnect()
        {
            if (!IsActive)
                return;
            IsActive = false;
            if (_isServer)
            {
                _network.StopListening(_port, this);
                if (!_silent)
                {
                    foreach (var client in _peers.Values)
                        client.Dropped(ServerConnectionId);
                }
            }
            else if (!_silent && _peers.TryGetValue(ServerConnectionId, out var server))
            {
                server.Dropped(_idOnServer);
            }
            _peers.Clear();
            _incoming.Clear();
            _isServer = false;
            _silent = false;
        }

        public void Disconnect(int connectionId)
        {
            if (!IsActive || !_isServer || !_peers.TryGetValue(connectionId, out var client))
                return;
            _peers.Remove(connectionId);
            if (!_silent)
                client.Dropped(ServerConnectionId);
        }

        private void Dropped(int connectionId)
        {
            if (_silent || !_peers.Remove(connectionId))
                return;
            Enqueue(NetworkTransportEventType.Disconnected, connectionId, null, null);
        }

        public void Send(int connectionId, byte[] data, bool reliable)
        {
            if (!IsActive || _silent || !_peers.TryGetValue(connectionId, out var peer))
                return;
            peer.Receive(_isServer ? ServerConnectionId : _idOnServer, data);
        }

        public void Broadcast(byte[] data, bool reliable)
        {
            if (!IsActive || !_isServer || _silent)
                return;
            foreach (var client in _peers.Values)
                client.Receive(ServerConnectionId, data);
        }

        private void Receive(int fromConnectionId, byte[] data)
        {
            if (IsActive && !_silent)
                Enqueue(NetworkTransportEventType.Data, fromConnectionId, (byte[])data.Clone(), null);
        }

        public bool Poll(out NetworkTransportEvent transportEvent)
        {
            if (_incoming.Count == 0)
            {
                transportEvent = default;
                return false;
            }
            transportEvent = _incoming.Dequeue();
            return true;
        }

        private void Enqueue(NetworkTransportEventType type, int connectionId, byte[] data, string error) =>
            _incoming.Enqueue(new NetworkTransportEvent { Type = type, ConnectionId = connectionId, Data = data, Error = error });
    }
}
