using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Flags enum for guild permissions (what members can do).
    /// Permissions control actions members can perform within the guild.
    /// </summary>
    [Flags]
    public enum GuildPermission
    {
        /// <summary>
        /// No permissions.
        /// </summary>
        None = 0,

        /// <summary>
        /// Can invite new members to the guild.
        /// </summary>
        Invite = 1 << 0,

        /// <summary>
        /// Can remove members from the guild.
        /// </summary>
        Kick = 1 << 1,

        /// <summary>
        /// Can promote members to higher ranks (within authority limits).
        /// </summary>
        Promote = 1 << 2,

        /// <summary>
        /// Can demote members to lower ranks (within authority limits).
        /// </summary>
        Demote = 1 << 3,

        /// <summary>
        /// Can withdraw gold from the guild treasury.
        /// </summary>
        WithdrawGold = 1 << 4,

        /// <summary>
        /// Can withdraw items from the guild treasury.
        /// </summary>
        WithdrawItems = 1 << 5,

        /// <summary>
        /// Can deposit into the guild treasury.
        /// </summary>
        DepositTreasury = 1 << 6,

        /// <summary>
        /// Can start wars/hostilities with other guilds.
        /// </summary>
        StartWar = 1 << 7,

        /// <summary>
        /// Can edit the guild's message of the day.
        /// </summary>
        EditMotd = 1 << 8,

        /// <summary>
        /// Can manage alliances with other guilds.
        /// </summary>
        ManageAlliances = 1 << 9,

        /// <summary>
        /// Can edit guild information (name, description, tag).
        /// </summary>
        EditGuildInfo = 1 << 10,

        /// <summary>
        /// Can disband the guild entirely.
        /// </summary>
        DisbandGuild = 1 << 11,

        /// <summary>
        /// Can view treasury contents and transaction history.
        /// </summary>
        ViewTreasury = 1 << 12,

        /// <summary>
        /// Can create and manage guild quests.
        /// </summary>
        ManageQuests = 1 << 13,

        /// <summary>
        /// Can organize and schedule guild events.
        /// </summary>
        OrganizeEvents = 1 << 14,

        /// <summary>
        /// Can modify rank permissions and privileges.
        /// </summary>
        ModifyRanks = 1 << 15,

        /// <summary>
        /// Can transfer guild leadership to another member.
        /// </summary>
        TransferLeadership = 1 << 16,

        /// <summary>
        /// Can set notes on other members.
        /// </summary>
        SetMemberNotes = 1 << 17,

        /// <summary>
        /// All permissions (admin level).
        /// </summary>
        All = ~0
    }
}
