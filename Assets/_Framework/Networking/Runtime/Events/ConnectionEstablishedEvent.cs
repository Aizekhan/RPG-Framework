namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network connection is established.
    /// </summary>
    public struct ConnectionEstablishedEvent
    {
        public int ConnectionId;
        public string RemoteAddress;
        public bool IsServer;
    }
}
