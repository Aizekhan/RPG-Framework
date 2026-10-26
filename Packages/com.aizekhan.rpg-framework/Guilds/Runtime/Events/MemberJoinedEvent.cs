using System;
using RPGFramework.Events;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when a new member joins a guild.
    /// </summary>
    public class MemberJoinedEvent : IEvent
    {
        /// <summary>
        /// Guild the member joined.
        /// </summary>
        public string GuildId { get; }

        /// <summary>
        /// Entity ID of the new member.
        /// </summary>
        public int EntityId { get; }

        /// <summary>
        /// Initial rank assigned to the member.
        /// </summary>
        public GuildRankType InitialRank { get; }

        /// <summary>
        /// Entity ID of who invited the member (0 if self-joined or founder).
        /// </summary>
        public int InvitedBy { get; }

        /// <summary>
        /// Timestamp when the member joined.
        /// </summary>
        public DateTime Timestamp { get; }

        public MemberJoinedEvent(string guildId, int entityId, GuildRankType initialRank, int invitedBy = 0)
        {
            GuildId = guildId ?? throw new ArgumentNullException(nameof(guildId));
            EntityId = entityId;
            InitialRank = initialRank;
            InvitedBy = invitedBy;
            Timestamp = DateTime.UtcNow;
        }
    }
}
