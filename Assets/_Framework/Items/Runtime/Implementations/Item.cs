namespace RPGFramework.Items
{
    /// <summary>
    /// Default implementation of IItem.
    /// Immutable item definition that serves as template for all item instances.
    /// </summary>
    public class Item : IItem
    {
        public string ItemId { get; }
        public string Name { get; }
        public string Description { get; }
        public ItemType ItemType { get; }
        public ItemRarity Rarity { get; }
        public ItemQuality Quality { get; }
        public int MaxStackSize { get; }
        public int ItemLevel { get; }
        public string IconId { get; }

        /// <summary>
        /// Create a new item definition.
        /// </summary>
        /// <param name="itemId">Unique item identifier</param>
        /// <param name="name">Display name</param>
        /// <param name="description">Tooltip description</param>
        /// <param name="itemType">Item category</param>
        /// <param name="rarity">Rarity tier (default: Common)</param>
        /// <param name="quality">Quality tier (default: Normal)</param>
        /// <param name="maxStackSize">Maximum stack size (default: 1 = not stackable)</param>
        /// <param name="itemLevel">Item level (default: 1)</param>
        /// <param name="iconId">Icon identifier (default: empty)</param>
        public Item(
            string itemId,
            string name,
            string description,
            ItemType itemType,
            ItemRarity rarity = ItemRarity.Common,
            ItemQuality quality = ItemQuality.Normal,
            int maxStackSize = 1,
            int itemLevel = 1,
            string iconId = "")
        {
            ItemId = itemId;
            Name = name;
            Description = description;
            ItemType = itemType;
            Rarity = rarity;
            Quality = quality;
            MaxStackSize = maxStackSize;
            ItemLevel = itemLevel;
            IconId = iconId;
        }
    }
}
