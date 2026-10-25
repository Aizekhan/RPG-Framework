namespace RPGFramework.Items
{
    /// <summary>
    /// Quality tier of an item (orthogonal to rarity).
    /// Represents magical enhancement or crafting quality.
    /// Independent from rarity - a Common item can be Unique quality.
    /// </summary>
    public enum ItemQuality
    {
        /// <summary>
        /// Normal quality (no magical properties).
        /// Standard items without special enhancements.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Magical quality (minor enchantments).
        /// Items with 1-2 basic magical properties.
        /// </summary>
        Magical = 1,

        /// <summary>
        /// Rare quality (multiple enchantments).
        /// Items with 3-4 magical properties and enhanced stats.
        /// </summary>
        Rare = 2,

        /// <summary>
        /// Unique quality (named items with special properties).
        /// One-of-a-kind items with unique mechanics or effects.
        /// </summary>
        Unique = 3,

        /// <summary>
        /// Set quality (part of item set with bonuses).
        /// Items that grant additional bonuses when multiple set pieces are equipped.
        /// </summary>
        Set = 4
    }
}
