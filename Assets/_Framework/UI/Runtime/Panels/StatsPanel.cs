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

        protected virtual void UpdatePrimaryAttributes()
        {
            var stats = character.Stats;

            if (strengthValueText != null)
                strengthValueText.text = stats.Strength.ToString("F0");

            if (agilityValueText != null)
                agilityValueText.text = stats.Agility.ToString("F0");

            if (intelligenceValueText != null)
                intelligenceValueText.text = stats.Intelligence.ToString("F0");

            if (vitalityValueText != null)
                vitalityValueText.text = stats.Vitality.ToString("F0");
        }

        protected virtual void UpdateCombatStats()
        {
            var stats = character.Stats;

            if (attackPowerText != null)
                attackPowerText.text = stats.AttackPower.ToString("F0");

            if (spellPowerText != null)
                spellPowerText.text = stats.SpellPower.ToString("F0");

            if (defenseText != null)
                defenseText.text = stats.Defense.ToString("F0");

            if (armorText != null)
                armorText.text = stats.Armor.ToString("F0");

            if (criticalChanceText != null)
                criticalChanceText.text = $"{(stats.CriticalChance * 100):F1}%";

            if (criticalDamageText != null)
                criticalDamageText.text = $"{(stats.CriticalDamage * 100):F0}%";

            if (attackSpeedText != null)
                attackSpeedText.text = stats.AttackSpeed.ToString("F2");

            if (movementSpeedText != null)
                movementSpeedText.text = stats.MovementSpeed.ToString("F1");
        }

        protected virtual void UpdateResistances()
        {
            // Override to implement resistances if available in stats
            if (physicalResistText != null)
                physicalResistText.text = "0%";

            if (fireResistText != null)
                fireResistText.text = "0%";

            if (iceResistText != null)
                iceResistText.text = "0%";

            if (lightningResistText != null)
                lightningResistText.text = "0%";

            if (poisonResistText != null)
                poisonResistText.text = "0%";
        }

        protected virtual void UpdateRegeneration()
        {
            var stats = character.Stats;

            if (healthRegenText != null)
                healthRegenText.text = $"{stats.HealthRegeneration:F1}/sec";

            if (manaRegenText != null)
                manaRegenText.text = $"{stats.ManaRegeneration:F1}/sec";
        }

        public override void Show()
        {
            base.Show();
            RefreshStats();
        }
    }
}
