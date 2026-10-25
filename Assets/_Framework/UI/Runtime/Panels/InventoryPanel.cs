using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using RPGFramework.Inventory;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic inventory panel for displaying items and equipment.
    /// Can be extended/customized for game-specific needs.
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
        protected IInventoryManager inventoryManager;

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            CreateInventorySlots();
        }

        /// <summary>
        /// Initializes panel with inventory manager.
        /// </summary>
        public virtual void Initialize(IInventoryManager inventoryManager)
        {
            this.inventoryManager = inventoryManager;
            RefreshInventory();
        }

        /// <summary>
        /// Refreshes inventory display.
        /// </summary>
        public virtual void RefreshInventory()
        {
            if (inventoryManager == null) return;

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
            }
        }

        protected virtual void ClearSlots()
        {
            // Override to clear slot contents
        }

        protected virtual void DisplayItems()
        {
            // Override to display items from inventory
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
