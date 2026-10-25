using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using RPGFramework.Items;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic equipment panel for displaying equipped items and stat modifiers.
    /// Shows equipment slots (helmet, armor, weapon, etc.) and total bonuses.
    /// Keyboard shortcut: E
    /// </summary>
    public class EquipmentPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;

        [Header("Equipment Slots")]
        [SerializeField] protected EquipmentSlotUI headSlot;
        [SerializeField] protected EquipmentSlotUI chestSlot;
        [SerializeField] protected EquipmentSlotUI legsSlot;
        [SerializeField] protected EquipmentSlotUI feetSlot;
        [SerializeField] protected EquipmentSlotUI mainHandSlot;
        [SerializeField] protected EquipmentSlotUI offHandSlot;
        [SerializeField] protected EquipmentSlotUI accessory1Slot;
        [SerializeField] protected EquipmentSlotUI accessory2Slot;

        [Header("Stat Bonuses")]
        [SerializeField] protected TextMeshProUGUI totalHealthBonusText;
        [SerializeField] protected TextMeshProUGUI totalManaBonusText;
        [SerializeField] protected TextMeshProUGUI totalStrengthBonusText;
        [SerializeField] protected TextMeshProUGUI totalAgilityBonusText;
        [SerializeField] protected TextMeshProUGUI totalIntelligenceBonusText;
        [SerializeField] protected TextMeshProUGUI totalVitalityBonusText;
        [SerializeField] protected TextMeshProUGUI totalAttackBonusText;
        [SerializeField] protected TextMeshProUGUI totalDefenseBonusText;

        [Header("Tooltip")]
        [SerializeField] protected GameObject tooltipPanel;
        [SerializeField] protected TextMeshProUGUI tooltipNameText;
        [SerializeField] protected TextMeshProUGUI tooltipStatsText;

        // Events
        public event Action<EquipmentSlot, IItem> OnEquipmentChanged;
        public event Action<IItem> OnItemUnequipped;

        protected Dictionary<EquipmentSlot, EquipmentSlotUI> equipmentSlots = new Dictionary<EquipmentSlot, EquipmentSlotUI>();

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            InitializeEquipmentSlots();
        }

        protected virtual void InitializeEquipmentSlots()
        {
            if (headSlot != null) RegisterSlot(EquipmentSlot.Head, headSlot);
            if (chestSlot != null) RegisterSlot(EquipmentSlot.Chest, chestSlot);
            if (legsSlot != null) RegisterSlot(EquipmentSlot.Legs, legsSlot);
            if (feetSlot != null) RegisterSlot(EquipmentSlot.Feet, feetSlot);
            if (mainHandSlot != null) RegisterSlot(EquipmentSlot.MainHand, mainHandSlot);
            if (offHandSlot != null) RegisterSlot(EquipmentSlot.OffHand, offHandSlot);
            if (accessory1Slot != null) RegisterSlot(EquipmentSlot.Accessory1, accessory1Slot);
            if (accessory2Slot != null) RegisterSlot(EquipmentSlot.Accessory2, accessory2Slot);
        }

        protected virtual void RegisterSlot(EquipmentSlot slotType, EquipmentSlotUI slotUI)
        {
            equipmentSlots[slotType] = slotUI;

            if (slotUI != null)
            {
                slotUI.OnSlotClicked += () => OnSlotClicked(slotType);
                slotUI.OnSlotHoverEnter += (item) => ShowTooltip(item);
                slotUI.OnSlotHoverExit += HideTooltip;
            }
        }

        /// <summary>
        /// Updates specific equipment slot with item.
        /// </summary>
        public virtual void UpdateSlot(EquipmentSlot slot, IItem item)
        {
            if (equipmentSlots.TryGetValue(slot, out var slotUI))
            {
                slotUI.SetItem(item);
            }

            UpdateStatBonuses();
        }

        /// <summary>
        /// Clears specific equipment slot.
        /// </summary>
        public virtual void ClearSlot(EquipmentSlot slot)
        {
            if (equipmentSlots.TryGetValue(slot, out var slotUI))
            {
                slotUI.ClearSlot();
            }

            UpdateStatBonuses();
        }

        /// <summary>
        /// Refreshes all equipment slots and stat bonuses.
        /// </summary>
        public virtual void RefreshEquipment()
        {
            UpdateStatBonuses();
        }

        protected virtual void UpdateStatBonuses()
        {
            // Calculate total bonuses from all equipped items
            float healthBonus = 0;
            float manaBonus = 0;
            float strengthBonus = 0;
            float agilityBonus = 0;
            float intelligenceBonus = 0;
            float vitalityBonus = 0;
            float attackBonus = 0;
            float defenseBonus = 0;

            foreach (var slotPair in equipmentSlots)
            {
                var item = slotPair.Value.CurrentItem;
                if (item == null) continue;

                // Sum up stat modifiers (override to implement actual stat calculation)
                // This is a placeholder for demonstration
            }

            // Update UI
            if (totalHealthBonusText != null)
                totalHealthBonusText.text = FormatBonus(healthBonus);

            if (totalManaBonusText != null)
                totalManaBonusText.text = FormatBonus(manaBonus);

            if (totalStrengthBonusText != null)
                totalStrengthBonusText.text = FormatBonus(strengthBonus);

            if (totalAgilityBonusText != null)
                totalAgilityBonusText.text = FormatBonus(agilityBonus);

            if (totalIntelligenceBonusText != null)
                totalIntelligenceBonusText.text = FormatBonus(intelligenceBonus);

            if (totalVitalityBonusText != null)
                totalVitalityBonusText.text = FormatBonus(vitalityBonus);

            if (totalAttackBonusText != null)
                totalAttackBonusText.text = FormatBonus(attackBonus);

            if (totalDefenseBonusText != null)
                totalDefenseBonusText.text = FormatBonus(defenseBonus);
        }

        protected virtual string FormatBonus(float value)
        {
            if (value > 0)
                return $"+{value:F0}";
            else if (value < 0)
                return $"{value:F0}";
            else
                return "0";
        }

        protected virtual void OnSlotClicked(EquipmentSlot slot)
        {
            if (equipmentSlots.TryGetValue(slot, out var slotUI))
            {
                var item = slotUI.CurrentItem;
                if (item != null)
                {
                    OnItemUnequipped?.Invoke(item);
                    ClearSlot(slot);
                }
            }
        }

        protected virtual void ShowTooltip(IItem item)
        {
            if (tooltipPanel == null || item == null) return;

            tooltipPanel.SetActive(true);

            if (tooltipNameText != null)
                tooltipNameText.text = item.Name;

            if (tooltipStatsText != null)
            {
                // Build stat text (override to customize)
                tooltipStatsText.text = item.Description;
            }
        }

        protected virtual void HideTooltip()
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Equipment slot types.
    /// </summary>
    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        MainHand,
        OffHand,
        Accessory1,
        Accessory2
    }

    /// <summary>
    /// UI component for individual equipment slot.
    /// Attach to slot GameObject in scene.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour
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
