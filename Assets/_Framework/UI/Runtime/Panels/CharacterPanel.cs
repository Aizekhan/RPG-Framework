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

        /// <summary>
        /// Updates experience display.
        /// Game layer overrides to access concrete Progression type.
        /// </summary>
        protected virtual void UpdateExperience()
        {
            // Game layer implements with concrete progression type
        }

        /// <summary>
        /// Updates attribute displays (health, mana, stats).
        /// Game layer overrides to access concrete Stats type.
        /// </summary>
        protected virtual void UpdateAttributes()
        {
            // Game layer implements with concrete stats type
        }

        /// <summary>
        /// Updates combat stats display.
        /// Game layer overrides to access concrete Stats type.
        /// </summary>
        protected virtual void UpdateCombatStats()
        {
            // Game layer implements with concrete stats type
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
