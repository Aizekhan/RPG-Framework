using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// Health bar above a combatant (always facing the camera) and a short flash of the body when it is hit.
    /// </summary>
    public sealed class HealthBarView : CombatantView
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Rotated to face the camera")]
        [SerializeField] private Transform _bar;

        [Tooltip("Scaled along X by the health fraction; its pivot is the bar's centre")]
        [SerializeField] private Transform _fill;

        [SerializeField] private Renderer[] _body = new Renderer[0];
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField, Min(0f)] private float _flashDuration = 0.12f;

        private MaterialPropertyBlock _block;
        private float _width = 1f;
        private float _lastHealth;
        private float _flash;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_fill != null)
                _width = _fill.localScale.x;
        }

        protected override void OnBound()
        {
            _lastHealth = Combatant?.Health ?? 0f;
            _flash = 0f;
            ApplyFlash(false);
        }

        protected override void OnUnbound() => ApplyFlash(false);

        private void LateUpdate()
        {
            var combatant = Combatant;
            if (combatant == null)
                return;

            float fraction = combatant.MaxHealth > 0f ? Mathf.Clamp01(combatant.Health / combatant.MaxHealth) : 0f;
            if (_fill != null)
            {
                _fill.localScale = new Vector3(_width * fraction, _fill.localScale.y, _fill.localScale.z);
                _fill.localPosition = new Vector3(-_width * (1f - fraction) * 0.5f, _fill.localPosition.y, _fill.localPosition.z);
            }

            var view = Camera.main;
            if (_bar != null && view != null)
                _bar.rotation = view.transform.rotation;

            if (combatant.Health < _lastHealth)
            {
                _flash = _flashDuration;
                ApplyFlash(true);
            }
            _lastHealth = combatant.Health;

            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                if (_flash <= 0f)
                    ApplyFlash(false);
            }
        }

        private void ApplyFlash(bool on)
        {
            if (_block == null)
                return;
            foreach (var body in _body)
            {
                if (body == null)
                    continue;
                if (on)
                {
                    body.GetPropertyBlock(_block);
                    _block.SetColor(ColorId, _flashColor);
                    body.SetPropertyBlock(_block);
                }
                else
                {
                    body.SetPropertyBlock(null);
                }
            }
        }
    }
}
