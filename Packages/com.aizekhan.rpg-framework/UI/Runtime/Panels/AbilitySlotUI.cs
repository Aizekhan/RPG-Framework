using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace RPGFramework.UI.Panels
{
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
