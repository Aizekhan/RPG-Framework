using RPGFramework.Events;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network connection is lost.
    /// Published by <see cref="NetworkManager"/> (when given an event publisher) as a connection closes, alongside <see cref="INetworkManager.PeerDisconnected"/>; after a drop, <see cref="WasTimeout"/> says whether the peer went silent.
    /// </summary>
    public struct ConnectionLostEvent : IEvent
    {
        public int ConnectionId;
        public string Reason;
        public bool WasTimeout;
    }
}
