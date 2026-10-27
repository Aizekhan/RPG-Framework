using RPGFramework.Events;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network connection is established.
    /// Published by <see cref="NetworkManager"/> (when given an event publisher) as each connection opens, alongside <see cref="INetworkManager.PeerConnected"/>.
    /// </summary>
    public struct ConnectionEstablishedEvent : IEvent
    {
        public int ConnectionId;
        public string RemoteAddress;
        public bool IsServer;
    }
}
