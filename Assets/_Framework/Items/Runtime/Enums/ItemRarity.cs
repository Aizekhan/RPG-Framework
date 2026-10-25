namespace RPGFramework.Items
{
    /// <summary>
    /// Rarity tier of an item.
    /// Affects drop rates, visual presentation, and base power level.
    /// Higher rarity typically means better stats and more valuable.
    /// </summary>
    public enum ItemRarity
    {
        /// <summary>
        /// Common items (white/grey color, abundant drops).
        /// Basic items found frequently throughout the game.
        /// </summary>
        Common = 0,

        /// <summary>
        /// Uncommon items (green color, slightly better than common).
        /// Moderate drop rate, noticeable improvement over common.
        /// </summary>
        Uncommon = 1,

        /// <summary>
        /// Rare items (blue color, notable finds).
        /// Lower drop rate, significant power increase.
        /// </summary>
        Rare = 2,

        /// <summary>
        /// Epic items (purple color, very rare drops).
        /// Rare drops from elite enemies or special sources.
        /// </summary>
        Epic = 3,

        /// <summary>
        /// Legendary items (orange/gold color, extremely rare).
        /// Very low drop rate, exceptional power.
        /// </summary>
        Legendary = 4,

        /// <summary>
        /// Mythic items (red/pink color, unique/artifact tier).
        /// Unique items with special mechanics, often one-of-a-kind.
        /// </summary>
        Mythic = 5
    }
}
