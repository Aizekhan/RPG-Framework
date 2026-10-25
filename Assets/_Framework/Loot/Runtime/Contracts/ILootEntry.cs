namespace RPGFramework.Loot
{
    /// <summary>
    /// Represents a single entry in a loot table.
    /// Defines what item can drop, with what probability, and in what quantities.
    /// </summary>
    public interface ILootEntry
    {
        /// <summary>
        /// Item definition ID to drop.
        /// Links to item registry for full item details.
        /// </summary>
        string ItemId { get; }

        /// <summary>
        /// Drop weight for weighted random selection.
        /// Higher weight = more likely to drop relative to other entries.
        /// </summary>
        int Weight { get; }

        /// <summary>
        /// Minimum quantity to drop.
        /// Actual quantity is randomly selected between min and max.
        /// </summary>
        int MinQuantity { get; }

        /// <summary>
        /// Maximum quantity to drop.
        /// Actual quantity is randomly selected between min and max.
        /// </summary>
        int MaxQuantity { get; }

        /// <summary>
        /// Drop chance percentage (0.0 to 1.0).
        /// Even if selected by weighted roll, item still needs to pass drop chance.
        /// Example: 0.5 = 50% chance to drop after being selected.
        /// </summary>
        float DropChance { get; }

        /// <summary>
        /// Minimum player/enemy level required for this drop.
        /// 0 = no requirement, available at all levels.
        /// </summary>
        int MinLevel { get; }

        /// <summary>
        /// Maximum player/enemy level for this drop.
        /// -1 = no maximum, available at all levels.
        /// </summary>
        int MaxLevel { get; }
    }
}
