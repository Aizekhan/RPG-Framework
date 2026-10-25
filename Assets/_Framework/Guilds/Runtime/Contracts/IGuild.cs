using System;
using System.Collections.Generic;
using RPGFramework.Factions;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Represents a guild - an extended faction with membership, ranks, and shared resources.
    /// Guilds are player-joinable organizations with hierarchical structure and collective goals.
    /// </summary>
    public interface IGuild : IFaction
    {
        /// <summary>
        /// Unique guild identifier (same as FactionId).
        /// </summary>
        string GuildId { get; }

        /// <summary>
        /// Guild's message of the day, visible to all members.
        /// </summary>
        string MessageOfTheDay { get; }

        /// <summary>
        /// Guild tag/abbreviation (e.g., "MYTH" for "MythHunters").
        /// </summary>
        string Tag { get; }

        /// <summary>
        /// Date and time when the guild was created.
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Entity ID of the guild leader.
        /// </summary>
        int LeaderId { get; }

        /// <summary>
        /// Current member count.
        /// </summary>
        int MemberCount { get; }

        /// <summary>
        /// Maximum number of members allowed.
        /// </summary>
        int MaxMembers { get; }

        /// <summary>
        /// Guild level (determines benefits, treasury capacity, etc.).
        /// </summary>
        int Level { get; }

        /// <summary>
        /// Total experience points accumulated by the guild.
        /// </summary>
        long Experience { get; }

        /// <summary>
        /// Get all members of this guild.
        /// </summary>
        /// <returns>Read-only collection of guild members.</returns>
        IReadOnlyList<IGuildMember> GetMembers();

        /// <summary>
        /// Get a specific member by entity ID.
        /// </summary>
        /// <param name="entityId">Entity ID of the member.</param>
        /// <returns>Guild member, or null if not found.</returns>
        IGuildMember GetMember(int entityId);

        /// <summary>
        /// Check if an entity is a member of this guild.
        /// </summary>
        /// <param name="entityId">Entity ID to check.</param>
        /// <returns>True if entity is a member.</returns>
        bool IsMember(int entityId);

        /// <summary>
        /// Get all ranks defined for this guild.
        /// </summary>
        /// <returns>Read-only collection of guild ranks.</returns>
        IReadOnlyList<IGuildRank> GetRanks();

        /// <summary>
        /// Get a specific rank by type.
        /// </summary>
        /// <param name="rankType">Rank type to retrieve.</param>
        /// <returns>Guild rank, or null if not found.</returns>
        IGuildRank GetRank(GuildRankType rankType);

        /// <summary>
        /// Check if the guild is at maximum capacity.
        /// </summary>
        /// <returns>True if guild is full.</returns>
        bool IsFull();

        /// <summary>
        /// Update the message of the day.
        /// </summary>
        /// <param name="motd">New message of the day.</param>
        void SetMessageOfTheDay(string motd);

        /// <summary>
        /// Add experience to the guild.
        /// May trigger level up.
        /// </summary>
        /// <param name="amount">Amount of experience to add.</param>
        void AddExperience(long amount);
    }
}
