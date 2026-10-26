namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Represents a rank within a guild hierarchy.
    /// Defines permissions (what members can do) and privileges (benefits members receive).
    /// </summary>
    public interface IGuildRank
    {
        /// <summary>
        /// Type/level of this rank.
        /// </summary>
        GuildRankType RankType { get; }

        /// <summary>
        /// Display name of the rank (e.g., "Recruit", "Veteran", "Officer").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Order/hierarchy level (0 = lowest, higher = more authority).
        /// </summary>
        int HierarchyLevel { get; }

        /// <summary>
        /// Permissions granted to members with this rank.
        /// </summary>
        GuildPermission Permissions { get; }

        /// <summary>
        /// Privileges granted to members with this rank.
        /// </summary>
        GuildPrivilege Privileges { get; }

        /// <summary>
        /// Check if this rank has a specific permission.
        /// </summary>
        /// <param name="permission">Permission to check.</param>
        /// <returns>True if rank has the permission.</returns>
        bool HasPermission(GuildPermission permission);

        /// <summary>
        /// Check if this rank has a specific privilege.
        /// </summary>
        /// <param name="privilege">Privilege to check.</param>
        /// <returns>True if rank has the privilege.</returns>
        bool HasPrivilege(GuildPrivilege privilege);

        /// <summary>
        /// Check if this rank can perform actions on another rank.
        /// Generally, higher ranks can act on lower ranks.
        /// </summary>
        /// <param name="targetRank">Rank to compare against.</param>
        /// <returns>True if this rank has authority over target rank.</returns>
        bool HasAuthorityOver(IGuildRank targetRank);
    }
}
