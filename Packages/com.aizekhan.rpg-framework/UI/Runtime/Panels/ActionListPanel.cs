using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// A button on a row of <see cref="ActionListPanel"/>: its caption, whether it can be pressed now, what it does.
    /// </summary>
    public sealed class ActionButton
    {
        public ActionButton(string label, Action click, Func<bool> enabled = null)
            : this(() => label, click, enabled)
        {
        }

        /// <param name="label">Read again with the row ("Buy 40g" when the price depends on who buys).</param>
        public ActionButton(Func<string> label, Action click, Func<bool> enabled = null)
        {
            Label = label ?? (() => "");
            Click = click;
            Enabled = enabled;
        }

        public Func<string> Label { get; }
        public Action Click { get; }

        /// <summary>
        /// Null: always.
        /// </summary>
        public Func<bool> Enabled { get; }
    }

    /// <summary>
    /// A row of <see cref="ActionListPanel"/>: a picture, a text and buttons of its own (a trainer's "+" and "Rank up",
    /// a shop's "Buy" and "Wear").
    /// </summary>
    public sealed class ActionRow
    {
        public ActionRow(Func<string> text, Func<Sprite> icon = null, params ActionButton[] buttons)
        {
            Text = text ?? (() => "");
            Icon = icon;
            Buttons = buttons ?? Array.Empty<ActionButton>();
        }

        public Func<string> Text { get; }

        /// <summary>
        /// Null: the row has no picture; a function that returns null leaves the picture blank.
        /// </summary>
        public Func<Sprite> Icon { get; }

        public IReadOnlyList<ActionButton> Buttons { get; }
    }

    /// <summary>
    /// A panel that builds itself: one row per thing to act on (a stat to train, an item for sale, a recipe, a road),
    /// each with a picture, a text and its own buttons, kept up to date while shown (texts, captions, which buttons
    /// can be pressed). No prefab or fields to wire. Sizes can be set in code before or after
    /// <see cref="Bind"/>. Works placed in a scene or as a screen of <c>INavigationService</c>.
    /// </summary>
    public class ActionListPanel : UIViewBase
    {
        [SerializeField, Min(8)] private int _fontSize = 24;
        [SerializeField, Min(200f)] private float _width = 720f;
        [SerializeField, Min(0f)] private float _iconSize = 56f;
        [SerializeField, Min(40f)] private float _buttonWidth = 140f;
        [SerializeField, Min(0f), Tooltip("Seconds between updates while shown")] private float _refreshSeconds = 0.25f;
        [SerializeField] private Color _panelColor = new Color(0.05f, 0.05f, 0.07f, 0.92f);
        [SerializeField, Tooltip("Where the box hangs on this object: (0.5, 0.5) centred, (0.5, 1) from the top")]
        private Vector2 _anchor = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 _offset;
        [SerializeField] private Color _buttonColor = new Color(0.22f, 0.32f, 0.5f, 1f);
        [SerializeField, Min(0f), Tooltip("The rows scroll when taller than this; 0: the box grows with its rows")]
        private float _maxHeight;

        private sealed class Shown
        {
            public ActionRow Row;
            public Image Icon;
            public Text Text;
            public readonly List<(ActionButton Action, Button Button, Text Caption)> Buttons =
                new List<(ActionButton, Button, Text)>();
        }

        private readonly List<Shown> _rows = new List<Shown>();
        private readonly List<ActionRow> _bound = new List<ActionRow>();
        private RectTransform _panel;
        private RectTransform _list;
        private RectTransform _viewport;
        private GameObject _lastSelected;
        private Text _title;
        private GameObject _titleRow;
        private string _titleText = "";
        private float _sinceRefresh;

        public int RowCount => _rows.Count;

        /// <summary>
        /// The text of a row as shown now (for tests and tooltips).
        /// </summary>
        public string TextAt(int row) => _rows[row].Text.text;

        /// <summary>
        /// A button of a row (to read its caption or whether it can be pressed).
        /// </summary>
        public Button ButtonAt(int row, int button) => _rows[row].Buttons[button].Button;

        public string Title
        {
            get => _titleText;
            set
            {
                Build();
                _titleText = value ?? "";
                _title.text = _titleText;
                _titleRow.SetActive(_titleText.Length > 0);
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set => Resize(ref _fontSize, Math.Max(8, value));
        }

        /// <summary>
        /// Where the box hangs on this object, and its pivot: (0.5, 0.5) centred, (0.5, 1) from the top, (0, 1) top
        /// left. <see cref="Offset"/> moves it from there. Takes effect at once.
        /// </summary>
        public Vector2 Anchor
        {
            get => _anchor;
            set
            {
                _anchor = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
                Place();
            }
        }

        /// <summary>
        /// The box's distance from <see cref="Anchor"/> (y up: (0, -20) is 20 below the top).
        /// </summary>
        public Vector2 Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                Place();
            }
        }

        public float Width
        {
            get => _width;
            set => Resize(ref _width, Math.Max(200f, value));
        }

        /// <summary>
        /// The side of a row's picture; the row is at least this tall.
        /// </summary>
        public float IconSize
        {
            get => _iconSize;
            set => Resize(ref _iconSize, Math.Max(0f, value));
        }

        public float ButtonWidth
        {
            get => _buttonWidth;
            set => Resize(ref _buttonWidth, Math.Max(40f, value));
        }

        /// <summary>
        /// The rows scroll (mouse wheel, drag, a selected button brought into view) when they are taller than this;
        /// 0: the box grows with its rows.
        /// </summary>
        public float MaxHeight
        {
            get => _maxHeight;
            set => Resize(ref _maxHeight, Math.Max(0f, value));
        }

        /// <summary>
        /// Whether the rows are taller than <see cref="MaxHeight"/>.
        /// </summary>
        public bool Scrolls => _maxHeight > 0f && RowsHeight > _maxHeight;

        /// <summary>
        /// How far the rows are scrolled down, in pixels from the first row's top.
        /// </summary>
        public float ScrollOffset => _viewport != null ? _list.anchoredPosition.y : 0f;

        /// <summary>
        /// Scrolls just enough for a row to be in view (nothing when everything fits).
        /// </summary>
        public void ScrollTo(int row)
        {
            if (_viewport == null || row < 0 || row >= _rows.Count)
                return;
            float top = row * (RowHeight + RowSpacing);
            float bottom = top + RowHeight;
            float offset = _list.anchoredPosition.y;
            if (top < offset)
                offset = top;
            else if (bottom > offset + _maxHeight)
                offset = bottom - _maxHeight;
            offset = Mathf.Clamp(offset, 0f, Mathf.Max(0f, RowsHeight - _maxHeight));
            _list.anchoredPosition = new Vector2(_list.anchoredPosition.x, offset);
        }

        private float RowHeight => Mathf.Max(_iconSize, _fontSize * 2f);
        private float RowSpacing => _fontSize * 0.3f;
        private float RowsHeight => _rows.Count == 0 ? 0f : _rows.Count * RowHeight + (_rows.Count - 1) * RowSpacing;

        /// <summary>
        /// Shows these rows, in this order. Replaces the rows shown before.
        /// </summary>
        public void Bind(IEnumerable<ActionRow> rows)
        {
            var list = new List<ActionRow>();
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (row != null)
                        list.Add(row);
                }
            }
            _bound.Clear();
            _bound.AddRange(list);
            Build();
            for (int i = _list.childCount - 1; i >= 0; i--)
                UIBuilder.Discard(_list.GetChild(i).gameObject);
            _rows.Clear();
            foreach (var row in _bound)
                AddRow(row);
            if (_viewport != null)
            {
                UIBuilder.Sized(_viewport, 0f, Mathf.Max(1f, Mathf.Min(RowsHeight, _maxHeight)));
                _list.anchoredPosition = Vector2.zero;
            }
            Refresh();
        }

        /// <summary>
        /// Reads every text, caption and button state again now.
        /// </summary>
        public void Refresh()
        {
            _sinceRefresh = 0f;
            foreach (var shown in _rows)
            {
                Set(shown.Text, Read(shown.Row.Text));
                if (shown.Icon != null)
                {
                    Sprite sprite = null;
                    try
                    {
                        sprite = shown.Row.Icon();
                    }
                    catch (Exception)
                    {
                        // A picture whose owner is gone shows nothing instead of breaking the panel
                    }
                    shown.Icon.sprite = sprite;
                    shown.Icon.enabled = sprite != null;
                }
                foreach (var (action, button, caption) in shown.Buttons)
                {
                    Set(caption, Read(action.Label));
                    bool enabled;
                    try
                    {
                        enabled = action.Click != null && (action.Enabled == null || action.Enabled());
                    }
                    catch (Exception)
                    {
                        enabled = false;
                    }
                    if (button.interactable != enabled)
                        button.interactable = enabled;
                }
            }
        }

        protected override void OnShow() => Refresh();

        protected virtual void LateUpdate()
        {
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= _refreshSeconds)
                Refresh();
            FollowSelection();
        }

        // A button chosen with the keys or a pad is brought into view
        private void FollowSelection()
        {
            if (_viewport == null)
                return;
            var selected = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                : null;
            if (selected == _lastSelected)
                return;
            _lastSelected = selected;
            if (selected == null)
                return;
            for (int i = 0; i < _rows.Count; i++)
            {
                foreach (var (_, button, _) in _rows[i].Buttons)
                {
                    if (button.gameObject == selected)
                    {
                        ScrollTo(i);
                        return;
                    }
                }
            }
        }

        private void AddRow(ActionRow row)
        {
            float height = Mathf.Max(_iconSize, _fontSize * 2f);
            var line = new GameObject("Row", typeof(RectTransform));
            line.transform.SetParent(_list, false);
            UIBuilder.Sized((RectTransform)line.transform, 0f, height);
            var layout = line.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = _fontSize * 0.5f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var shown = new Shown { Row = row };
            if (row.Icon != null && _iconSize > 0f)
            {
                var icon = UIBuilder.Box(line.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white)
                    .GetComponent<Image>();
                icon.preserveAspect = true;
                UIBuilder.Sized(icon, _iconSize, _iconSize);
                shown.Icon = icon;
            }

            shown.Text = UIBuilder.Label(line.transform, "Text", "", _fontSize, TextAnchor.MiddleLeft);
            UIBuilder.Sized(shown.Text, 0f, height).GetComponent<LayoutElement>().flexibleWidth = 1f;

            for (int i = 0; i < row.Buttons.Count; i++)
            {
                var action = row.Buttons[i];
                if (action == null)
                    continue;
                var button = UIBuilder.Button(line.transform, "Button" + i, "", _fontSize, _buttonColor);
                UIBuilder.Sized(button, _buttonWidth, Mathf.Min(height, _fontSize * 2f));
                button.onClick.AddListener(() =>
                {
                    action.Click?.Invoke();
                    Refresh();
                });
                shown.Buttons.Add((action, button, button.GetComponentInChildren<Text>()));
            }
            _rows.Add(shown);
        }

        private void Build()
        {
            if (_list != null)
                return;
            _panel = UIBuilder.Box(transform, "Panel", _anchor, _offset, new Vector2(_width, 0f), _panelColor);
            var layout = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            int padding = Mathf.RoundToInt(_fontSize * 0.8f);
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = _fontSize * 0.5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _titleRow = new GameObject("Title", typeof(RectTransform));
            _titleRow.transform.SetParent(_panel, false);
            UIBuilder.Sized((RectTransform)_titleRow.transform, 0f, _fontSize * 1.8f);
            _title = UIBuilder.Label(_titleRow.transform, "Text", _titleText, Mathf.RoundToInt(_fontSize * 1.25f), TextAnchor.MiddleLeft);
            _titleRow.SetActive(_titleText.Length > 0);

            if (_maxHeight <= 0f)
            {
                _viewport = null;
                _list = UIBuilder.List(_panel, "Rows", vertical: true, spacing: RowSpacing);
                return;
            }

            // The rows hang from the top of a window they scroll in
            _viewport = UIBuilder.Box(_panel, "Viewport", new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            _viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // the wheel works between rows too
            _viewport.gameObject.AddComponent<RectMask2D>();
            _list = UIBuilder.List(_viewport, "Rows", vertical: true, spacing: RowSpacing);
            _list.anchorMin = new Vector2(0f, 1f);
            _list.anchorMax = new Vector2(1f, 1f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.anchoredPosition = Vector2.zero;
            _list.sizeDelta = new Vector2(0f, _list.sizeDelta.y);
            var scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _list;
            scroll.viewport = _viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = _fontSize * 2f;
        }

        // A size changed: an already built panel is built again with the same rows
        private void Resize<T>(ref T field, T value)
        {
            field = value;
            if (_panel == null)
                return;
            UIBuilder.Discard(_panel.gameObject);
            _panel = null;
            _list = null;
            _viewport = null;
            Bind(new List<ActionRow>(_bound));
        }

        private static string Read(Func<string> text)
        {
            try
            {
                return text?.Invoke() ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static void Set(Text label, string text)
        {
            if (label.text != text)
                label.text = text;
        }

        private void Place()
        {
            if (_panel == null)
                return;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = _anchor;
            _panel.anchoredPosition = _offset;
        }
    }
}
