using System;
using System.Collections.Generic;
using RPGFramework.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// A panel that builds itself: one row per stat, name on the left and value on the right, kept up to date while
    /// it is shown. Bind it to a stat container (every stat it has) or to rows of your own; no prefab or fields to
    /// wire. Works placed in a scene or as a screen of <c>INavigationService</c> (<c>CodeViewFactory.Register</c>).
    /// </summary>
    public class StatListPanel : UIViewBase
    {
        [SerializeField, Min(8)] private int _fontSize = 24;
        [SerializeField, Min(200f)] private float _width = 560f;
        [SerializeField, Min(0f), Tooltip("Seconds between value updates while shown")] private float _refreshSeconds = 0.25f;
        [SerializeField] private Color _panelColor = new Color(0.05f, 0.05f, 0.07f, 0.92f);

        private sealed class Row
        {
            public Text Value;
            public Func<string> Read;
        }

        private readonly List<Row> _rows = new List<Row>();
        private RectTransform _list;
        private Text _title;
        private GameObject _titleRow;
        private float _sinceRefresh;

        public int RowCount => _rows.Count;

        /// <summary>
        /// The text of a row's value as shown now (for tests and tooltips).
        /// </summary>
        public string ValueAt(int row) => _rows[row].Value.text;

        public string Title
        {
            get => _title != null ? _title.text : "";
            set
            {
                Build();
                _title.text = value ?? "";
                _titleRow.SetActive(!string.IsNullOrEmpty(value));
            }
        }

        /// <summary>
        /// Rows of your own: a name and a function that reads the value ("Gold", () => world.CurrencyOf()).
        /// Replaces the rows shown before.
        /// </summary>
        public void Bind(IEnumerable<(string Label, Func<string> Value)> rows)
        {
            Build();
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                var child = _list.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _rows.Clear();
            if (rows != null)
            {
                foreach (var (label, value) in rows)
                    AddRow(label, value);
            }
            Refresh();
        }

        /// <summary>
        /// One row per stat the container has, with its final value (modifiers included). The container is read
        /// through a function because stat containers are values: pass where it lives now (<c>() => hero.Stats</c>).
        /// </summary>
        /// <param name="name">A stat's name on screen; its enum name when null.</param>
        /// <param name="format">A value on screen; rounded to a whole number when null.</param>
        public void Bind<TStat, TValue>(Func<IStatContainer<TStat, TValue>> stats, Func<TStat, string> name = null,
            Func<TValue, string> format = null)
            where TStat : struct, IConvertible
            where TValue : struct, IComparable<TValue>
        {
            if (stats == null)
                throw new ArgumentNullException(nameof(stats));
            var rows = new List<(string, Func<string>)>();
            foreach (var stat in stats().GetAllStatTypes())
            {
                var shown = stat;
                rows.Add((name != null ? name(stat) : stat.ToString(), () =>
                {
                    var value = stats().GetFinalStat(shown);
                    return format != null ? format(value) : Whole(value);
                }));
            }
            Bind(rows);
        }

        /// <summary>
        /// Reads every value again now.
        /// </summary>
        public void Refresh()
        {
            _sinceRefresh = 0f;
            foreach (var row in _rows)
            {
                string value;
                try
                {
                    value = row.Read?.Invoke() ?? "";
                }
                catch (Exception)
                {
                    // A value whose owner is gone (a character that left) shows nothing instead of breaking the panel
                    value = "";
                }
                if (row.Value.text != value)
                    row.Value.text = value;
            }
        }

        protected override void OnShow() => Refresh();

        protected virtual void LateUpdate()
        {
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= _refreshSeconds)
                Refresh();
        }

        private void AddRow(string label, Func<string> value)
        {
            var row = new GameObject(label ?? "Row", typeof(RectTransform));
            row.transform.SetParent(_list, false);
            UIBuilder.Sized((RectTransform)row.transform, 0f, _fontSize * 1.5f);
            UIBuilder.Label(row.transform, "Name", label, _fontSize, TextAnchor.MiddleLeft);
            var text = UIBuilder.Label(row.transform, "Value", "", _fontSize, TextAnchor.MiddleRight);
            _rows.Add(new Row { Value = text, Read = value });
        }

        private void Build()
        {
            if (_list != null)
                return;
            var panel = UIBuilder.Box(transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(_width, 0f), _panelColor);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            int padding = Mathf.RoundToInt(_fontSize * 0.8f);
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = _fontSize * 0.5f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _titleRow = new GameObject("Title", typeof(RectTransform));
            _titleRow.transform.SetParent(panel, false);
            UIBuilder.Sized((RectTransform)_titleRow.transform, 0f, _fontSize * 1.8f);
            _title = UIBuilder.Label(_titleRow.transform, "Text", "", Mathf.RoundToInt(_fontSize * 1.25f), TextAnchor.MiddleLeft);
            _titleRow.SetActive(false);

            _list = UIBuilder.List(panel, "Rows", vertical: true, spacing: _fontSize * 0.2f);
        }

        private static string Whole<TValue>(TValue value) =>
            Math.Round(Convert.ToDouble(value)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
