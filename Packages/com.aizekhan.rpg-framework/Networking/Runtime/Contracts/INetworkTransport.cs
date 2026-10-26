using System;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Abstraction for network transport layer (TCP, UDP, WebSocket, etc.).
    /// Framework users can implement this to integrate with Unity Netcode, Mirror, Photon, etc.
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>
        /// Is the transport currently active?
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Start listening for incoming connections (server mode).
        /// </summary>
        void StartServer(int port);

        /// <summary>
        /// Connect to a remote server (client mode).
        /// </summary>
        void Connect(string address, int port);

        /// <summary>
        /// Disconnect from server or stop server.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Send data to a specific connection.
        /// </summary>
        void Send(int connectionId, byte[] data, bool reliable);

        /// <summary>
        /// Broadcast data to all connections.
        /// </summary>
        void Broadcast(byte[] data, bool reliable);

        /// <summary>
        /// Poll for incoming messages. Returns true if messages are available.
        /// </summary>
        bool Poll(out NetworkTransportEvent transportEvent);
    }

    /// <summary>
    /// Transport event data from polling.
    /// </summary>
    public struct NetworkTransportEvent
    {
        public NetworkTransportEventType Type;
        public int ConnectionId;
        public byte[] Data;
        public string Error;
    }

    /// <summary>
    /// Type of transport event.
    /// </summary>
    public enum NetworkTransportEventType
    {
        None,
        Connected,
        Disconnected,
        Data,
        Error
    }
}
