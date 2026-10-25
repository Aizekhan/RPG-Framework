using System;
using System.Collections.Generic;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Core guild state data.
    /// Stores essential guild information and configuration.
    /// </summary>
    [Serializable]
    public struct GuildData
    {
        /// <summary>
        /// Unique guild identifier.
        /// </summary>
        public string GuildId;

        /// <summary>
        /// Guild name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Guild tag/abbreviation.
        /// </summary>
        public string Tag;

        /// <summary>
        /// Guild description.
        /// </summary>
        public string Description;

        /// <summary>
        /// Message of the day.
        /// </summary>
        public string MessageOfTheDay;

        /// <summary>
        /// Date and time when the guild was created.
        /// </summary>
        public DateTime CreatedAt;

        /// <summary>
        /// Entity ID of the guild leader.
        /// </summary>
        public int LeaderId;

        /// <summary>
        /// Maximum number of members allowed.
        /// </summary>
        public int MaxMembers;

        /// <summary>
        /// Guild level.
        /// </summary>
        public int Level;

        /// <summary>
        /// Total experience points.
        /// </summary>
        public long Experience;

        /// <summary>
        /// Whether the guild is active.
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// List of member entity IDs.
        /// </summary>
        public List<int> MemberIds;

        /// <summary>
        /// Create new guild data.
        /// </summary>
        public static GuildData Create(
            string guildId,
            string name,
            string tag,
            int leaderId,
            string description = "",
            int maxMembers = 100)
        {
            return new GuildData
            {
                GuildId = guildId,
                Name = name,
                Tag = tag,
                Description = description ?? string.Empty,
                MessageOfTheDay = "Welcome to the guild!",
                CreatedAt = DateTime.UtcNow,
                LeaderId = leaderId,
                MaxMembers = maxMembers,
                Level = 1,
                Experience = 0,
                IsActive = true,
                MemberIds = new List<int> { leaderId }
            };
        }

        /// <summary>
        /// Check if guild is at maximum capacity.
        /// </summary>
        public bool IsFull()
        {
            return MemberIds != null && MemberIds.Count >= MaxMembers;
        }

        /// <summary>
        /// Get current member count.
        /// </summary>
        public int GetMemberCount()
        {
            return MemberIds?.Count ?? 0;
        }
    }
}
