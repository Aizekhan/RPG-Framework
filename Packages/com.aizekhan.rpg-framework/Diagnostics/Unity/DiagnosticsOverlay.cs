using System;
using System.Collections.Generic;
using UnityEngine;
#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RPGFramework.Diagnostics.Unity
{
    /// <summary>
    /// The screen corner a <see cref="DiagnosticsOverlay"/> stands in.
    /// </summary>
    public enum DiagnosticsCorner
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3
    }

    /// <summary>
    /// IMGUI overlay that shows a <see cref="DiagnosticsHub"/> report over the running game, refreshed on unscaled
    /// time (<see cref="RefreshInterval"/>, 0 = every frame). Assign <see cref="Hub"/> from your bootstrap; toggle with
    /// <see cref="Visible"/> or the toggle key (F3 by default, <see cref="ToggleKey"/> from code; read through the
    /// Input System when it is installed and enabled, else the legacy input manager). <see cref="Corner"/>,
    /// <see cref="Opacity"/>, <see cref="Size"/> (0 = fit the text) and <see cref="Sections"/> make it a small
    /// see-through panel; <see cref="DiagnosticsPanelLayout"/> sets them at once.
    /// </summary>
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        private const float Margin = 8f;
        private const float Padding = 6f;

        [SerializeField] private bool _visible = true;
        [Tooltip("Seconds between refreshes, unscaled (0 = every frame)")]
        [SerializeField, Min(0f)] private float _refreshInterval = 0.25f;
        [SerializeField] private int _fontSize = 14;
        [Tooltip("Width and height of the panel (0 = fit the text)")]
        [SerializeField] private Vector2 _size = new Vector2(520f, 420f);
        [SerializeField] private DiagnosticsCorner _corner = DiagnosticsCorner.TopLeft;
        [Tooltip("The background's opacity (0 = only the text, 1 = the skin's box)")]
        [SerializeField, Range(0f, 1f)] private float _opacity = 1f;
        [Tooltip("Titles of the sections shown, e.g. Performance and Render (empty = all)")]
        [SerializeField] private string[] _sections = Array.Empty<string>();
        [Tooltip("Show the '== Title ==' line above each section")]
        [SerializeField] private bool _headers = true;
#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        [SerializeField] private Key _toggleKey = Key.F3;
#elif ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;
#endif

#if !(RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM) && !ENABLE_LEGACY_INPUT_MANAGER
        private KeyCode _unreadKey = KeyCode.F3;
