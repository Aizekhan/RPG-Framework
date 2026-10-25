namespace RPGFramework.Achievements
{
    /// <summary>
    /// Represents the rarity/difficulty tier of an achievement.
    /// Higher tiers typically indicate more challenging or prestigious achievements.
    /// </summary>
    public enum AchievementTier
    {
        /// <summary>
        /// Common achievements - easy to obtain.
        /// </summary>
        Common = 0,

        /// <summary>
        /// Uncommon achievements - require some effort.
        /// </summary>
        Uncommon = 1,

        /// <summary>
        /// Rare achievements - challenging to obtain.
        /// </summary>
        Rare = 2,

        /// <summary>
        /// Epic achievements - very challenging to obtain.
        /// </summary>
        Epic = 3,

        /// <summary>
        /// Legendary achievements - extremely challenging to obtain.
        /// </summary>
        Legendary = 4,

        /// <summary>
        /// Mythic achievements - the pinnacle of difficulty and prestige.
        /// </summary>
        Mythic = 5
    }
}
