namespace RPGFramework.Pooling
{
    /// <summary>
    /// Statistics tracking for object pools.
    /// Provides runtime metrics for pool usage, performance analysis, and leak detection.
    /// </summary>
    public struct PoolStatistics
    {
        /// <summary>
        /// Pool identifier.
        /// </summary>
        public string PoolKey;

        /// <summary>
        /// Type name of pooled objects.
        /// </summary>
        public string PoolType;

        /// <summary>
        /// Initial pool size (pre-warmed objects).
        /// </summary>
        public int InitialSize;

        /// <summary>
        /// Current number of active (in-use) objects.
        /// </summary>
        public int ActiveCount;

        /// <summary>
        /// Current number of inactive (available) objects.
        /// </summary>
        public int InactiveCount;

        /// <summary>
        /// Total number of objects created by this pool.
        /// </summary>
        public int TotalCreated;

        /// <summary>
        /// Number of objects created on-demand during Get() calls (excludes pre-warming).
        /// </summary>
        public int CreatedOnDemand;

        /// <summary>
        /// Total number of Get() calls.
        /// </summary>
        public long TotalGetCount;

        /// <summary>
        /// Total number of Release() calls.
        /// </summary>
        public long TotalReturnCount;

        /// <summary>
        /// Peak active object count (high-water mark).
        /// </summary>
        public int PeakActiveCount;

        /// <summary>
        /// Average time objects remain active (in seconds).
        /// </summary>
        public float AverageActiveTime;

        /// <summary>
        /// Total pool size (active + inactive).
        /// </summary>
        public readonly int TotalSize => ActiveCount + InactiveCount;

        /// <summary>
        /// Pool hit rate (reused vs created on demand).
        /// 1.0 = perfect reuse (no objects created during Get), 0.0 = no reuse (all Gets created new objects).
        /// </summary>
        public readonly float HitRate => TotalGetCount > 0
            ? (TotalGetCount - CreatedOnDemand) / (float)TotalGetCount
            : 0f;

        public override readonly string ToString()
        {
            return $"Pool: {PoolKey} ({PoolType}), Active: {ActiveCount}, Inactive: {InactiveCount}, " +
                   $"Total: {TotalSize}, Gets: {TotalGetCount}, Returns: {TotalReturnCount}, " +
                   $"Peak: {PeakActiveCount}, HitRate: {HitRate:P1}";
        }
    }
}
