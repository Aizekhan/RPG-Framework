using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// UI slot that shows one item (equipment or inventory). Attach to the slot prefab or GameObject, or make one in
    /// code with <see cref="Create"/> (with a caption and a count, and hover that works by itself); the picture comes
    /// from <see cref="IconOf"/>. On a prefab, wire OnPointerEnter/OnPointerExit to an EventTrigger for tooltips.
    /// </summary>
    public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private GameObject emptyIndicator;
        [SerializeField] private Button slotButton;

        private bool _listening;
        // Made in code: the pointer reaches the hover events with no EventTrigger (a prefab's trigger would call twice)
        private bool _hoverByItself;
        private Text _caption;
        private Text _count;
        private int _countShown = -1;

        public IItem CurrentItem { get; private set; }

        /// <summary>
        /// The picture of an item (the game's icons: an atlas, Addressables, a GameSetup); null or no picture leaves
        /// the icon blank.
        /// </summary>
        public Func<IItem, Sprite> IconOf { get; set; }

        public event Action OnSlotClicked;
        public event Action<IItem> OnSlotHoverEnter;
        public event Action OnSlotHoverExit;

        /// <summary>
        /// The text under the picture of a slot made in code ("Head", a price); empty hides it.
        /// </summary>
        public string Caption
        {
            get => _caption != null ? _caption.text : "";
            set
            {
                if (_caption == null)
                    return;
                _caption.text = value ?? "";
                _caption.gameObject.SetActive(_caption.text.Length > 0);
            }
        }

        /// <summary>
        /// How many of the item the slot holds; shown in the corner of a slot made in code when more than one.
        /// </summary>
        public int Count
        {
            get => _countShown < 0 ? 0 : _countShown;
            set
            {
                value = Math.Max(0, value);
                if (value == _countShown)
                    return;
                _countShown = value;
                if (_count == null)
                    return;
                _count.text = value > 1 ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
                _count.gameObject.SetActive(value > 1);
            }
        }

        /// <summary>
        /// A slot made in code: a frame, the icon, a mark shown while empty, a button, a caption under the picture,
        /// a count in the corner (<see cref="Count"/>); hover raises <see cref="OnSlotHoverEnter"/> by itself.
        /// </summary>
        public static ItemSlotUI Create(Transform parent, string name, float size, Func<IItem, Sprite> iconOf = null,
            string caption = null)
        {
            var frame = UIBuilder.Box(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size),
                new Color(0.18f, 0.18f, 0.2f, 0.95f));
            var icon = UIBuilder.Fill(UIBuilder.Box(frame, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white), size * 0.08f)
                .GetComponent<Image>();
            icon.preserveAspect = true;
            var empty = UIBuilder.Fill(UIBuilder.Box(frame, "Empty", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(1f, 1f, 1f, 0.06f)), size * 0.08f).gameObject;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();

            var slot = frame.gameObject.AddComponent<ItemSlotUI>();
            slot.itemIcon = icon;
            slot.emptyIndicator = empty;
            slot.slotButton = button;
            slot.IconOf = iconOf;
            slot._hoverByItself = true;

            int small = Mathf.Max(10, Mathf.RoundToInt(size * 0.22f));
            slot._count = UIBuilder.Label(frame, "Count", "", small, TextAnchor.LowerRight);
            slot._count.rectTransform.offsetMax = new Vector2(-size * 0.08f, 0f);
            slot._count.gameObject.SetActive(false);
            var captionBox = UIBuilder.Box(frame, "Caption", new Vector2(0.5f, 1f), new Vector2(0f, -size - 2f),
                new Vector2(size * 1.6f, small * 1.4f));
            slot._caption = UIBuilder.Label(captionBox, "Text", "", small, TextAnchor.UpperCenter);
            slot._caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            slot.Caption = caption;

            slot.Listen();
            slot.ClearSlot();
            return slot;
        }

        private void Awake() => Listen();

        private void Listen()
        {
            if (_listening || slotButton == null)
                return;
            _listening = true;
            slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke());
        }

        public void SetItem(IItem item)
        {
            CurrentItem = item;

            if (item != null)
            {
                if (itemIcon != null)
                {
                    itemIcon.enabled = true;
                    // Without a function the prefab's own picture stays
                    if (IconOf != null)
                    {
                        itemIcon.sprite = IconOf(item);
                        itemIcon.enabled = itemIcon.sprite != null;
                    }
                }

                if (emptyIndicator != null)
                    emptyIndicator.SetActive(false);
            }
            else
            {
                ClearSlot();
            }
        }

        public void ClearSlot()
        {
            CurrentItem = null;

            if (itemIcon != null)
                itemIcon.enabled = false;

            if (emptyIndicator != null)
                emptyIndicator.SetActive(true);
        }

        public void OnPointerEnter()
        {
            if (CurrentItem != null)
                OnSlotHoverEnter?.Invoke(CurrentItem);
        }

        public void OnPointerExit()
        {
            OnSlotHoverExit?.Invoke();
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            if (_hoverByItself)
                OnPointerEnter();
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            if (_hoverByItself)
                OnPointerExit();
        }
    }
}
