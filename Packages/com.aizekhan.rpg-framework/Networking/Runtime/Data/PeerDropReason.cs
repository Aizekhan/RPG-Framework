namespace RPGFramework.Networking
{
    /// <summary>
    /// Why <see cref="INetworkManager"/> dropped a peer itself.
    /// </summary>
    public enum PeerDropReason
    {
        /// <summary>
        /// Nothing came from it for <see cref="NetworkConfig.ConnectionTimeout"/> seconds (a crash, a pulled cable).
        /// </summary>
        TimedOut,

        /// <summary>
        /// It sent more than <see cref="NetworkConfig.MaxBadMessages"/> messages that could not be read or were not
        /// its to send.
        /// </summary>
        BadMessages
    }
}
