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

        /// <summary>
        /// Messages refused by <see cref="NetworkAuthority"/> because they came from the wrong side.
        /// </summary>
        public int RejectedMessages;

        /// <summary>
        /// Exceptions thrown by message handlers (each is logged; other handlers still run).
        /// </summary>
        public int HandlerErrors;
    }
}
