using System;
using System.Collections.Generic;
using RPGFramework.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// A panel that builds itself: one <see cref="ItemSlotUI"/> per equipment slot of the game's own list (a runner's
    /// "board" and "charm", an RPG's eight), with the slot's name and the item in it, kept up to date while shown.
    /// No prefab or fields to wire. Works placed in a scene or as a screen of <c>INavigationService</c>.
    /// </summary>
    public class EquipmentSlotsPanel : UIViewBase
    {
        [SerializeField, Min(8)] private int _fontSize = 24;
        [SerializeField, Min(24f)] private float _slotSize = 72f;
        [SerializeField, Min(200f)] private float _width = 460f;
        [SerializeField, Min(0f), Tooltip("Seconds between updates while shown")] private float _refreshSeconds = 0.25f;
        [SerializeField] private Color _panelColor = new Color(0.05f, 0.05f, 0.07f, 0.92f);

        private readonly Dictionary<string, ItemSlotUI> _slots = new Dictionary<string, ItemSlotUI>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();
        private Func<string, IItem> _itemIn;
        private RectTransform _list;
        private float _sinceRefresh;

        /// <summary>
        /// A slot was clicked (to unequip, to pick what goes in it).
        /// </summary>
        public event Action<string> SlotClicked;

        public IReadOnlyList<string> SlotIds => _order;

        /// <returns>The slot's view, or null.</returns>
        public ItemSlotUI SlotOf(string slotId) => slotId != null && _slots.TryGetValue(slotId, out var slot) ? slot : null;

        /// <summary>
        /// Shows these slots, in this order. Replaces the slots shown before.
        /// </summary>
        /// <param name="slots">The game's slot ids.</param>
        /// <param name="itemIn">What is in a slot now (null: empty).</param>
        /// <param name="iconOf">The picture of an item; null leaves icons blank.</param>
        /// <param name="slotName">A slot's name on screen; its id when null.</param>
        public void Bind(IEnumerable<string> slots, Func<string, IItem> itemIn, Func<IItem, Sprite> iconOf = null,
            Func<string, string> slotName = null)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));
            Build();
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                var child = _list.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _slots.Clear();
            _order.Clear();
            _itemIn = itemIn;

            foreach (string id in slots)
            {
                if (string.IsNullOrEmpty(id) || _slots.ContainsKey(id))
                    continue;
                var row = new GameObject(id, typeof(RectTransform));
                row.transform.SetParent(_list, false);
                UIBuilder.Sized((RectTransform)row.transform, 0f, _slotSize);
                var slot = ItemSlotUI.Create(row.transform, "Slot", _slotSize, iconOf);
                var slotRect = (RectTransform)slot.transform;
                slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0f, 0.5f);
                slotRect.anchoredPosition = Vector2.zero;
                var label = UIBuilder.Label(row.transform, "Name", slotName != null ? slotName(id) : id, _fontSize, TextAnchor.MiddleLeft);
                label.rectTransform.offsetMin = new Vector2(_slotSize + _fontSize * 0.6f, 0f);
                string clicked = id;
                slot.OnSlotClicked += () => SlotClicked?.Invoke(clicked);
                _slots.Add(id, slot);
                _order.Add(id);
            }
            Refresh();
        }

        /// <summary>
        /// Asks what is in every slot again now.
        /// </summary>
        public void Refresh()
        {
            _sinceRefresh = 0f;
            if (_itemIn == null)
                return;
            foreach (string id in _order)
            {
                var slot = _slots[id];
                var item = _itemIn(id);
                if (!ReferenceEquals(item, slot.CurrentItem))
                    slot.SetItem(item);
            }
        }

        protected override void OnShow() => Refresh();

        protected virtual void LateUpdate()
        {
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= _refreshSeconds)
                Refresh();
        }

        private void Build()
        {
            if (_list != null)
                return;
            var panel = UIBuilder.Box(transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(_width, 0f), _panelColor);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            int padding = Mathf.RoundToInt(_fontSize * 0.8f);
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _list = UIBuilder.List(panel, "Slots", vertical: true, spacing: _fontSize * 0.4f);
        }
    }
}
