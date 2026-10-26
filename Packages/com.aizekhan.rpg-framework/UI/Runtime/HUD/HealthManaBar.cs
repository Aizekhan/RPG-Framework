using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGFramework.UI.HUD
{
    /// <summary>
    /// Generic health and mana bar component for HUD.
    /// Displays current/max values with smooth fill animations.
    /// </summary>
    public class HealthManaBar : MonoBehaviour
    {
        [Header("Health Bar")]
        [SerializeField] protected Slider healthSlider;
        [SerializeField] protected Image healthFillImage;
        [SerializeField] protected TextMeshProUGUI healthText;
        [SerializeField] protected Gradient healthGradient;

        [Header("Mana Bar")]
        [SerializeField] protected Slider manaSlider;
        [SerializeField] protected Image manaFillImage;
        [SerializeField] protected TextMeshProUGUI manaText;
        [SerializeField] protected Gradient manaGradient;

        [Header("Animation")]
        [SerializeField] protected float smoothSpeed = 5f;
        [SerializeField] protected bool useSmoothing = true;

        protected float targetHealthValue;
        protected float targetManaValue;
        protected float currentHealthValue;
        protected float currentManaValue;

        protected float maxHealth;
        protected float maxMana;

        protected virtual void Update()
        {
            if (useSmoothing)
            {
                UpdateHealthSmooth();
                UpdateManaSmooth();
            }
        }

        /// <summary>
        /// Sets maximum health value.
        /// </summary>
        public virtual void SetMaxHealth(float maxHealth)
        {
            this.maxHealth = maxHealth;

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
            }

            UpdateHealthDisplay();
        }

        /// <summary>
        /// Sets maximum mana value.
        /// </summary>
        public virtual void SetMaxMana(float maxMana)
        {
            this.maxMana = maxMana;

            if (manaSlider != null)
            {
                manaSlider.maxValue = maxMana;
            }

            UpdateManaDisplay();
        }

        /// <summary>
        /// Updates current health value.
        /// </summary>
        public virtual void SetHealth(float currentHealth)
        {
            targetHealthValue = Mathf.Clamp(currentHealth, 0, maxHealth);

            if (!useSmoothing)
            {
                currentHealthValue = targetHealthValue;
                UpdateHealthDisplay();
            }
        }

        /// <summary>
        /// Updates current mana value.
        /// </summary>
        public virtual void SetMana(float currentMana)
        {
            targetManaValue = Mathf.Clamp(currentMana, 0, maxMana);

            if (!useSmoothing)
            {
                currentManaValue = targetManaValue;
                UpdateManaDisplay();
            }
        }

        protected virtual void UpdateHealthSmooth()
        {
            if (Mathf.Abs(currentHealthValue - targetHealthValue) > 0.01f)
            {
                currentHealthValue = Mathf.Lerp(currentHealthValue, targetHealthValue, Time.deltaTime * smoothSpeed);
                UpdateHealthDisplay();
            }
        }

        protected virtual void UpdateManaSmooth()
        {
            if (Mathf.Abs(currentManaValue - targetManaValue) > 0.01f)
            {
                currentManaValue = Mathf.Lerp(currentManaValue, targetManaValue, Time.deltaTime * smoothSpeed);
                UpdateManaDisplay();
            }
        }

        protected virtual void UpdateHealthDisplay()
        {
            if (healthSlider != null)
            {
                healthSlider.value = currentHealthValue;
            }

            if (healthText != null)
            {
                healthText.text = $"{currentHealthValue:F0} / {maxHealth:F0}";
            }

            if (healthFillImage != null && healthGradient != null)
            {
                float normalizedHealth = maxHealth > 0 ? currentHealthValue / maxHealth : 0;
                healthFillImage.color = healthGradient.Evaluate(normalizedHealth);
            }
        }

        protected virtual void UpdateManaDisplay()
        {
            if (manaSlider != null)
            {
                manaSlider.value = currentManaValue;
            }

            if (manaText != null)
            {
                manaText.text = $"{currentManaValue:F0} / {maxMana:F0}";
            }

            if (manaFillImage != null && manaGradient != null)
            {
                float normalizedMana = maxMana > 0 ? currentManaValue / maxMana : 0;
                manaFillImage.color = manaGradient.Evaluate(normalizedMana);
            }
        }

        /// <summary>
        /// Initializes health and mana bars with max and current values.
        /// </summary>
        public virtual void Initialize(float maxHealth, float currentHealth, float maxMana, float currentMana)
        {
            SetMaxHealth(maxHealth);
            SetMaxMana(maxMana);

            this.currentHealthValue = currentHealth;
            this.targetHealthValue = currentHealth;
            this.currentManaValue = currentMana;
            this.targetManaValue = currentMana;

            UpdateHealthDisplay();
            UpdateManaDisplay();
        }
    }
}
