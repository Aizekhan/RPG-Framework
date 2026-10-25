using RPGFramework.ECS;

namespace RPGFramework.Networking
{
    /// <summary>
    /// ECS component for network-synchronized entities.
    /// </summary>
    public struct NetworkEntityComponent : IComponent
    {
        /// <summary>
        /// Unique network identifier.
        /// </summary>
        public int NetworkId;

        /// <summary>
        /// Owner connection ID (authoritative peer).
        /// </summary>
        public int OwnerId;

        /// <summary>
        /// Replication mode for this entity.
        /// </summary>
        public ReplicationMode ReplicationMode;

        /// <summary>
        /// Is this entity owned by the local peer?
        /// </summary>
        public bool IsLocalOwner;

        /// <summary>
        /// Prefab identifier for spawning.
        /// </summary>
        public string PrefabId;
    }
}
