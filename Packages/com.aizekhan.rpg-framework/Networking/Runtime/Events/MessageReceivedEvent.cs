using RPGFramework.Events;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network message is received.
    /// Published by <see cref="NetworkManager"/> (when given an event publisher) for every message it accepts, after its handlers ran (pings are internal and not published).
    /// </summary>
    public struct MessageReceivedEvent : IEvent
    {
        public int ConnectionId;
        public int MessageTypeId;
        public INetworkMessage Message;
    }
}
