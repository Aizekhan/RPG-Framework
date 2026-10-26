using UnityEngine;
using UnityEngine.UI;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Health bar above a character (always facing the camera) and a short flash of the body when it is hit.
    /// Without a bar of its own the prefab gets a small world-space one, placed above its renderers.
    /// </summary>
    public sealed class HealthBarView : ActorView
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Rotated to face the camera; leave empty to build a default bar")]
        [SerializeField] private Transform _bar;

        [Tooltip("Shrinks with health: a RectTransform by its right anchor, any other Transform by its X scale")]
        [SerializeField] private Transform _fill;

        [Tooltip("Flash these when hit; leave empty for every renderer of the prefab")]
        [SerializeField] private Renderer[] _body = new Renderer[0];

        [SerializeField] private Color _playerColor = new Color(0.3f, 0.85f, 0.35f);
        [SerializeField] private Color _enemyColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField, Min(0f)] private float _flashDuration = 0.12f;

        private MaterialPropertyBlock _block;
        private float _width = 1f;
        private float _lastHealth;
        private float _flash;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_body == null || _body.Length == 0)
                _body = GetComponentsInChildren<MeshRenderer>(true);
            if (_bar == null)
                BuildDefaultBar();
            if (_fill != null)
                _width = _fill.localScale.x;
        }

        protected override void OnBound()
        {
            var actor = Actor;
            _lastHealth = actor?.Health ?? 0f;
            _flash = 0f;
            ApplyFlash(false);
            if (_fill == null || actor == null)
                return;
            var color = actor.IsPlayer ? _playerColor : _enemyColor;
            if (_fill.TryGetComponent<Image>(out var image))
            {
                image.color = color;
            }
            else if (_fill.TryGetComponent<Renderer>(out var fillRenderer))
            {
                // A mesh bar (e.g. a cube) is tinted without touching its shared material
                var block = new MaterialPropertyBlock();
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                fillRenderer.SetPropertyBlock(block);
            }
        }

        protected override void OnUnbound() => ApplyFlash(false);

        private void LateUpdate()
        {
            var actor = Actor;
            if (actor == null)
                return;

            float fraction = actor.HealthFraction;
            if (_fill is RectTransform rect)
            {
                rect.anchorMax = new Vector2(fraction, 1f);
            }
            else if (_fill != null)
            {
                _fill.localScale = new Vector3(_width * fraction, _fill.localScale.y, _fill.localScale.z);
                _fill.localPosition = new Vector3(-_width * (1f - fraction) * 0.5f, _fill.localPosition.y, _fill.localPosition.z);
            }

            var view = Camera.main;
            if (_bar != null && view != null)
                _bar.rotation = view.transform.rotation;

            if (actor.Health < _lastHealth)
            {
                _flash = _flashDuration;
                ApplyFlash(true);
            }
            _lastHealth = actor.Health;

            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                if (_flash <= 0f)
                    ApplyFlash(false);
            }
        }

        private void BuildDefaultBar()
        {
            float top = 2f;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                top = bounds.max.y - transform.position.y;
            }

            var canvasObject = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.SetParent(transform, false);
            canvasRect.localPosition = new Vector3(0f, top + 0.4f, 0f);
            canvasRect.sizeDelta = new Vector2(100f, 12f);
            canvasRect.localScale = Vector3.one * 0.012f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var background = Panel(canvasRect, "Background", new Color(0f, 0f, 0f, 0.75f));
            _fill = Panel(background, "Fill", _enemyColor);
            _bar = canvasRect;
        }

        private static RectTransform Panel(RectTransform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
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
                    _block.SetColor(BaseColorId, _flashColor);
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
