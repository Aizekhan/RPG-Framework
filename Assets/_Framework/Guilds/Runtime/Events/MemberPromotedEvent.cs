using System;
using RPGFramework.Events;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when a guild member is promoted to a higher rank.
    /// </summary>
    public class MemberPromotedEvent : IEvent
    {
        /// <summary>
        /// Guild ID.
        /// </summary>
        public string GuildId { get; }

        /// <summary>
        /// Entity ID of the member who was promoted.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Previous rank.
        /// </summary>
        public GuildRankType OldRank { get; }

        /// <summary>
        /// New rank after promotion.
        /// </summary>
        public GuildRankType NewRank { get; }

        /// <summary>
        /// Entity ID of who performed the promotion.
        /// </summary>
        public int PromotedBy { get; }

        /// <summary>
        /// Timestamp of the promotion.
        /// </summary>
        public DateTime Timestamp { get; }

        public MemberPromotedEvent(
            string guildId,
            int entityId,
            GuildRankType oldRank,
            GuildRankType newRank,
            int promotedBy)
        {
            GuildId = guildId ?? throw new ArgumentNullException(nameof(guildId));
            EntityId = entityId;
            OldRank = oldRank;
            NewRank = newRank;
            PromotedBy = promotedBy;
            Timestamp = DateTime.UtcNow;
        }
    }
}
