using UnityEngine;
using System.Collections;

namespace RPGFramework.Cameras
{
    /// <summary>
    /// Basic camera follow implementation with smooth damping.
    /// Game layer can extend for additional features (zoom, rotation limits, etc.).
    /// </summary>
    public class CameraFollow : MonoBehaviour, ICameraController
    {
        [Header("Follow Settings")]
        [SerializeField] protected Transform target;
        [SerializeField] protected Vector3 offset = new Vector3(0, 5, -10);
        [SerializeField] protected float followSpeed = 5f;
        [SerializeField] protected bool isFollowing = true;

        [Header("Smoothing")]
        [SerializeField] protected bool useSmoothDamping = true;
        [SerializeField] protected float smoothTime = 0.3f;

        [Header("Boundaries")]
        [SerializeField] protected bool useBoundaries = false;
        [SerializeField] protected Vector2 minBounds = new Vector2(-50, -50);
        [SerializeField] protected Vector2 maxBounds = new Vector2(50, 50);

        [Header("Shake")]
        [SerializeField] protected float shakeDecay = 5f;

        protected Vector3 velocity = Vector3.zero;
        protected Vector3 shakeOffset = Vector3.zero;
        protected float shakeIntensity = 0f;
        protected Coroutine shakeCoroutine;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        public float FollowSpeed
        {
            get => followSpeed;
            set => followSpeed = Mathf.Max(0.1f, value);
        }

        public bool IsFollowing
        {
            get => isFollowing;
            set => isFollowing = value;
        }

        protected virtual void LateUpdate()
        {
            UpdateCamera();
        }

        public virtual void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public virtual void UpdateCamera()
        {
            if (!isFollowing || target == null)
                return;

            Vector3 desiredPosition = target.position + offset;

            // Apply boundaries if enabled
            if (useBoundaries)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.z = Mathf.Clamp(desiredPosition.z, minBounds.y, maxBounds.y);
            }

            // Apply smooth damping or lerp
            Vector3 smoothedPosition;
            if (useSmoothDamping)
            {
                smoothedPosition = Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref velocity,
                    smoothTime
                );
            }
            else
            {
                smoothedPosition = Vector3.Lerp(
                    transform.position,
                    desiredPosition,
                    followSpeed * Time.deltaTime
                );
            }

            // Apply shake offset
            transform.position = smoothedPosition + shakeOffset;

            // Decay shake
            if (shakeIntensity > 0)
            {
                shakeIntensity = Mathf.Max(0, shakeIntensity - shakeDecay * Time.deltaTime);
                shakeOffset = Random.insideUnitSphere * shakeIntensity;
            }
            else
            {
                shakeOffset = Vector3.zero;
            }
        }

        public virtual void Shake(float intensity, float duration)
        {
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));
        }

        protected virtual IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            shakeIntensity = intensity;
            yield return new WaitForSeconds(duration);
            shakeIntensity = 0f;
            shakeOffset = Vector3.zero;
        }

        /// <summary>
        /// Set camera boundaries for restricting movement.
        /// </summary>
        public virtual void SetBoundaries(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;
            useBoundaries = true;
        }

        /// <summary>
        /// Disable camera boundaries.
        /// </summary>
        public virtual void DisableBoundaries()
        {
            useBoundaries = false;
        }

        /// <summary>
        /// Immediately snap camera to target position without smoothing.
        /// </summary>
        public virtual void SnapToTarget()
        {
            if (target == null)
                return;

            Vector3 desiredPosition = target.position + offset;

            if (useBoundaries)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.z = Mathf.Clamp(desiredPosition.z, minBounds.y, maxBounds.y);
            }

            transform.position = desiredPosition;
            velocity = Vector3.zero;
        }
    }
}
