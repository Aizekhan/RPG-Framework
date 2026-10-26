using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic shop/trading panel for buying and selling items.
    /// Displays merchant inventory with buy prices and player inventory with sell prices.
    /// </summary>
    public class ShopPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;

        [Header("Shop Info")]
        [SerializeField] protected TextMeshProUGUI shopNameText;
        [SerializeField] protected TextMeshProUGUI playerGoldText;

        [Header("Tabs")]
        [SerializeField] protected Toggle buyTab;
        [SerializeField] protected Toggle sellTab;

        [Header("Buy View")]
        [SerializeField] protected Transform buyItemsContainer;
        [SerializeField] protected GameObject buyItemEntryPrefab;

        [Header("Sell View")]
        [SerializeField] protected Transform sellItemsContainer;
        [SerializeField] protected GameObject sellItemEntryPrefab;

        [Header("Item Info")]
        [SerializeField] protected TextMeshProUGUI selectedItemNameText;
        [SerializeField] protected TextMeshProUGUI selectedItemDescriptionText;
        [SerializeField] protected TextMeshProUGUI selectedItemPriceText;
        [SerializeField] protected Button confirmButton;
        [SerializeField] protected TextMeshProUGUI confirmButtonText;

        // Events
        public event Action<string, int> OnItemPurchased; // itemId, quantity
        public event Action<string, int> OnItemSold; // itemId, quantity
        public event Action OnShopClosed;

        protected List<GameObject> buyEntries = new List<GameObject>();
        protected List<GameObject> sellEntries = new List<GameObject>();
        protected IItem selectedItem;
        protected bool isBuyMode = true;
        protected int playerGold;

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseShop);

            if (buyTab != null)
                buyTab.onValueChanged.AddListener(isOn => { if (isOn) SwitchToBuyMode(); });

            if (sellTab != null)
                sellTab.onValueChanged.AddListener(isOn => { if (isOn) SwitchToSellMode(); });

            if (confirmButton != null)
                confirmButton.onClick.AddListener(ConfirmTransaction);
        }

        /// <summary>
        /// Opens shop with merchant's inventory.
        /// </summary>
        public virtual void OpenShop(string shopName, List<IItem> merchantItems, int playerGold)
        {
            if (shopNameText != null)
                shopNameText.text = shopName;

            this.playerGold = playerGold;
            UpdatePlayerGoldDisplay();

            PopulateBuyItems(merchantItems);
            SwitchToBuyMode();

            Show();
        }

        /// <summary>
        /// Updates player's inventory for selling.
        /// </summary>
        public virtual void UpdatePlayerInventory(List<IItem> playerItems)
        {
            PopulateSellItems(playerItems);
        }

        /// <summary>
        /// Updates player's gold display.
        /// </summary>
        public virtual void UpdatePlayerGold(int gold)
        {
            playerGold = gold;
            UpdatePlayerGoldDisplay();
        }

        protected virtual void UpdatePlayerGoldDisplay()
        {
            if (playerGoldText != null)
                playerGoldText.text = $"Gold: {playerGold}";
        }

        protected virtual void SwitchToBuyMode()
        {
            isBuyMode = true;

            if (buyItemsContainer != null)
                buyItemsContainer.gameObject.SetActive(true);

            if (sellItemsContainer != null)
                sellItemsContainer.gameObject.SetActive(false);

            if (confirmButtonText != null)
                confirmButtonText.text = "Buy";

            ClearSelection();
        }

        protected virtual void SwitchToSellMode()
        {
            isBuyMode = false;

            if (buyItemsContainer != null)
                buyItemsContainer.gameObject.SetActive(false);

            if (sellItemsContainer != null)
                sellItemsContainer.gameObject.SetActive(true);

            if (confirmButtonText != null)
                confirmButtonText.text = "Sell";

            ClearSelection();
        }

        protected virtual void PopulateBuyItems(List<IItem> items)
        {
            ClearBuyEntries();

            if (buyItemsContainer == null || buyItemEntryPrefab == null || items == null) return;

            foreach (var item in items)
            {
                var entry = Instantiate(buyItemEntryPrefab, buyItemsContainer);
                buyEntries.Add(entry);
                SetupBuyEntry(entry, item);
            }
        }

        protected virtual void PopulateSellItems(List<IItem> items)
        {
            ClearSellEntries();

            if (sellItemsContainer == null || sellItemEntryPrefab == null || items == null) return;

            foreach (var item in items)
            {
                var entry = Instantiate(sellItemEntryPrefab, sellItemsContainer);
                sellEntries.Add(entry);
                SetupSellEntry(entry, item);
            }
        }

        protected virtual void SetupBuyEntry(GameObject entry, IItem item)
        {
            var text = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                int price = GetBuyPrice(item);
                text.text = $"{item.Name} - {price}g";
            }

            var button = entry.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => SelectItem(item, isBuyMode: true));
            }
        }

        protected virtual void SetupSellEntry(GameObject entry, IItem item)
        {
            var text = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                int price = GetSellPrice(item);
                text.text = $"{item.Name} - {price}g";
            }

            var button = entry.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => SelectItem(item, isBuyMode: false));
            }
        }

        protected virtual void SelectItem(IItem item, bool isBuyMode)
        {
            selectedItem = item;

            if (selectedItemNameText != null)
                selectedItemNameText.text = item.Name;

            if (selectedItemDescriptionText != null)
                selectedItemDescriptionText.text = item.Description;

            int price = isBuyMode ? GetBuyPrice(item) : GetSellPrice(item);
            if (selectedItemPriceText != null)
                selectedItemPriceText.text = $"Price: {price}g";

            // Enable/disable confirm button based on affordability
            if (confirmButton != null)
            {
                bool canAfford = isBuyMode ? playerGold >= price : true;
                confirmButton.interactable = canAfford;
            }
        }

        protected virtual void ClearSelection()
        {
            selectedItem = null;

            if (selectedItemNameText != null)
                selectedItemNameText.text = "";

            if (selectedItemDescriptionText != null)
                selectedItemDescriptionText.text = "";

            if (selectedItemPriceText != null)
                selectedItemPriceText.text = "";

            if (confirmButton != null)
                confirmButton.interactable = false;
        }

        protected virtual void ConfirmTransaction()
        {
            if (selectedItem == null) return;

            if (isBuyMode)
            {
                OnItemPurchased?.Invoke(selectedItem.ItemId, 1);
            }
            else
            {
                OnItemSold?.Invoke(selectedItem.ItemId, 1);
            }

            ClearSelection();
        }

        /// <summary>
        /// Gets buy price for an item.
        /// Game layer overrides with actual pricing logic.
        /// </summary>
        protected virtual int GetBuyPrice(IItem item)
        {
            return 0; // Game layer implements
        }

        /// <summary>
        /// Gets sell price for an item.
        /// Game layer overrides with actual pricing logic.
        /// </summary>
        protected virtual int GetSellPrice(IItem item)
        {
            return 0; // Game layer implements
        }

        protected virtual void ClearBuyEntries()
        {
            foreach (var entry in buyEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            buyEntries.Clear();
        }

        protected virtual void ClearSellEntries()
        {
            foreach (var entry in sellEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            sellEntries.Clear();
        }

        protected virtual void CloseShop()
        {
            OnShopClosed?.Invoke();
            ClearSelection();
            Close();
        }
    }
}
