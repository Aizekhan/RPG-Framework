using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using RPGFramework.Loot;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic loot panel for displaying dropped items.
    /// Auto-opens when loot is available.
    /// </summary>
    public class LootPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;
        [SerializeField] protected Button takeAllButton;

        [Header("Loot List")]
        [SerializeField] protected Transform lootListContainer;
        [SerializeField] protected GameObject lootEntryPrefab;

        [Header("Loot Info")]
        [SerializeField] protected TextMeshProUGUI lootSourceText;
        [SerializeField] protected TextMeshProUGUI totalItemsText;

        // Events
        public event Action<string> OnItemTaken; // itemId
        public event Action OnTakeAll;
        public event Action OnLootClosed;

        protected List<GameObject> lootEntries = new List<GameObject>();
        protected List<LootDrop> currentLoot = new List<LootDrop>();

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseLoot);

            if (takeAllButton != null)
                takeAllButton.onClick.AddListener(TakeAll);
        }

        /// <summary>
        /// Displays loot from a loot result.
        /// </summary>
        public virtual void DisplayLoot(LootResult lootResult, string sourceName = "Loot")
        {
            if (lootResult == null || lootResult.Drops == null) return;

            currentLoot.Clear();
            currentLoot.AddRange(lootResult.Drops);

            if (lootSourceText != null)
                lootSourceText.text = sourceName;

            if (totalItemsText != null)
                totalItemsText.text = $"Items: {currentLoot.Count}";

            RefreshLootList();
            Show();
        }

        protected virtual void RefreshLootList()
        {
            ClearLootList();

            if (lootListContainer == null || lootEntryPrefab == null) return;

            foreach (var drop in currentLoot)
            {
                var entry = Instantiate(lootEntryPrefab, lootListContainer);
                lootEntries.Add(entry);

                // Setup entry (override to customize)
                SetupLootEntry(entry, drop);
            }
        }

        protected virtual void SetupLootEntry(GameObject entry, LootDrop drop)
        {
            var text = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"{drop.ItemId} x{drop.Quantity}";
            }

            var button = entry.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => TakeItem(drop.ItemId));
            }
        }

        protected virtual void TakeItem(string itemId)
        {
            // Remove from current loot
            currentLoot.RemoveAll(d => d.ItemId == itemId);

            OnItemTaken?.Invoke(itemId);
            RefreshLootList();

            // Auto-close if empty
            if (currentLoot.Count == 0)
            {
                CloseLoot();
            }
        }

        protected virtual void TakeAll()
        {
            OnTakeAll?.Invoke();
            currentLoot.Clear();
            CloseLoot();
        }

        protected virtual void CloseLoot()
        {
            OnLootClosed?.Invoke();
            currentLoot.Clear();
            Hide();
        }

        protected virtual void ClearLootList()
        {
            foreach (var entry in lootEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            lootEntries.Clear();
        }
    }
}
