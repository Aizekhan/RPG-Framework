using RPGFramework.ECS;

namespace RPGFramework.Networking
{
    /// <summary>
    /// ECS component for per-entity network statistics.
    /// </summary>
    public struct NetworkStatsComponent : IComponent
    {
        /// <summary>
        /// Total bytes sent for this entity.
        /// </summary>
        public long BytesSent;

        /// <summary>
        /// Total bytes received for this entity.
        /// </summary>
        public long BytesReceived;

        /// <summary>
        /// Number of state updates sent.
        /// </summary>
        public int UpdatesSent;

        /// <summary>
        /// Number of state updates received.
        /// </summary>
        public int UpdatesReceived;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public long LastUpdateTime;

        /// <summary>
        /// Average update size in bytes.
        /// </summary>
        public float AverageUpdateSize;
    }
}
