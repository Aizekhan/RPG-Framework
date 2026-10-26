using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// A quick forward lunge of the model when its owner strikes; stands in for an attack animation.
    /// </summary>
    public sealed class StrikeAnimator : MonoBehaviour
    {
        [Tooltip("The visible model; moved forward and back on a strike")]
        [SerializeField] private Transform _model;

        [SerializeField, Min(0f)] private float _distance = 0.45f;
        [SerializeField, Min(0.01f)] private float _duration = 0.18f;

        private Vector3 _rest;
        private float _time = -1f;

        private void Awake()
        {
            if (_model != null)
                _rest = _model.localPosition;
        }

        private void OnDisable()
        {
            _time = -1f;
            if (_model != null)
                _model.localPosition = _rest;
        }

        public void Play() => _time = 0f;

        private void LateUpdate()
        {
            if (_model == null || _time < 0f)
                return;

            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / _duration);
            // 0 → 1 → 0 over the duration
            _model.localPosition = _rest + Vector3.forward * (_distance * Mathf.Sin(t * Mathf.PI));
            if (t >= 1f)
                _time = -1f;
        }
    }
}
