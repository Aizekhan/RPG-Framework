namespace RPGFramework.Housing.Runtime.Data
{
    /// <summary>
    /// Access permission levels for house guests.
    /// </summary>
    public enum PermissionLevel
    {
        /// <summary>
        /// Full ownership rights.
        /// </summary>
        Owner = 5,

        /// <summary>
        /// Can modify furniture and manage guests.
        /// </summary>
        CoOwner = 4,

        /// <summary>
        /// Can access storage and use crafting stations.
        /// </summary>
        Resident = 3,

        /// <summary>
        /// Can enter and view only.
        /// </summary>
        Visitor = 2,

        /// <summary>
        /// Explicitly denied access.
        /// </summary>
        Banned = 0
    }
}
