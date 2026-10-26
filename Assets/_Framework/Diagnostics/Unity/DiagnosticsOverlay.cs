using UnityEngine;

namespace RPGFramework.Diagnostics.Unity
{
    /// <summary>
    /// IMGUI overlay that shows a <see cref="DiagnosticsHub"/> report, refreshed a few times per second on unscaled
    /// time. Assign <see cref="Hub"/> from your bootstrap; toggle with <see cref="Visible"/> (or the toggle key when
    /// the legacy input manager is enabled).
    /// </summary>
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        [SerializeField] private bool _visible = true;
        [SerializeField] private float _refreshInterval = 0.25f;
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private Vector2 _size = new Vector2(520f, 420f);
#if ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;
#endif

        private float _nextRefresh;
        private Vector2 _scroll;
        private GUIStyle _style;

        public DiagnosticsHub Hub { get; set; }

        public bool Visible
        {
            get => _visible;
            set => _visible = value;
        }

        /// <summary>
        /// The report currently displayed.
        /// </summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>
        /// Re-renders the report now.
        /// </summary>
        public void Refresh()
        {
            Text = Hub != null ? Hub.Render() : "No DiagnosticsHub assigned";
            _nextRefresh = UnityEngine.Time.unscaledTime + _refreshInterval;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
                _visible = !_visible;
#endif
            if (_visible && UnityEngine.Time.unscaledTime >= _nextRefresh)
                Refresh();
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = _fontSize, wordWrap = false, richText = false };

            GUILayout.BeginArea(new Rect(8f, 8f, _size.x, _size.y), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(Text, _style);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
