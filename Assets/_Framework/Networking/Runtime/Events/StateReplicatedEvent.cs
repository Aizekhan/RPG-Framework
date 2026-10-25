namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when an entity's state is replicated.
    /// </summary>
    public struct StateReplicatedEvent
    {
        public int NetworkId;
        public bool IsDelta;
        public int DataSize;
    }
}
