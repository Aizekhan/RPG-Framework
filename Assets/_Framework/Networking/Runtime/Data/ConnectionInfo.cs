namespace RPGFramework.Networking
{
    /// <summary>
    /// Information about a network connection.
    /// </summary>
    public struct ConnectionInfo
    {
        public int ConnectionId;
        public string RemoteAddress;
        public int RemotePort;
        public ConnectionState State;
        public long ConnectedTime;
        public float AverageRtt;
        public float PacketLoss;
        public long BytesSent;
        public long BytesReceived;
    }
}
