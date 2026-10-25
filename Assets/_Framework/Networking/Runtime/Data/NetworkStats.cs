namespace RPGFramework.Networking
{
    /// <summary>
    /// Network statistics for monitoring and debugging.
    /// </summary>
    public struct NetworkStats
    {
        public int ActiveConnections;
        public long TotalBytesSent;
        public long TotalBytesReceived;
        public long TotalMessagesSent;
        public long TotalMessagesReceived;
        public float AverageLatency;
        public float PacketLoss;
        public int EntitiesReplicated;
        public float CurrentBandwidthUsage;
        public int DroppedPackets;
    }
}
