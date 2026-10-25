namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// ECS component holding companion statistics.
    /// </summary>
    public struct CompanionStatsData
    {
        /// <summary>
        /// Current health points.
        /// </summary>
        public int CurrentHealth;

        /// <summary>
        /// Maximum health points.
        /// </summary>
        public int MaxHealth;

        /// <summary>
        /// Attack/damage value.
        /// </summary>
        public int Attack;

        /// <summary>
        /// Defense/armor value.
        /// </summary>
        public int Defense;

        /// <summary>
        /// Movement speed multiplier.
        /// </summary>
        public float Speed;

        /// <summary>
        /// Current hunger level (0-100, where 100 is full/not hungry).
        /// </summary>
        public int Hunger;

        /// <summary>
        /// Current happiness level (0-100).
        /// </summary>
        public int Happiness;

        /// <summary>
        /// Current experience points.
        /// </summary>
        public int Experience;

        /// <summary>
        /// Experience required for next level.
        /// </summary>
        public int ExperienceToNextLevel;

        /// <summary>
        /// Health regeneration rate per second (when conditions are met).
        /// </summary>
        public float HealthRegenRate;

        /// <summary>
        /// Hunger decay rate per second.
        /// </summary>
        public float HungerDecayRate;
    }
}
