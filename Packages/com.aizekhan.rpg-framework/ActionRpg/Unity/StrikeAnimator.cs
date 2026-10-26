using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A quick forward lunge of the model when its owner strikes; stands in for an attack animation.
    /// With an <see cref="Animator"/> on the prefab, set a trigger name instead to play a real attack clip.
    /// </summary>
    public sealed class StrikeAnimator : MonoBehaviour
    {
        [Tooltip("The visible model; moved forward and back on a strike. Defaults to the first child")]
        [SerializeField] private Transform _model;

        [Tooltip("If set and the prefab has an Animator, this trigger is fired instead of the lunge")]
        [SerializeField] private string _animatorTrigger = "";

        [SerializeField, Min(0f)] private float _distance = 0.45f;
        [SerializeField, Min(0.01f)] private float _duration = 0.18f;

        private Animator _animator;
        private Vector3 _rest;
        private float _time = -1f;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_model == null && transform.childCount > 0)
                _model = transform.GetChild(0);
            if (_model != null)
                _rest = _model.localPosition;
        }

        private void OnDisable()
        {
            _time = -1f;
            if (_model != null)
                _model.localPosition = _rest;
        }

        public void Play()
        {
            if (_animator != null && !string.IsNullOrEmpty(_animatorTrigger))
                _animator.SetTrigger(_animatorTrigger);
            else
                _time = 0f;
        }

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
