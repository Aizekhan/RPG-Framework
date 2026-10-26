using RPGFramework.ActionRpg.Coop;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Moves a character that another machine drives (in co-op: the other players, and on a joined machine the
    /// enemies too) smoothly to where that machine last said it is. The presenter enables it instead of the
    /// <see cref="PlayerController"/> or <see cref="EnemyBrain"/> for such characters.
    /// </summary>
    public sealed class RemoteMotion : ActorView
    {
        [Tooltip("How quickly the character catches up with its reported position (higher = snappier)")]
        [SerializeField, Min(0.1f)] private float _sharpness = 14f;

        [Tooltip("Farther than this from the reported position, the character jumps there instead of sliding")]
        [SerializeField, Min(0f)] private float _snapDistance = 6f;

        private Vector3 _target;
        private Quaternion _facing;
        private bool _hasTarget;

        /// <summary>
        /// Where it goes next. The first position after it appears is taken at once.
        /// </summary>
        public void SetTarget(ActorPose pose)
        {
            _target = new Vector3(pose.X, pose.Y, pose.Z);
            _facing = Quaternion.Euler(0f, pose.Yaw, 0f);
            if (_hasTarget && (_target - transform.position).sqrMagnitude <= _snapDistance * _snapDistance)
                return;
            _hasTarget = true;
            transform.SetPositionAndRotation(_target, _facing);
        }

        protected override void OnBound() => _hasTarget = false;

        private void Update()
        {
            if (!_hasTarget)
                return;
            float blend = 1f - Mathf.Exp(-_sharpness * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, _target, blend),
                Quaternion.Slerp(transform.rotation, _facing, blend));
        }
    }
}
