namespace RPGFramework.Networking
{
    /// <summary>
    /// Defines how an entity is replicated across the network.
    /// </summary>
    public enum ReplicationMode
    {
        /// <summary>
        /// No replication - local only.
        /// </summary>
        None,

        /// <summary>
        /// Full state replication every update.
        /// </summary>
        FullState,

        /// <summary>
        /// Delta compression - only send changes.
        /// </summary>
        Delta,

        /// <summary>
        /// Only replicate to owner.
        /// </summary>
        OwnerOnly,

        /// <summary>
        /// Replicate to all except owner.
        /// </summary>
        ExceptOwner,

        /// <summary>
        /// Interest management - only replicate to relevant clients.
        /// </summary>
        InterestManaged
    }
}
