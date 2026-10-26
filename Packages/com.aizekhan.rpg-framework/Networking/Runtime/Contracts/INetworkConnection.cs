using System;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Represents a network connection to a remote peer.
    /// </summary>
    public interface INetworkConnection
    {
        /// <summary>
        /// Unique connection identifier.
        /// </summary>
        int ConnectionId { get; }

        /// <summary>
        /// Current connection state.
        /// </summary>
        ConnectionState State { get; }

        /// <summary>
        /// Remote address of this connection.
        /// </summary>
        string RemoteAddress { get; }

        /// <summary>
        /// Average round-trip time in milliseconds.
        /// </summary>
        float AverageRtt { get; }

        /// <summary>
        /// Packet loss percentage (0-100).
        /// </summary>
        float PacketLoss { get; }

        /// <summary>
        /// Send a message through this connection.
        /// </summary>
        void Send(INetworkMessage message, bool reliable = true);

        /// <summary>
        /// Close this connection.
        /// </summary>
        void Close();
    }
}
