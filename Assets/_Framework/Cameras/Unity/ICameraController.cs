using UnityEngine;

namespace RPGFramework.Cameras
{
    /// <summary>
    /// Interface for camera controllers.
    /// Framework provides base implementations, game layer can extend for specific behaviors.
    /// </summary>
    public interface ICameraController
    {
        /// <summary>
        /// Current camera target.
        /// </summary>
        Transform Target { get; set; }

        /// <summary>
        /// Camera offset from target.
        /// </summary>
        Vector3 Offset { get; set; }

        /// <summary>
        /// Camera follow speed.
        /// </summary>
        float FollowSpeed { get; set; }

        /// <summary>
        /// Whether camera should follow target.
        /// </summary>
        bool IsFollowing { get; set; }

        /// <summary>
        /// Set camera target.
        /// </summary>
        void SetTarget(Transform target);

        /// <summary>
        /// Update camera position (called in LateUpdate).
        /// </summary>
        void UpdateCamera();

        /// <summary>
        /// Shake camera for impact/damage effects.
        /// </summary>
        void Shake(float intensity, float duration);
    }
}
