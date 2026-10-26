namespace RPGFramework.Loot
{
    /// <summary>
    /// Represents a single generated item drop.
    /// Contains item ID, quantity, and source entry information.
    /// </summary>
    public struct LootDrop
    {
        /// <summary>
        /// Item definition ID.
        /// Links to item registry for full item details.
        /// </summary>
        public string ItemId;

        /// <summary>
        /// Quantity dropped.
        /// Determined by loot entry's min/max quantity range.
        /// </summary>
        public int Quantity;

        /// <summary>
        /// Source loot entry that generated this drop.
        /// Null for guaranteed drops.
        /// </summary>
        public ILootEntry SourceEntry;
    }
}
