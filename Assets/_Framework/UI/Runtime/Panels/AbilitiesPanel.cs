using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic abilities panel for displaying and managing character abilities.
    /// Shows ability cooldowns, mana costs, and allows ability slot assignment.
    /// Keyboard shortcut: K
    /// </summary>
    public class AbilitiesPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;

        [Header("Ability List")]
        [SerializeField] protected Transform abilitiesContainer;
        [SerializeField] protected GameObject abilityEntryPrefab;

        [Header("Ability Slots")]
        [SerializeField] protected AbilitySlotUI slot1;
        [SerializeField] protected AbilitySlotUI slot2;
        [SerializeField] protected AbilitySlotUI slot3;
        [SerializeField] protected AbilitySlotUI slot4;
        [SerializeField] protected AbilitySlotUI slot5;
        [SerializeField] protected AbilitySlotUI slot6;

        [Header("Ability Details")]
        [SerializeField] protected TextMeshProUGUI abilityNameText;
        [SerializeField] protected TextMeshProUGUI abilityDescriptionText;
        [SerializeField] protected TextMeshProUGUI abilityCostText;
        [SerializeField] protected TextMeshProUGUI abilityCooldownText;
        [SerializeField] protected TextMeshProUGUI abilityDamageText;

        // Events
        public event Action<string, int> OnAbilityAssigned; // abilityId, slotIndex
        public event Action<int> OnAbilityUnassigned; // slotIndex
        public event Action<string> OnAbilitySelected; // abilityId

        protected List<GameObject> abilityEntries = new List<GameObject>();
        protected AbilitySlotUI[] abilitySlots;
        protected string selectedAbilityId;

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            InitializeAbilitySlots();
        }

        protected virtual void InitializeAbilitySlots()
        {
            abilitySlots = new AbilitySlotUI[6];

            if (slot1 != null) RegisterSlot(0, slot1);
            if (slot2 != null) RegisterSlot(1, slot2);
            if (slot3 != null) RegisterSlot(2, slot3);
            if (slot4 != null) RegisterSlot(3, slot4);
            if (slot5 != null) RegisterSlot(4, slot5);
            if (slot6 != null) RegisterSlot(5, slot6);
        }

        protected virtual void RegisterSlot(int index, AbilitySlotUI slotUI)
        {
            abilitySlots[index] = slotUI;

            if (slotUI != null)
            {
                slotUI.OnSlotClicked += () => OnSlotClicked(index);
            }
        }

        /// <summary>
        /// Populates ability list with available abilities.
        /// </summary>
        public virtual void PopulateAbilities(List<AbilityData> abilities)
        {
            ClearAbilityList();

            if (abilitiesContainer == null || abilityEntryPrefab == null || abilities == null) return;

            foreach (var ability in abilities)
            {
                var entry = Instantiate(abilityEntryPrefab, abilitiesContainer);
                abilityEntries.Add(entry);
                SetupAbilityEntry(entry, ability);
            }
        }

        protected virtual void SetupAbilityEntry(GameObject entry, AbilityData ability)
        {
            var text = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"{ability.abilityName}\nMP: {ability.manaCost}";
            }

            var button = entry.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => SelectAbility(ability));
            }
        }

        protected virtual void SelectAbility(AbilityData ability)
        {
            selectedAbilityId = ability.abilityId;

            if (abilityNameText != null)
                abilityNameText.text = ability.abilityName;

            if (abilityDescriptionText != null)
                abilityDescriptionText.text = ability.description;

            if (abilityCostText != null)
                abilityCostText.text = $"Mana Cost: {ability.manaCost}";

            if (abilityCooldownText != null)
                abilityCooldownText.text = $"Cooldown: {ability.cooldown}s";

            if (abilityDamageText != null)
            {
                if (ability.damage > 0)
                    abilityDamageText.text = $"Damage: {ability.damage}";
                else
                    abilityDamageText.text = "";
            }

            OnAbilitySelected?.Invoke(ability.abilityId);
        }

        /// <summary>
        /// Assigns ability to specific slot.
        /// </summary>
        public virtual void AssignAbilityToSlot(string abilityId, int slotIndex, AbilityData abilityData)
        {
            if (slotIndex < 0 || slotIndex >= abilitySlots.Length) return;

            var slot = abilitySlots[slotIndex];
            if (slot != null)
            {
                slot.SetAbility(abilityId, abilityData);
            }
        }

        /// <summary>
        /// Clears ability from specific slot.
        /// </summary>
        public virtual void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= abilitySlots.Length) return;

            var slot = abilitySlots[slotIndex];
            if (slot != null)
            {
                slot.ClearSlot();
            }
        }

        protected virtual void OnSlotClicked(int slotIndex)
        {
            if (!string.IsNullOrEmpty(selectedAbilityId))
            {
                // Assign selected ability to clicked slot
                OnAbilityAssigned?.Invoke(selectedAbilityId, slotIndex);
            }
            else
            {
                // Unassign ability from slot
                OnAbilityUnassigned?.Invoke(slotIndex);
            }
        }

        protected virtual void ClearAbilityList()
        {
            foreach (var entry in abilityEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            abilityEntries.Clear();
        }
    }

    /// <summary>
    /// Data structure for ability information.
    /// </summary>
    [Serializable]
    public class AbilityData
    {
        public string abilityId;
        public string abilityName;
        public string description;
        public int manaCost;
        public float cooldown;
        public float damage;
        public Sprite icon;
    }

    /// <summary>
    /// UI component for individual ability slot.
    /// </summary>
    public class AbilitySlotUI : MonoBehaviour
    {
        [SerializeField] private Image abilityIcon;
        [SerializeField] private TextMeshProUGUI keybindText;
        [SerializeField] private GameObject emptyIndicator;
        [SerializeField] private Button slotButton;

        public string AssignedAbilityId { get; private set; }
        public AbilityData AbilityData { get; private set; }

        public event Action OnSlotClicked;

        private void Awake()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke());
        }

        public void SetAbility(string abilityId, AbilityData abilityData)
        {
            AssignedAbilityId = abilityId;
            AbilityData = abilityData;

            if (abilityIcon != null && abilityData.icon != null)
            {
                abilityIcon.sprite = abilityData.icon;
                abilityIcon.enabled = true;
            }

            if (emptyIndicator != null)
                emptyIndicator.SetActive(false);
        }

        public void ClearSlot()
        {
            AssignedAbilityId = null;
            AbilityData = null;

            if (abilityIcon != null)
                abilityIcon.enabled = false;

            if (emptyIndicator != null)
                emptyIndicator.SetActive(true);
        }

        public void SetKeybind(string keybind)
        {
            if (keybindText != null)
                keybindText.text = keybind;
        }
    }
}
