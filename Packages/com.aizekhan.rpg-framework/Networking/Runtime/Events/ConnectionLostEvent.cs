namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network connection is lost.
    /// </summary>
    public struct ConnectionLostEvent
    {
        public int ConnectionId;
        public string Reason;
        public bool WasTimeout;
    }
}
