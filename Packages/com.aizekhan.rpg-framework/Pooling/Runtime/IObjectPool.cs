namespace RPGFramework.Pooling
{
    /// <summary>
    /// Base interface for object pools.
    /// Provides read-only statistics and lifecycle management.
    /// </summary>
    public interface IObjectPool
    {
        /// <summary>
        /// Number of objects currently in use.
        /// </summary>
        int CountActive { get; }

        /// <summary>
        /// Number of objects available in the pool.
        /// </summary>
        int CountInactive { get; }

        /// <summary>
        /// Clears the pool, releasing all pooled objects.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Generic typed object pool interface.
    /// Provides Get/Release operations for strongly-typed pooling.
    /// </summary>
    /// <typeparam name="T">Type of objects to pool (must be class)</typeparam>
    public interface IObjectPool<T> : IObjectPool where T : class
    {
        /// <summary>
        /// Gets an object from the pool.
        /// Creates a new instance if pool is empty.
        /// </summary>
        T Get();

        /// <summary>
        /// Returns an object to the pool for reuse.
        /// </summary>
        /// <param name="instance">Object to return</param>
        void Release(T instance);
    }
}
