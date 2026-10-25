namespace RPGFramework.Housing.Runtime.Data
{
    /// <summary>
    /// Defines how furniture can be placed within a house.
    /// </summary>
    public enum PlacementMode
    {
        /// <summary>
        /// Must be placed on the floor surface.
        /// </summary>
        Floor = 0,

        /// <summary>
        /// Must be attached to a wall.
        /// </summary>
        Wall = 1,

        /// <summary>
        /// Must be attached to the ceiling.
        /// </summary>
        Ceiling = 2,

        /// <summary>
        /// Can be placed anywhere in 3D space.
        /// </summary>
        Freestanding = 3
    }
}
