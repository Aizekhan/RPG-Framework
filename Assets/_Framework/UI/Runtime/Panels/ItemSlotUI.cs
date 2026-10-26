using UnityEngine;
using UnityEngine.UI;
using System;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// UI slot that shows one item (equipment or inventory). Attach to the slot prefab or GameObject;
    /// wire OnPointerEnter/OnPointerExit to an EventTrigger for tooltips.
    /// </summary>
    public class ItemSlotUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private GameObject emptyIndicator;
        [SerializeField] private Button slotButton;

        public IItem CurrentItem { get; private set; }

        public event Action OnSlotClicked;
        public event Action<IItem> OnSlotHoverEnter;
        public event Action OnSlotHoverExit;

        private void Awake()
        {
            if (slotButton != null)
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
                    // Set icon sprite (override to implement)
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
