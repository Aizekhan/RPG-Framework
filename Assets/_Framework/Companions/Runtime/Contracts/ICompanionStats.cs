namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Manages companion statistics and attributes.
    /// </summary>
    public interface ICompanionStats
    {
        /// <summary>
        /// Current health points.
        /// </summary>
        int CurrentHealth { get; }

        /// <summary>
        /// Maximum health points.
        /// </summary>
        int MaxHealth { get; }

        /// <summary>
        /// Attack/damage value.
        /// </summary>
        int Attack { get; }

        /// <summary>
        /// Defense/armor value.
        /// </summary>
        int Defense { get; }

        /// <summary>
        /// Movement speed.
        /// </summary>
        float Speed { get; }

        /// <summary>
        /// Current hunger level (0-100, where 100 is full).
        /// </summary>
        int Hunger { get; }

        /// <summary>
        /// Current happiness level (0-100).
        /// </summary>
        int Happiness { get; }

        /// <summary>
        /// Current experience points.
        /// </summary>
        int Experience { get; }

        /// <summary>
        /// Experience required for next level.
        /// </summary>
        int ExperienceToNextLevel { get; }

        /// <summary>
        /// Applies damage to the companion.
        /// </summary>
        void TakeDamage(int amount);

        /// <summary>
        /// Heals the companion.
        /// </summary>
        void Heal(int amount);

        /// <summary>
        /// Adds experience points and checks for level up.
        /// </summary>
        void GainExperience(int amount);

        /// <summary>
        /// Modifies hunger level.
        /// </summary>
        void ModifyHunger(int amount);

        /// <summary>
        /// Modifies happiness level.
        /// </summary>
        void ModifyHappiness(int amount);

        /// <summary>
        /// Regenerates stats over time (hunger decrease, health regen if fed).
        /// </summary>
        void RegenerateStats(float deltaTime);
    }
}
