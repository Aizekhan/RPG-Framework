using RPGFramework.Serialization;

namespace RPGFramework.DeltaCompression
{
    /// <summary>
    /// Interface for delta compression serialization.
    /// Compresses state updates by sending only changed data.
    /// </summary>
    public interface IDeltaSerializer
    {
        /// <summary>
        /// Creates a delta packet containing only changes since last state.
        /// Returns full state on first call or if delta is too large.
        /// </summary>
        byte[] CreateDelta<T>(int entityId, T currentState) where T : ISerializable;

        /// <summary>
        /// Applies a delta packet to reconstruct the full state.
        /// </summary>
        T ApplyDelta<T>(int entityId, byte[] delta) where T : ISerializable, new();

        /// <summary>
        /// Clears cached state for an entity.
        /// </summary>
        void ClearState(int entityId);

        /// <summary>
        /// Clears all cached states.
        /// </summary>
        void ClearAllStates();
    }
}
