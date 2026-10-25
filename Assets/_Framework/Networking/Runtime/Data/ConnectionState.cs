namespace RPGFramework.Networking
{
    /// <summary>
    /// State of a network connection.
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        TimedOut,
        Error
    }
}
