namespace RPGFramework.Quests
{
    /// <summary>
    /// Type of quest reward.
    /// </summary>
    public enum RewardType
    {
        /// <summary>
        /// Item reward (equipment, consumable, etc.).
        /// </summary>
        Item,

        /// <summary>
        /// Experience points reward.
        /// </summary>
        Experience,

        /// <summary>
        /// Currency reward (gold, coins, etc.).
        /// </summary>
        Currency,

        /// <summary>
        /// Unlock reward (skill, ability, area, etc.).
        /// </summary>
        Unlock,

        /// <summary>
        /// Custom reward type (game-specific).
        /// </summary>
        Custom
    }
}
