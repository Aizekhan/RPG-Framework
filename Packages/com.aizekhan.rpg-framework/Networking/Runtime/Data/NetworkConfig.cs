namespace RPGFramework.Networking
{
    /// <summary>
    /// Configuration for network manager.
    /// </summary>
    public struct NetworkConfig
    {
        /// <summary>
        /// Maximum number of connections (server only).
        /// </summary>
        public int MaxConnections;

        /// <summary>
        /// Seconds without anything from a peer before it is dropped as gone (<see cref="PeerDropReason.TimedOut"/>);
        /// 0 waits for the transport to notice. Keep it well above <see cref="PingInterval"/>, which keeps quiet peers heard.
        /// </summary>
        public float ConnectionTimeout;

        /// <summary>
        /// Unreadable or not-allowed messages a peer may send before it is dropped
        /// (<see cref="PeerDropReason.BadMessages"/>); 0 never drops for that.
        /// </summary>
        public int MaxBadMessages;

        /// <summary>
        /// Replication update rate in Hz (updates per second).
        /// </summary>
        public int ReplicationRate;

        /// <summary>
        /// Enable delta compression for replication.
        /// </summary>
        public bool UseDeltaCompression;

        /// <summary>
        /// Maximum bandwidth in bytes per second (0 = unlimited).
        /// </summary>
        public int MaxBandwidth;

        /// <summary>
        /// Enable lag compensation.
        /// </summary>
        public bool EnableLagCompensation;

        /// <summary>
        /// Enable client-side prediction.
        /// </summary>
        public bool EnablePrediction;

        /// <summary>
        /// Snapshot interpolation delay in seconds.
        /// </summary>
        public float InterpolationDelay;

        /// <summary>
        /// Ping interval in seconds.
        /// </summary>
        public float PingInterval;

        /// <summary>
        /// Default configuration.
        /// </summary>
        public static NetworkConfig Default => new NetworkConfig
        {
            MaxConnections = 32,
            ConnectionTimeout = 10f,
            ReplicationRate = 20,
            UseDeltaCompression = true,
            MaxBandwidth = 0,
            EnableLagCompensation = true,
            EnablePrediction = true,
            InterpolationDelay = 0.1f,
            PingInterval = 1f,
            MaxBadMessages = 20
        };
    }
}
