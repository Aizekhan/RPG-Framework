using System;
using System.Collections.Generic;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Who may originate a message type.
    /// </summary>
    public enum MessageAuthority
    {
        /// <summary>
        /// Either side may send it (chat, pings). Default for unregistered types.
        /// </summary>
        Any,

        /// <summary>
        /// Only the server sends it (spawns, authoritative state); a server drops it when a client sends it.
        /// </summary>
        Server,

        /// <summary>
        /// Only clients send it (input, requests); a client drops it when it arrives from the server.
        /// </summary>
        Client
    }

    /// <summary>
    /// Per-message-type authority rules. Assign to <see cref="NetworkManager.Authority"/> to reject messages from the
    /// wrong side on receive (e.g. a modified client sending spawn messages) and to catch sends from the wrong side.
    /// </summary>
    public sealed class NetworkAuthority
    {
        private readonly Dictionary<int, MessageAuthority> _rules = new Dictionary<int, MessageAuthority>();

        /// <summary>
        /// Rules for the framework's own replication messages: spawns, despawns and entity state come from the server.
        /// </summary>
        public static NetworkAuthority CreateDefault() => new NetworkAuthority()
            .Set(MessageType.EntitySpawn, MessageAuthority.Server)
            .Set(MessageType.EntityDespawn, MessageAuthority.Server)
            .Set(MessageType.EntityStateUpdate, MessageAuthority.Server)
            .Set(MessageType.EntityDeltaUpdate, MessageAuthority.Server);

        public NetworkAuthority Set(int messageTypeId, MessageAuthority authority)
        {
            if (authority == MessageAuthority.Any)
                _rules.Remove(messageTypeId);
            else
                _rules[messageTypeId] = authority;
            return this;
        }

        public NetworkAuthority Set<T>(MessageAuthority authority) where T : INetworkMessage, new() =>
            Set(new T().MessageTypeId, authority);

        public MessageAuthority Get(int messageTypeId) =>
            _rules.TryGetValue(messageTypeId, out var authority) ? authority : MessageAuthority.Any;

        public bool CanSend(int messageTypeId, NetworkRole localRole)
        {
            switch (Get(messageTypeId))
            {
                case MessageAuthority.Server:
                    return localRole == NetworkRole.Server || localRole == NetworkRole.Host;
                case MessageAuthority.Client:
                    return localRole == NetworkRole.Client || localRole == NetworkRole.Host;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Whether a message received from the remote side is acceptable: a server (or host) only hears clients and a
        /// client only hears the server.
        /// </summary>
        public bool CanReceive(int messageTypeId, NetworkRole localRole)
        {
            switch (Get(messageTypeId))
            {
                case MessageAuthority.Server:
                    return localRole == NetworkRole.Client;
                case MessageAuthority.Client:
                    return localRole == NetworkRole.Server || localRole == NetworkRole.Host;
                default:
                    return true;
            }
        }
    }
}
