namespace RPGFramework.Housing.Runtime.Data
{
    /// <summary>
    /// Categories of furniture items for organization and filtering.
    /// </summary>
    public enum FurnitureCategory
    {
        /// <summary>
        /// Purely decorative items with no functional purpose.
        /// </summary>
        Decoration = 0,

        /// <summary>
        /// Basic furniture (chairs, tables, beds).
        /// </summary>
        Furniture = 1,

        /// <summary>
        /// Storage containers (chests, wardrobes, cabinets).
        /// </summary>
        Storage = 2,

        /// <summary>
        /// Crafting stations (forge, alchemy table, enchanting table).
        /// </summary>
        Crafting = 3,

        /// <summary>
        /// Functional items (lighting, doors, windows).
        /// </summary>
        Functional = 4,

        /// <summary>
        /// Trophy displays and achievement showcases.
        /// </summary>
        Trophy = 5
    }
}
