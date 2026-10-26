using System;
using System.Collections.Generic;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Main interface for network management.
    /// Handles connections, message routing, and network state.
    /// </summary>
    public interface INetworkManager
    {
        /// <summary>
        /// Current network role.
        /// </summary>
        NetworkRole Role { get; }

        /// <summary>
        /// Is currently connected (client) or listening (server)?
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Local connection ID (for clients).
        /// </summary>
        int LocalConnectionId { get; }

        /// <summary>
        /// All active connections (for servers).
        /// </summary>
        IReadOnlyList<INetworkConnection> Connections { get; }

        /// <summary>
        /// Network configuration.
        /// </summary>
        NetworkConfig Config { get; }

        /// <summary>
        /// Start as server.
        /// </summary>
        void StartServer(int port);

        /// <summary>
        /// Start as client and connect to server.
        /// </summary>
        void StartClient(string address, int port);

        /// <summary>
        /// Stop network operations.
        /// </summary>
        void Stop();

        /// <summary>
        /// Send a message to a specific connection (server only).
        /// </summary>
        void Send(int connectionId, INetworkMessage message, bool reliable = true);

        /// <summary>
        /// Send a message to server (client only).
        /// </summary>
        void SendToServer(INetworkMessage message, bool reliable = true);

        /// <summary>
        /// Broadcast a message to all connected clients (server only).
        /// </summary>
        void Broadcast(INetworkMessage message, bool reliable = true);

        /// <summary>
        /// Adds a handler for messages of type T. Several handlers may listen to one type; each runs in
        /// registration order and an exception in one does not stop the others. Dispose the result to remove it.
        /// </summary>
        IDisposable RegisterMessageHandler<T>(Action<int, T> handler) where T : INetworkMessage;

        /// <summary>
        /// Removes every handler for messages of type T.
        /// </summary>
        void UnregisterMessageHandler<T>() where T : INetworkMessage;

        /// <summary>
        /// Removes one handler; returns false if it was not registered.
        /// </summary>
        bool UnregisterMessageHandler<T>(Action<int, T> handler) where T : INetworkMessage;

        /// <summary>
        /// Process incoming messages. Call this regularly (e.g., every frame).
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Get connection by ID.
        /// </summary>
        INetworkConnection GetConnection(int connectionId);

        /// <summary>
        /// Get network statistics.
        /// </summary>
        NetworkStats GetStats();
    }
}
