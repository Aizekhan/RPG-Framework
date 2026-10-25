using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Represents a member of a guild with rank, permissions, and contribution tracking.
    /// </summary>
    public interface IGuildMember
    {
        /// <summary>
        /// Entity ID of the member.
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// Guild ID this member belongs to.
        /// </summary>
        string GuildId { get; }

        /// <summary>
        /// Current rank within the guild.
        /// </summary>
        GuildRankType Rank { get; }

        /// <summary>
        /// Date and time when the member joined.
        /// </summary>
        DateTime JoinedAt { get; }

        /// <summary>
        /// Date and time of the member's last login/activity.
        /// </summary>
        DateTime LastActiveAt { get; }

        /// <summary>
        /// Total contribution points to the guild.
        /// Used for tracking member activity and merit.
        /// </summary>
        long ContributionPoints { get; }

        /// <summary>
        /// Personal note/title visible to other guild members.
        /// </summary>
        string Note { get; }

        /// <summary>
        /// Check if this member has a specific permission.
        /// </summary>
        /// <param name="permission">Permission to check.</param>
        /// <returns>True if member has the permission.</returns>
        bool HasPermission(GuildPermission permission);

        /// <summary>
        /// Check if this member has a specific privilege.
        /// </summary>
        /// <param name="privilege">Privilege to check.</param>
        /// <returns>True if member has the privilege.</returns>
        bool HasPrivilege(GuildPrivilege privilege);

        /// <summary>
        /// Get all permissions this member has.
        /// </summary>
        /// <returns>Combined permission flags.</returns>
        GuildPermission GetPermissions();

        /// <summary>
        /// Get all privileges this member has.
        /// </summary>
        /// <returns>Combined privilege flags.</returns>
        GuildPrivilege GetPrivileges();

        /// <summary>
        /// Add contribution points to this member.
        /// </summary>
        /// <param name="points">Amount of contribution points to add.</param>
        void AddContribution(long points);

        /// <summary>
        /// Update the member's note.
        /// </summary>
        /// <param name="note">New note text.</param>
        void SetNote(string note);

        /// <summary>
        /// Update last active timestamp to current time.
        /// </summary>
        void UpdateLastActive();
    }
}
