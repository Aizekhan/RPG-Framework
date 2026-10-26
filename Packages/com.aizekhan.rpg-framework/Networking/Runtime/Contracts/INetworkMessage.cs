namespace RPGFramework.Networking
{
    /// <summary>
    /// Base interface for all network messages.
    /// </summary>
    public interface INetworkMessage
    {
        /// <summary>
        /// Unique message type identifier.
        /// </summary>
        int MessageTypeId { get; }

        /// <summary>
        /// Timestamp when this message was created.
        /// </summary>
        long Timestamp { get; }
    }
}
