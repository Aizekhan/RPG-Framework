namespace RPGFramework.Items
{
    /// <summary>
    /// Base interface for all items.
    /// Represents the definition/template of an item, not instances.
    /// Item definitions are immutable and shared across all instances.
    /// </summary>
    public interface IItem
    {
        /// <summary>
        /// Unique identifier for this item type.
        /// Used for lookups in item registry and serialization.
        /// </summary>
        string ItemId { get; }

        /// <summary>
        /// Display name of the item.
        /// Shown in UI tooltips and inventory screens.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description text for tooltips/UI.
        /// Provides flavor text and mechanical information.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Category of this item (Equipment, Consumable, Quest, Crafting, etc.).
        /// Determines core behavior and usage patterns.
        /// </summary>
        ItemType ItemType { get; }

        /// <summary>
        /// Rarity tier (Common, Uncommon, Rare, Epic, Legendary, Mythic).
        /// Affects drop rates, visual presentation, and value.
        /// </summary>
        ItemRarity Rarity { get; }

        /// <summary>
        /// Quality tier (Normal, Magical, Rare, Unique, Set).
        /// Represents magical enhancement level.
        /// </summary>
        ItemQuality Quality { get; }

        /// <summary>
        /// Maximum stack size for this item.
        /// 1 = not stackable (equipment, unique items).
        /// Greater than 1 = stackable (consumables, materials).
        /// </summary>
        int MaxStackSize { get; }

        /// <summary>
        /// Item level/tier for requirements or scaling.
        /// Used for level restrictions and stat scaling.
        /// </summary>
        int ItemLevel { get; }

        /// <summary>
        /// Icon identifier for UI rendering.
        /// Framework stores string ID, game layer maps to actual sprite.
        /// </summary>
        string IconId { get; }
    }
}
