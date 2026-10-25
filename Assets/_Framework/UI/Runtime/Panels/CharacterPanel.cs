using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using RPGFramework.Characters;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic character panel for displaying character stats, level, and XP.
    /// Keyboard shortcut: C
    /// </summary>
    public class CharacterPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;

        [Header("Character Info")]
        [SerializeField] protected TextMeshProUGUI characterNameText;
        [SerializeField] protected TextMeshProUGUI characterLevelText;
        [SerializeField] protected TextMeshProUGUI characterClassText;

        [Header("Experience")]
        [SerializeField] protected Slider experienceSlider;
        [SerializeField] protected TextMeshProUGUI experienceText;

        [Header("Attributes")]
        [SerializeField] protected TextMeshProUGUI healthText;
        [SerializeField] protected TextMeshProUGUI manaText;
        [SerializeField] protected TextMeshProUGUI strengthText;
        [SerializeField] protected TextMeshProUGUI agilityText;
        [SerializeField] protected TextMeshProUGUI intelligenceText;
        [SerializeField] protected TextMeshProUGUI vitalityText;

        [Header("Combat Stats")]
        [SerializeField] protected TextMeshProUGUI attackPowerText;
        [SerializeField] protected TextMeshProUGUI defenseText;
        [SerializeField] protected TextMeshProUGUI critChanceText;
        [SerializeField] protected TextMeshProUGUI critDamageText;

        [Header("Character Model")]
        [SerializeField] protected Transform characterModelPosition;

        // Events
        public event Action OnCharacterPanelOpened;
        public event Action OnCharacterPanelClosed;

        protected ICharacter character;

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);
        }

        /// <summary>
        /// Initializes panel with character data.
        /// </summary>
        public virtual void Initialize(ICharacter character)
        {
            this.character = character;
            RefreshDisplay();
        }

        /// <summary>
        /// Refreshes all character information display.
        /// </summary>
        public virtual void RefreshDisplay()
        {
            if (character == null) return;

            UpdateCharacterInfo();
            UpdateExperience();
            UpdateAttributes();
            UpdateCombatStats();
        }

        protected virtual void UpdateCharacterInfo()
        {
            if (characterNameText != null)
                characterNameText.text = character.Name;

            if (characterLevelText != null)
                characterLevelText.text = $"Level {character.Level}";

            // Override to set class text from character data
            if (characterClassText != null)
                characterClassText.text = "Adventurer";
        }

        protected virtual void UpdateExperience()
        {
            if (character.Progression == null) return;

            int currentXP = character.Progression.CurrentExperience;
            int requiredXP = character.Progression.ExperienceToNextLevel;

            if (experienceSlider != null)
            {
                experienceSlider.maxValue = requiredXP;
                experienceSlider.value = currentXP;
            }

            if (experienceText != null)
            {
                experienceText.text = $"{currentXP} / {requiredXP} XP";
            }
        }

        protected virtual void UpdateAttributes()
        {
            if (character.Stats == null) return;

            if (healthText != null)
                healthText.text = $"{character.Stats.CurrentHealth:F0} / {character.Stats.MaxHealth:F0}";

            if (manaText != null)
                manaText.text = $"{character.Stats.CurrentMana:F0} / {character.Stats.MaxMana:F0}";

            if (strengthText != null)
                strengthText.text = character.Stats.Strength.ToString("F0");

            if (agilityText != null)
                agilityText.text = character.Stats.Agility.ToString("F0");

            if (intelligenceText != null)
                intelligenceText.text = character.Stats.Intelligence.ToString("F0");

            if (vitalityText != null)
                vitalityText.text = character.Stats.Vitality.ToString("F0");
        }

        protected virtual void UpdateCombatStats()
        {
            if (character.Stats == null) return;

            if (attackPowerText != null)
                attackPowerText.text = character.Stats.AttackPower.ToString("F0");

            if (defenseText != null)
                defenseText.text = character.Stats.Defense.ToString("F0");

            if (critChanceText != null)
                critChanceText.text = $"{(character.Stats.CriticalChance * 100):F1}%";

            if (critDamageText != null)
                critDamageText.text = $"{(character.Stats.CriticalDamage * 100):F0}%";
        }

        protected virtual void ClosePanel()
        {
            OnCharacterPanelClosed?.Invoke();
            Hide();
        }

        public override void Show()
        {
            base.Show();
            RefreshDisplay();
            OnCharacterPanelOpened?.Invoke();
        }
    }
}
