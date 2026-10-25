using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when a guild member is demoted to a lower rank.
    /// </summary>
    public class MemberDemotedEvent
    {
        /// <summary>
        /// Guild ID.
        /// </summary>
        public string GuildId { get; }

        /// <summary>
        /// Entity ID of the member who was demoted.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Previous rank.
        /// </summary>
        public GuildRankType OldRank { get; }

        /// <summary>
        /// New rank after demotion.
        /// </summary>
        public GuildRankType NewRank { get; }

        /// <summary>
        /// Entity ID of who performed the demotion.
        /// </summary>
        public int DemotedBy { get; }

        /// <summary>
        /// Reason for demotion (optional).
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Timestamp of the demotion.
        /// </summary>
        public DateTime Timestamp { get; }

        public MemberDemotedEvent(
            string guildId,
            int entityId,
            GuildRankType oldRank,
            GuildRankType newRank,
            int demotedBy,
            string reason = null)
        {
            GuildId = guildId ?? throw new ArgumentNullException(nameof(guildId));
            EntityId = entityId;
            OldRank = oldRank;
            NewRank = newRank;
            DemotedBy = demotedBy;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }
}
