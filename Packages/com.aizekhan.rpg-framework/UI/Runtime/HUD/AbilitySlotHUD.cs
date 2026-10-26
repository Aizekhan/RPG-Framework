using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace RPGFramework.UI.HUD
{
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