#endif
        private float _nextRefresh;
        private Vector2 _scroll;
        private GUIStyle _style;
        private GUIStyle _shadow;
        private readonly GUIContent _content = new GUIContent();

        public DiagnosticsHub Hub { get; set; }

        /// <summary>
        /// Seconds between refreshes on unscaled time; 0 refreshes every frame.
        /// </summary>
        public float RefreshInterval
        {
            get => _refreshInterval;
            set => _refreshInterval = Mathf.Max(0f, value);
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = Mathf.Max(1, value);
                _style = null;
            }
        }

        /// <summary>
        /// The panel's width and height in pixels; 0 fits that side to the text.
        /// </summary>
        public Vector2 Size
        {
            get => _size;
            set => _size = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }

        public DiagnosticsCorner Corner
        {
            get => _corner;
            set => _corner = value;
        }

        /// <summary>
        /// The background's opacity: 1 the skin's box, 0 only the text (drawn with a shadow under 1).
        /// </summary>
        public float Opacity
        {
            get => _opacity;
            set => _opacity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Titles of the sections shown (<see cref="IDiagnosticsSection.Title"/>, e.g. "Performance", "Render");
        /// empty: all of them.
        /// </summary>
        public IReadOnlyList<string> Sections
        {
            get => _sections;
            set
            {
                if (value == null)
                {
                    _sections = Array.Empty<string>();
                    return;
                }
                var copy = new string[value.Count];
                for (int i = 0; i < copy.Length; i++)
                    copy[i] = value[i];
                _sections = copy;
            }
        }

        /// <summary>
        /// Whether each section starts with its "== Title ==" line.
        /// </summary>
        public bool Headers
        {
            get => _headers;
            set => _headers = value;
        }

        public bool Visible
        {
            get => _visible;
            set => _visible = value;
        }

        /// <summary>
        /// The key that shows and hides the overlay (<c>KeyCode.None</c>: no key), settable from code, e.g. by a host
        /// that adds the overlay at runtime. With the Input System the key is matched by name (letters, F-keys,
        /// digits and the keypad).
        /// </summary>
        public KeyCode ToggleKey
        {
#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            get => KeyCodeOf(_toggleKey);
            set => _toggleKey = KeyOf(value);
#elif ENABLE_LEGACY_INPUT_MANAGER
            get => _toggleKey;
            set => _toggleKey = value;
#else
            get => _unreadKey;
            set => _unreadKey = value;
#endif
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
            Text = Hub != null ? Hub.Render(_sections, _headers).TrimEnd() : "No DiagnosticsHub assigned";
            _nextRefresh = UnityEngine.Time.unscaledTime + _refreshInterval;
        }

        private void Update()
        {
#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (_toggleKey != Key.None && keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
                _visible = !_visible;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (_toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
                _visible = !_visible;
#endif
            if (_visible && UnityEngine.Time.unscaledTime >= _nextRefresh)
                Refresh();
        }

#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        // By name: Alpha5 is Digit5, Keypad5 is Numpad5; letters and F-keys share their names
        internal static Key KeyOf(KeyCode code)
        {
            if (code == KeyCode.None)
                return Key.None;
            string name = code.ToString().Replace("Alpha", "Digit").Replace("Keypad", "Numpad");
            return System.Enum.TryParse(name, out Key key) ? key : Key.None;
        }

        internal static KeyCode KeyCodeOf(Key key)
        {
            if (key == Key.None)
                return KeyCode.None;
            string name = key.ToString().Replace("Digit", "Alpha").Replace("Numpad", "Keypad");
            return System.Enum.TryParse(name, out KeyCode code) ? code : KeyCode.None;
        }
#endif

        private void OnGUI()
        {
            if (!_visible)
                return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = _fontSize, wordWrap = false, richText = false };
                _shadow = new GUIStyle(_style);
                _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }

            _content.text = Text;
            var fit = _style.CalcSize(_content);
            float width = _size.x > 0f ? _size.x : fit.x + Padding * 2f;
            float height = _size.y > 0f ? _size.y : fit.y + Padding * 2f;
            width = Mathf.Min(width, Mathf.Max(0f, Screen.width - Margin * 2f));
            height = Mathf.Min(height, Mathf.Max(0f, Screen.height - Margin * 2f));
            bool right = _corner == DiagnosticsCorner.TopRight || _corner == DiagnosticsCorner.BottomRight;
            bool bottom = _corner == DiagnosticsCorner.BottomLeft || _corner == DiagnosticsCorner.BottomRight;
            var area = new Rect(right ? Screen.width - Margin - width : Margin, bottom ? Screen.height - Margin - height : Margin,
                width, height);

            if (_opacity > 0f)
            {
                var background = GUI.backgroundColor;
                GUI.backgroundColor = new Color(background.r, background.g, background.b, background.a * _opacity);
                GUI.Box(area, GUIContent.none);
                GUI.backgroundColor = background;
            }

            var inner = new Rect(area.x + Padding, area.y + Padding, area.width - Padding * 2f, area.height - Padding * 2f);
            if (fit.y <= inner.height + 0.5f)
            {
                if (_opacity < 1f)
                    GUI.Label(new Rect(inner.x + 1f, inner.y + 1f, inner.width, inner.height), _content, _shadow);
                GUI.Label(inner, _content, _style);
                return;
            }
            GUILayout.BeginArea(inner);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_content, _style);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
