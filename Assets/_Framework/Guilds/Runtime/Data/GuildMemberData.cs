using System;
using RPGFramework.ECS;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// ECS Component storing guild membership data for an entity.
    /// Attached to entities that are members of a guild.
    /// </summary>
    [Serializable]
    public struct GuildMemberData : IComponent
    {
        /// <summary>
        /// Guild ID this entity is a member of.
        /// Empty string if not in a guild.
        /// </summary>
        public string GuildId;

        /// <summary>
        /// Current rank within the guild.
        /// </summary>
        public GuildRankType Rank;

        /// <summary>
        /// Date and time when the entity joined the guild.
        /// </summary>
        public DateTime JoinedAt;

        /// <summary>
        /// Date and time of the entity's last activity.
        /// </summary>
        public DateTime LastActiveAt;

        /// <summary>
        /// Total contribution points to the guild.
        /// </summary>
        public long ContributionPoints;

        /// <summary>
        /// Personal note/title visible to other guild members.
        /// </summary>
        public string Note;

        /// <summary>
        /// Create new guild member data.
        /// </summary>
        public static GuildMemberData Create(string guildId, GuildRankType rank = GuildRankType.Initiate)
        {
            return new GuildMemberData
            {
                GuildId = guildId,
                Rank = rank,
                JoinedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                ContributionPoints = 0,
                Note = string.Empty
            };
        }

        /// <summary>
        /// Check if entity is in a guild.
        /// </summary>
        public bool IsInGuild()
        {
            return !string.IsNullOrEmpty(GuildId);
        }

        /// <summary>
        /// Clear guild membership.
        /// </summary>
        public void Clear()
        {
            GuildId = string.Empty;
            Rank = GuildRankType.Initiate;
            ContributionPoints = 0;
            Note = string.Empty;
        }
    }
}
