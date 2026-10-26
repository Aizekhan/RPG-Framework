using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Default implementation of ICompanionStats.
    /// </summary>
    public class CompanionStats : ICompanionStats
    {
        private CompanionStatsData _data;

        public int CurrentHealth => _data.CurrentHealth;
        public int MaxHealth => _data.MaxHealth;
        public int Attack => _data.Attack;
        public int Defense => _data.Defense;
        public float Speed => _data.Speed;
        public int Hunger => _data.Hunger;
        public int Happiness => _data.Happiness;
        public int Experience => _data.Experience;
        public int ExperienceToNextLevel => _data.ExperienceToNextLevel;

        public CompanionStats(int maxHealth, int attack, int defense, float speed)
        {
            _data = new CompanionStatsData
            {
                MaxHealth = maxHealth,
                CurrentHealth = maxHealth,
                Attack = attack,
                Defense = defense,
                Speed = speed,
                Hunger = 100, // Start full
                Happiness = 50, // Start neutral
                Experience = 0,
                ExperienceToNextLevel = 100,
                HealthRegenRate = maxHealth * 0.01f, // 1% per second when well-fed
                HungerDecayRate = 1.0f // Lose 1 hunger per second
            };
        }

        public void TakeDamage(int amount)
        {
            _data.CurrentHealth = Math.Max(0, _data.CurrentHealth - amount);
        }

        public void Heal(int amount)
        {
            _data.CurrentHealth = Math.Min(_data.MaxHealth, _data.CurrentHealth + amount);
        }

        public void GainExperience(int amount)
        {
            _data.Experience += amount;
            // Note: Level-up logic would be handled by the calling system
        }

        public void ModifyHunger(int amount)
        {
            _data.Hunger = Math.Clamp(_data.Hunger + amount, 0, 100);
        }

        public void ModifyHappiness(int amount)
        {
            _data.Happiness = Math.Clamp(_data.Happiness + amount, 0, 100);
        }

        public void RegenerateStats(float deltaTime)
        {
            // Hunger decreases over time
            _data.Hunger = Math.Max(0, _data.Hunger - (int)(_data.HungerDecayRate * deltaTime));

            // Health regeneration only when well-fed (hunger > 50)
            if (_data.Hunger > 50 && _data.CurrentHealth < _data.MaxHealth)
            {
                int regenAmount = (int)(_data.HealthRegenRate * deltaTime);
                _data.CurrentHealth = Math.Min(_data.MaxHealth, _data.CurrentHealth + regenAmount);
            }

            // Happiness decreases if starving
            if (_data.Hunger < 20)
            {
                _data.Happiness = Math.Max(0, _data.Happiness - (int)(5 * deltaTime));
            }
            // Happiness increases slowly when well-fed
            else if (_data.Hunger > 70)
            {
                _data.Happiness = Math.Min(100, _data.Happiness + (int)(2 * deltaTime));
            }
        }

        public void LevelUp(int level)
        {
            // Scale stats with level
            _data.MaxHealth += (int)(MaxHealth * 0.1f); // +10% per level
            _data.CurrentHealth = _data.MaxHealth;
            _data.Attack += (int)(Attack * 0.08f); // +8% per level
            _data.Defense += (int)(Defense * 0.08f); // +8% per level
            _data.Experience = 0;
            _data.ExperienceToNextLevel = (int)(100 * Math.Pow(1.5, level)); // Exponential scaling
        }

        public CompanionStatsData GetData() => _data;

        public void SetStats(CompanionStatsData data)
        {
            _data = data;
        }
    }
}
