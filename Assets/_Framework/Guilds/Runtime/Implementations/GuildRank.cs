namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Concrete implementation of IGuildRank.
    /// Defines a rank within the guild hierarchy with associated permissions and privileges.
    /// </summary>
    public class GuildRank : IGuildRank
    {
        public GuildRankType RankType { get; private set; }
        public string Name { get; private set; }
        public int HierarchyLevel { get; private set; }
        public GuildPermission Permissions { get; private set; }
        public GuildPrivilege Privileges { get; private set; }

        /// <summary>
        /// Create a new guild rank.
        /// </summary>
        public GuildRank(
            GuildRankType rankType,
            string name,
            int hierarchyLevel,
            GuildPermission permissions,
            GuildPrivilege privileges)
        {
            RankType = rankType;
            Name = name;
            HierarchyLevel = hierarchyLevel;
            Permissions = permissions;
            Privileges = privileges;
        }

        public bool HasPermission(GuildPermission permission)
        {
            return (Permissions & permission) == permission;
        }

        public bool HasPrivilege(GuildPrivilege privilege)
        {
            return (Privileges & privilege) == privilege;
        }

        public bool HasAuthorityOver(IGuildRank targetRank)
        {
            if (targetRank == null)
                return false;

            return HierarchyLevel > targetRank.HierarchyLevel;
        }

        /// <summary>
        /// Add a permission to this rank.
        /// </summary>
        public void AddPermission(GuildPermission permission)
        {
            Permissions |= permission;
        }

        /// <summary>
        /// Remove a permission from this rank.
        /// </summary>
        public void RemovePermission(GuildPermission permission)
        {
            Permissions &= ~permission;
        }

        /// <summary>
        /// Add a privilege to this rank.
        /// </summary>
        public void AddPrivilege(GuildPrivilege privilege)
        {
            Privileges |= privilege;
        }

        /// <summary>
        /// Remove a privilege from this rank.
        /// </summary>
        public void RemovePrivilege(GuildPrivilege privilege)
        {
            Privileges &= ~privilege;
        }

        public override string ToString()
        {
            return $"GuildRank[{RankType}]: {Name} (Level {HierarchyLevel})";
        }
    }
}
