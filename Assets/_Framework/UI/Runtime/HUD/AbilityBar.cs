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

    /// <summary>
    /// Individual ability slot for HUD ability bar.
    /// </summary>
    public class AbilitySlotHUD : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] protected Image abilityIcon;
        [SerializeField] protected Image cooldownOverlay;
        [SerializeField] protected TextMeshProUGUI cooldownText;
        [SerializeField] protected TextMeshProUGUI keybindText;
        [SerializeField] protected GameObject emptyIndicator;
        [SerializeField] protected Button slotButton;
        [SerializeField] protected Image unavailableOverlay;

        [Header("Colors")]
        [SerializeField] protected Color availableColor = Color.white;
        [SerializeField] protected Color unavailableColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        protected float remainingCooldown;
        protected float maxCooldown;
        protected bool isOnCooldown;

        public event Action OnSlotClicked;

        protected virtual void Awake()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke());

            if (cooldownOverlay != null)
                cooldownOverlay.fillAmount = 0f;
        }

        protected virtual void Update()
        {
            if (isOnCooldown)
            {
                UpdateCooldown();
            }
        }

        public virtual void SetAbility(Sprite icon, string abilityName = "")
        {
            if (abilityIcon != null && icon != null)
            {
                abilityIcon.sprite = icon;
                abilityIcon.enabled = true;
            }

            if (emptyIndicator != null)
                emptyIndicator.SetActive(false);
        }

        public virtual void ClearSlot()
        {
            if (abilityIcon != null)
                abilityIcon.enabled = false;

            if (emptyIndicator != null)
                emptyIndicator.SetActive(true);

            if (cooldownOverlay != null)
                cooldownOverlay.fillAmount = 0f;

            if (cooldownText != null)
                cooldownText.text = "";

            isOnCooldown = false;
        }

        public virtual void SetKeybind(string keybind)
        {
            if (keybindText != null)
                keybindText.text = keybind;
        }

        public virtual void SetCooldown(float remaining, float max)
        {
            remainingCooldown = remaining;
            maxCooldown = max;
            isOnCooldown = remaining > 0;

            if (!isOnCooldown)
            {
                if (cooldownOverlay != null)
                    cooldownOverlay.fillAmount = 0f;

                if (cooldownText != null)
                    cooldownText.text = "";
            }
        }

        protected virtual void UpdateCooldown()
        {
            remainingCooldown -= Time.deltaTime;

            if (remainingCooldown <= 0)
            {
                remainingCooldown = 0;
                isOnCooldown = false;

                if (cooldownOverlay != null)
                    cooldownOverlay.fillAmount = 0f;

                if (cooldownText != null)
                    cooldownText.text = "";
            }
            else
            {
                float fillAmount = remainingCooldown / maxCooldown;

                if (cooldownOverlay != null)
                    cooldownOverlay.fillAmount = fillAmount;

                if (cooldownText != null)
                {
                    if (remainingCooldown > 1f)
                        cooldownText.text = Mathf.Ceil(remainingCooldown).ToString();
                    else
                        cooldownText.text = remainingCooldown.ToString("F1");
                }
            }
        }

        public virtual void SetAvailable(bool available)
        {
            if (abilityIcon != null)
            {
                abilityIcon.color = available ? availableColor : unavailableColor;
            }

            if (unavailableOverlay != null)
            {
                unavailableOverlay.gameObject.SetActive(!available);
            }

            if (slotButton != null)
            {
                slotButton.interactable = available && !isOnCooldown;
            }
        }
    }
}
