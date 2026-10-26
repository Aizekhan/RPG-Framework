namespace RPGFramework.Networking
{
    /// <summary>
    /// A transport that can drop one connection and keep the rest (a server letting go of a silent player).
    /// <see cref="NetworkManager"/> uses it when it drops a peer itself; without it, only that peer's events stop.
    /// </summary>
    public interface INetworkPeerDisconnect
    {
        /// <summary>
        /// Closes this connection; no Disconnected event follows for it.
        /// </summary>
        void Disconnect(int connectionId);
    }
}
