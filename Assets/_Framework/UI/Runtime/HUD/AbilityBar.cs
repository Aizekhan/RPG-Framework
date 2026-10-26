using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace RPGFramework.UI.HUD
{
    /// <summary>
    /// Generic ability bar for HUD.
    /// Displays ability icons, cooldowns, and keybinds (1-6 keys).
    /// </summary>
    public class AbilityBar : MonoBehaviour
    {
        [Header("Ability Slots")]
        [SerializeField] protected AbilitySlotHUD slot1;
        [SerializeField] protected AbilitySlotHUD slot2;
        [SerializeField] protected AbilitySlotHUD slot3;
        [SerializeField] protected AbilitySlotHUD slot4;
        [SerializeField] protected AbilitySlotHUD slot5;
        [SerializeField] protected AbilitySlotHUD slot6;

        protected AbilitySlotHUD[] slots;

        // Events
        public event Action<int> OnAbilitySlotClicked; // slot index

        protected virtual void Awake()
        {
            InitializeSlots();
        }

        protected virtual void InitializeSlots()
        {
            slots = new AbilitySlotHUD[6];

            if (slot1 != null) RegisterSlot(0, slot1, "1");
            if (slot2 != null) RegisterSlot(1, slot2, "2");
            if (slot3 != null) RegisterSlot(2, slot3, "3");
            if (slot4 != null) RegisterSlot(3, slot4, "4");
            if (slot5 != null) RegisterSlot(4, slot5, "5");
            if (slot6 != null) RegisterSlot(5, slot6, "6");
        }

        protected virtual void RegisterSlot(int index, AbilitySlotHUD slot, string keybind)
        {
            slots[index] = slot;
            slot.SetKeybind(keybind);
            slot.OnSlotClicked += () => OnAbilitySlotClicked?.Invoke(index);
        }

        /// <summary>
        /// Sets ability in specific slot.
        /// </summary>
        public virtual void SetAbility(int slotIndex, Sprite icon, string abilityName = "")
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;

            var slot = slots[slotIndex];
            if (slot != null)
            {
                slot.SetAbility(icon, abilityName);
            }
        }

        /// <summary>
        /// Clears ability from specific slot.
        /// </summary>
        public virtual void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;

            var slot = slots[slotIndex];
            if (slot != null)
            {
                slot.ClearSlot();
            }
        }

        /// <summary>
        /// Updates cooldown for specific slot.
        /// </summary>
        public virtual void SetCooldown(int slotIndex, float remainingCooldown, float maxCooldown)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;

            var slot = slots[slotIndex];
            if (slot != null)
            {
                slot.SetCooldown(remainingCooldown, maxCooldown);
            }
        }

        /// <summary>
        /// Sets ability availability (enabled/disabled based on mana/conditions).
        /// </summary>
        public virtual void SetAbilityAvailable(int slotIndex, bool available)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;

            var slot = slots[slotIndex];
            if (slot != null)
            {
                slot.SetAvailable(available);
            }
        }
    }
}
