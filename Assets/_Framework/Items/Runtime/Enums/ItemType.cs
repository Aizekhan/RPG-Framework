namespace RPGFramework.Items
{
    /// <summary>
    /// Primary category of an item.
    /// Determines core behavior and usage patterns.
    /// </summary>
    public enum ItemType
    {
        /// <summary>
        /// Equippable gear (weapons, armor, accessories).
        /// Can be equipped in equipment slots to provide bonuses.
        /// </summary>
        Equipment = 0,

        /// <summary>
        /// Single-use items (potions, food, scrolls).
        /// Consumed when used to apply effects.
        /// </summary>
        Consumable = 1,

        /// <summary>
        /// Quest-related items (not tradeable/droppable).
        /// Used for quest progression and storylines.
        /// </summary>
        Quest = 2,

        /// <summary>
        /// Crafting materials and resources.
        /// Used in crafting systems to create other items.
        /// </summary>
        Crafting = 3,

        /// <summary>
        /// Currency items (gold, tokens, gems).
        /// Used as medium of exchange.
        /// </summary>
        Currency = 4,

        /// <summary>
        /// Miscellaneous items that don't fit other categories.
        /// Includes junk, keys, trophies, etc.
        /// </summary>
        Miscellaneous = 5
    }
}
