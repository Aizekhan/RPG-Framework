using RPGFramework.ECS;

namespace RPGFramework.Networking
{
    /// <summary>
    /// ECS component for network-synchronized transform.
    /// </summary>
    public struct NetworkTransformComponent : IComponent
    {
        /// <summary>
        /// Network-synchronized position.
        /// </summary>
        public Vector3Network Position;

        /// <summary>
        /// Network-synchronized rotation.
        /// </summary>
        public QuaternionNetwork Rotation;

        /// <summary>
        /// Network-synchronized velocity (for prediction).
        /// </summary>
        public Vector3Network Velocity;

        /// <summary>
        /// Position threshold for delta compression.
        /// </summary>
        public float PositionThreshold;

        /// <summary>
        /// Rotation threshold for delta compression.
        /// </summary>
        public float RotationThreshold;

        /// <summary>
        /// Velocity threshold for delta compression.
        /// </summary>
        public float VelocityThreshold;

        /// <summary>
        /// Enable interpolation on clients.
        /// </summary>
        public bool EnableInterpolation;

        /// <summary>
        /// Enable prediction on clients.
        /// </summary>
        public bool EnablePrediction;
    }
}
