using UnityEngine;
using RPGFramework.Stats;

namespace ReferenceGame.Stats
{
    /// <summary>
    /// Game-specific stats implementation for Reference Game.
    /// Extends Framework's IStats marker interface with concrete stat properties.
    /// </summary>
    public class PlayerStats : IStats
    {
        // Health
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }

        // Mana
        public float CurrentMana { get; private set; }
        public float MaxMana { get; private set; }

        // Primary Attributes
        public int Strength { get; private set; }
        public int Agility { get; private set; }
        public int Intelligence { get; private set; }
        public int Vitality { get; private set; }

        // Combat Stats (derived)
        public float AttackPower => Strength * 2f;
        public float SpellPower => Intelligence * 2f;
        public float Defense => Agility * 1.5f;
        public float Armor => Vitality * 1.5f;

        public PlayerStats(int level = 1)
        {
            // Initialize stats based on level
            int baseVitality = 10 + (level - 1) * 2;
            int baseStrength = 10 + (level - 1) * 2;
            int baseAgility = 10 + (level - 1) * 2;
            int baseIntelligence = 10 + (level - 1) * 2;

            Vitality = baseVitality;
            Strength = baseStrength;
            Agility = baseAgility;
            Intelligence = baseIntelligence;

            MaxHealth = baseVitality * 10f;
            CurrentHealth = MaxHealth;

            MaxMana = baseIntelligence * 5f;
            CurrentMana = MaxMana;
        }

        public void TakeDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        }

        public void Heal(float amount)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        public void ConsumeMana(float amount)
        {
            CurrentMana = Mathf.Max(0, CurrentMana - amount);
        }

        public void RestoreMana(float amount)
        {
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        }

        public bool IsAlive => CurrentHealth > 0;
    }
}
