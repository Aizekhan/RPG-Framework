namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Standard guild rank hierarchy.
    /// Higher values indicate higher rank/authority.
    /// </summary>
    public enum GuildRankType
    {
        /// <summary>
        /// New member with minimal permissions (rank 0).
        /// </summary>
        Initiate = 0,

        /// <summary>
        /// Regular member with basic permissions (rank 1).
        /// </summary>
        Member = 1,

        /// <summary>
        /// Trusted member with moderate permissions (rank 2).
        /// Can help with recruitment and basic management.
        /// </summary>
        Officer = 2,

        /// <summary>
        /// Senior member with high permissions (rank 3).
        /// Can manage most guild operations.
        /// </summary>
        Elder = 3,

        /// <summary>
        /// Guild leader with full permissions (rank 4).
        /// Ultimate authority over the guild.
        /// </summary>
        Leader = 4
    }
}
