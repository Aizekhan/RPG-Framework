namespace RPGFramework.Achievements
{
    /// <summary>
    /// Types of rewards that can be granted upon achievement unlock.
    /// </summary>
    public enum RewardType
    {
        /// <summary>
        /// Currency reward (gold, coins, etc.).
        /// </summary>
        Gold = 0,

        /// <summary>
        /// Experience points reward.
        /// </summary>
        Experience = 1,

        /// <summary>
        /// Item reward (equipment, consumables, etc.).
        /// </summary>
        Item = 2,

        /// <summary>
        /// Title or name plate reward.
        /// </summary>
        Title = 3,

        /// <summary>
        /// Cosmetic reward (skins, emotes, visual effects).
        /// </summary>
        Cosmetic = 4,

        /// <summary>
        /// Permanent stat bonus reward.
        /// </summary>
        StatBonus = 5
    }
}
