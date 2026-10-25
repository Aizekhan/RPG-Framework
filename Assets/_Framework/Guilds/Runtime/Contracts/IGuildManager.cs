using System.Collections.Generic;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Main manager for the Guild system.
    /// Handles guild creation, membership, rank management, and lifecycle.
    /// </summary>
    public interface IGuildManager
    {
        /// <summary>
        /// Create a new guild.
        /// </summary>
        /// <param name="guildId">Unique guild identifier.</param>
        /// <param name="name">Guild name.</param>
        /// <param name="tag">Guild tag/abbreviation.</param>
        /// <param name="leaderId">Entity ID of the guild leader.</param>
        /// <param name="description">Guild description.</param>
        /// <param name="maxMembers">Maximum number of members allowed (default 100).</param>
        /// <returns>The created guild.</returns>
        IGuild CreateGuild(string guildId, string name, string tag, int leaderId, string description = "", int maxMembers = 100);

        /// <summary>
        /// Disband a guild, removing all members and releasing resources.
        /// </summary>
        /// <param name="guildId">Guild to disband.</param>
        void DisbandGuild(string guildId);

        /// <summary>
        /// Get a guild by ID.
        /// </summary>
        /// <param name="guildId">Guild identifier.</param>
        /// <returns>The guild, or null if not found.</returns>
        IGuild GetGuild(string guildId);

        /// <summary>
        /// Get the guild an entity is a member of.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <returns>The guild, or null if entity is not in a guild.</returns>
        IGuild GetGuildForEntity(int entityId);

        /// <summary>
        /// Get all registered guilds.
        /// </summary>
        /// <returns>Read-only collection of all guilds.</returns>
        IReadOnlyList<IGuild> GetAllGuilds();

        /// <summary>
        /// Add a member to a guild.
        /// </summary>
        /// <param name="guildId">Guild to join.</param>
        /// <param name="entityId">Entity joining the guild.</param>
        /// <param name="rank">Initial rank (defaults to Initiate).</param>
        /// <returns>True if member was added successfully.</returns>
        bool AddMember(string guildId, int entityId, GuildRankType rank = GuildRankType.Initiate);

        /// <summary>
        /// Remove a member from a guild.
        /// </summary>
        /// <param name="guildId">Guild to leave.</param>
        /// <param name="entityId">Entity leaving the guild.</param>
        /// <returns>True if member was removed successfully.</returns>
        bool RemoveMember(string guildId, int entityId);

        /// <summary>
        /// Promote a member to a higher rank.
        /// </summary>
        /// <param name="guildId">Guild ID.</param>
        /// <param name="entityId">Member to promote.</param>
        /// <param name="newRank">New rank.</param>
        /// <returns>True if promotion was successful.</returns>
        bool PromoteMember(string guildId, int entityId, GuildRankType newRank);

        /// <summary>
        /// Demote a member to a lower rank.
        /// </summary>
        /// <param name="guildId">Guild ID.</param>
        /// <param name="entityId">Member to demote.</param>
        /// <param name="newRank">New rank.</param>
        /// <returns>True if demotion was successful.</returns>
        bool DemoteMember(string guildId, int entityId, GuildRankType newRank);

        /// <summary>
        /// Transfer guild leadership to another member.
        /// </summary>
        /// <param name="guildId">Guild ID.</param>
        /// <param name="newLeaderId">Entity ID of the new leader.</param>
        /// <returns>True if transfer was successful.</returns>
        bool TransferLeadership(string guildId, int newLeaderId);

        /// <summary>
        /// Check if an entity can perform an action based on their guild rank and permissions.
        /// </summary>
        /// <param name="entityId">Entity attempting the action.</param>
        /// <param name="permission">Required permission.</param>
        /// <returns>True if entity has the permission.</returns>
        bool HasPermission(int entityId, GuildPermission permission);

        /// <summary>
        /// Check if an entity has a specific guild privilege.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <param name="privilege">Required privilege.</param>
        /// <returns>True if entity has the privilege.</returns>
        bool HasPrivilege(int entityId, GuildPrivilege privilege);

        /// <summary>
        /// Get the guild member data for an entity.
        /// </summary>
        /// <param name="entityId">Entity ID.</param>
        /// <returns>Guild member, or null if entity is not in a guild.</returns>
        IGuildMember GetMember(int entityId);

        /// <summary>
        /// Check if an entity is in any guild.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <returns>True if entity is a guild member.</returns>
        bool IsInGuild(int entityId);
    }
}
