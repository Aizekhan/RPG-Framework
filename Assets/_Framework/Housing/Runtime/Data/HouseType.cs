namespace RPGFramework.Housing.Runtime.Data
{
    /// <summary>
    /// Types of houses available for construction and ownership.
    /// </summary>
    public enum HouseType
    {
        /// <summary>
        /// Small single-room dwelling in a city building.
        /// </summary>
        Apartment = 0,

        /// <summary>
        /// Small standalone house with basic amenities.
        /// </summary>
        Cottage = 1,

        /// <summary>
        /// Medium-sized house with multiple rooms.
        /// </summary>
        Manor = 2,

        /// <summary>
        /// Large fortified residence with extensive rooms.
        /// </summary>
        Castle = 3,

        /// <summary>
        /// Shared large building for guild organizations.
        /// </summary>
        GuildHall = 4
    }
}
