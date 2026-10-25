namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network message is received.
    /// </summary>
    public struct MessageReceivedEvent
    {
        public int ConnectionId;
        public int MessageTypeId;
        public INetworkMessage Message;
    }
}
