using UnityEngine;
using UnityEngine.UI;
using System;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// UI slot that shows one item (equipment or inventory). Attach to the slot prefab or GameObject, or make one in
    /// code with <see cref="Create"/>; the picture comes from <see cref="IconOf"/>. Wire OnPointerEnter/OnPointerExit
    /// to an EventTrigger for tooltips.
    /// </summary>
    public class ItemSlotUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private GameObject emptyIndicator;
        [SerializeField] private Button slotButton;

        private bool _listening;

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
        /// A slot made in code: a frame, the icon, a mark shown while empty, and a button.
        /// </summary>
        public static ItemSlotUI Create(Transform parent, string name, float size, Func<IItem, Sprite> iconOf = null)
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
    }
}
