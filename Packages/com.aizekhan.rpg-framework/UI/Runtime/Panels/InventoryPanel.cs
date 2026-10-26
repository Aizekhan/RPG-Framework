using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic inventory panel: shows the items of an <see cref="IInventoryContainer{TItem}"/> in a grid of
    /// <see cref="ItemSlotUI"/> slots, with tooltips and click events. Override the virtual methods for
    /// game-specific layouts (weight, gold, sorting).
    /// Keyboard shortcut: I
    /// </summary>
    public class InventoryPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;

        [Header("Inventory Grid")]
        [SerializeField] protected GridLayoutGroup inventoryGrid;
        [SerializeField] protected GameObject itemSlotPrefab;
        [SerializeField] protected int gridWidth = 8;
        [SerializeField] protected int gridHeight = 6;

        [Header("Equipment Slots")]
        [SerializeField] protected Transform equipmentSlotsContainer;

        [Header("Item Info")]
        [SerializeField] protected TextMeshProUGUI weightText;
        [SerializeField] protected TextMeshProUGUI goldText;

        [Header("Tooltip")]
        [SerializeField] protected GameObject tooltipPanel;
        [SerializeField] protected TextMeshProUGUI tooltipNameText;
        [SerializeField] protected TextMeshProUGUI tooltipDescriptionText;

        // Events
        public event Action<IItem> OnItemSelected;
        public event Action<IItem> OnItemUsed;
        public event Action<IItem> OnItemDropped;

        protected List<GameObject> itemSlots = new List<GameObject>();
        protected IInventoryContainer<IItem> inventory;

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            CreateInventorySlots();
        }

        /// <summary>
        /// Binds the panel to an inventory and shows its items.
        /// </summary>
        public virtual void Initialize(IInventoryContainer<IItem> inventory)
        {
            this.inventory = inventory;
            RefreshInventory();
        }

        /// <summary>
        /// Refreshes inventory display.
        /// </summary>
        public virtual void RefreshInventory()
        {
            if (inventory == null) return;

            ClearSlots();
            DisplayItems();
            UpdateInfo();
        }

        protected virtual void CreateInventorySlots()
        {
            if (inventoryGrid == null || itemSlotPrefab == null) return;

            int totalSlots = gridWidth * gridHeight;
            for (int i = 0; i < totalSlots; i++)
            {
                var slot = Instantiate(itemSlotPrefab, inventoryGrid.transform);
                slot.name = $"ItemSlot_{i}";
                itemSlots.Add(slot);

                var slotUI = slot.GetComponent<ItemSlotUI>();
                if (slotUI != null)
                {
                    slotUI.OnSlotClicked += () => { if (slotUI.CurrentItem != null) OnItemClicked(slotUI.CurrentItem); };
                    slotUI.OnSlotHoverEnter += ShowTooltip;
                    slotUI.OnSlotHoverExit += HideTooltip;
                }
            }
        }

        protected virtual void ClearSlots()
        {
            foreach (var slot in itemSlots)
            {
                var slotUI = slot != null ? slot.GetComponent<ItemSlotUI>() : null;
                if (slotUI != null)
                    slotUI.ClearSlot();
            }
        }

        protected virtual void DisplayItems()
        {
            var items = inventory.GetAllItems();
            for (int i = 0; i < itemSlots.Count; i++)
            {
                var slotUI = itemSlots[i] != null ? itemSlots[i].GetComponent<ItemSlotUI>() : null;
                if (slotUI != null)
                    slotUI.SetItem(i < items.Count ? items[i] : null);
            }
        }

        protected virtual void UpdateInfo()
        {
            // Override to update weight/gold/stats
        }

        protected virtual void ShowTooltip(IItem item)
        {
            if (tooltipPanel == null) return;

            tooltipPanel.SetActive(true);

            if (tooltipNameText != null)
                tooltipNameText.text = item.Name;

            if (tooltipDescriptionText != null)
                tooltipDescriptionText.text = item.Description;
        }

        protected virtual void HideTooltip()
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        /// <summary>
        /// Called when item is clicked.
        /// </summary>
        protected virtual void OnItemClicked(IItem item)
        {
            OnItemSelected?.Invoke(item);
        }

        /// <summary>
        /// Called when item is used (right-click or use button).
        /// </summary>
        protected virtual void OnItemUsedInternal(IItem item)
        {
            OnItemUsed?.Invoke(item);
        }
    }
}
