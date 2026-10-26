using UnityEngine;

namespace RPGFramework.Pooling.Unity
{
    /// <summary>
    /// Implement on components of pooled prefabs to reset state when the object is spawned/despawned.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Added to every instance created by a <see cref="GameObjectPool"/>; lets gameplay code return an
    /// object without knowing which pool it came from.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        public GameObjectPool Pool { get; internal set; }

        public bool IsSpawned => Pool != null && Pool.IsActive(gameObject);

        /// <summary>
        /// Returns the object to its pool; destroys it if the pool is gone.
        /// </summary>
        public void ReturnToPool()
        {
            if (Pool != null && !Pool.IsDisposed)
                Pool.Release(gameObject);
            else
                UnityObjects.Destroy(gameObject);
        }
    }
}
