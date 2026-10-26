using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using RPGFramework.Characters;

namespace RPGFramework.UI.Panels
{
    /// <summary>
    /// Generic stats panel for displaying detailed character statistics.
    /// Shows all stats, resistances, and derived combat values.
    /// </summary>
    public class StatsPanel : UIViewBase
    {
        [Header("Panel References")]
        [SerializeField] protected Button closeButton;

        [Header("Primary Attributes")]
        [SerializeField] protected TextMeshProUGUI strengthValueText;
        [SerializeField] protected TextMeshProUGUI agilityValueText;
        [SerializeField] protected TextMeshProUGUI intelligenceValueText;
        [SerializeField] protected TextMeshProUGUI vitalityValueText;

        [Header("Combat Stats")]
        [SerializeField] protected TextMeshProUGUI attackPowerText;
        [SerializeField] protected TextMeshProUGUI spellPowerText;
        [SerializeField] protected TextMeshProUGUI defenseText;
        [SerializeField] protected TextMeshProUGUI armorText;
        [SerializeField] protected TextMeshProUGUI criticalChanceText;
        [SerializeField] protected TextMeshProUGUI criticalDamageText;
        [SerializeField] protected TextMeshProUGUI attackSpeedText;
        [SerializeField] protected TextMeshProUGUI movementSpeedText;

        [Header("Resistances")]
        [SerializeField] protected TextMeshProUGUI physicalResistText;
        [SerializeField] protected TextMeshProUGUI fireResistText;
        [SerializeField] protected TextMeshProUGUI iceResistText;
        [SerializeField] protected TextMeshProUGUI lightningResistText;
        [SerializeField] protected TextMeshProUGUI poisonResistText;

        [Header("Regeneration")]
        [SerializeField] protected TextMeshProUGUI healthRegenText;
        [SerializeField] protected TextMeshProUGUI manaRegenText;

        [Header("Equipment Bonuses")]
        [SerializeField] protected TextMeshProUGUI equipmentBonusText;

        protected ICharacter character;

        protected override void Awake()
        {
            base.Awake();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
        }

        /// <summary>
        /// Initializes panel with character data.
        /// </summary>
        public virtual void Initialize(ICharacter character)
        {
            this.character = character;
            RefreshStats();
        }

        /// <summary>
        /// Refreshes all stat displays.
        /// </summary>
        public virtual void RefreshStats()
        {
            if (character == null || character.Stats == null) return;

            UpdatePrimaryAttributes();
            UpdateCombatStats();
            UpdateResistances();
            UpdateRegeneration();
        }

        /// <summary>
        /// Updates primary attributes display.
        /// Game layer overrides to access concrete Stats type.
        /// </summary>
        protected virtual void UpdatePrimaryAttributes()
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

        /// <summary>
        /// Updates resistances display.
        /// Game layer overrides if resistances are available.
        /// </summary>
        protected virtual void UpdateResistances()
        {
            // Game layer implements with concrete stats type
        }

        /// <summary>
        /// Updates regeneration display.
        /// Game layer overrides to access concrete Stats type.
        /// </summary>
        protected virtual void UpdateRegeneration()
        {
            // Game layer implements with concrete stats type
        }

        public override void Show()
        {
            base.Show();
            RefreshStats();
        }
    }
}
