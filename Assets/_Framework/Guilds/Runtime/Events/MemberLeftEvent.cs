using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when a member leaves or is removed from a guild.
    /// </summary>
    public class MemberLeftEvent
    {
        /// <summary>
        /// Guild the member left.
        /// </summary>
        public string GuildId { get; }

        /// <summary>
        /// Entity ID of the member who left.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Rank the member had when leaving.
        /// </summary>
        public GuildRankType LastRank { get; }

        /// <summary>
        /// Whether the member left voluntarily or was kicked.
        /// </summary>
        public bool WasKicked { get; }

        /// <summary>
        /// Entity ID of who kicked the member (0 if voluntary leave).
        /// </summary>
        public int KickedBy { get; }

        /// <summary>
        /// Reason for leaving/being kicked (optional).
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Timestamp when the member left.
        /// </summary>
        public DateTime Timestamp { get; }

        public MemberLeftEvent(
            string guildId,
            int entityId,
            GuildRankType lastRank,
            bool wasKicked,
            int kickedBy = 0,
            string reason = null)
        {
            GuildId = guildId ?? throw new ArgumentNullException(nameof(guildId));
            EntityId = entityId;
            LastRank = lastRank;
            WasKicked = wasKicked;
            KickedBy = kickedBy;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }
}
